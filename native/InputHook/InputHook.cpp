#include "Protocol.h"
#include <deque>
#include <mutex>
#include <string>
#include <algorithm>
#include <atomic>

namespace {
kathana::Shared* shared = nullptr;
std::mutex gate;
std::deque<RAWINPUT> raw;
std::deque<MSG> messages;
BYTE keys[256]{};
bool owned[256]{};
POINT cursor{};
bool cursorSet = false, wasLive = false;
HANDLE keyboardDevice = nullptr, mouseDevice = nullptr;
std::atomic<DWORD> windowThread{0};
uint32_t epoch = 0;
std::atomic<DWORD> startupStage{0}, startupError{0};
std::atomic<DWORD> lastEventProcess{0};
BOOL CALLBACK ReportStartup(HWND window, LPARAM) {
    DWORD process{}; GetWindowThreadProcessId(window, &process);
    if (process == GetCurrentProcessId() && IsWindowVisible(window) && !GetWindow(window, GW_OWNER)) {
        SetPropW(window, L"KathanaInputHook.StartupStage", reinterpret_cast<HANDLE>(static_cast<ULONG_PTR>(startupStage.load() + 1)));
        SetPropW(window, L"KathanaInputHook.StartupError", reinterpret_cast<HANDLE>(static_cast<ULONG_PTR>(startupError.load())));
    }
    return TRUE;
}
HWND Target() { return shared ? reinterpret_cast<HWND>(shared->hwnd) : nullptr; }
bool Live() {
    if (!shared || shared->magic != kathana::Magic || shared->version != kathana::Version || shared->ready != 1) return false;
    auto tick = InterlockedCompareExchange64(&shared->heartbeat, 0, 0);
    return tick != 0 && GetTickCount64() - static_cast<ULONGLONG>(tick) < 2000 && IsWindow(Target());
}
bool UIThread() { return windowThread && GetCurrentThreadId() == windowThread; }
void Message(UINT id, WPARAM wp, LPARAM lp) {
    MSG msg{}; msg.hwnd = Target(); msg.message = id; msg.wParam = wp; msg.lParam = lp;
    msg.time = GetTickCount(); msg.pt = cursor;
    messages.push_back(msg);
}
void RawKey(UINT vk, UINT flags) {
    RAWINPUT value{};
    value.header.dwType = RIM_TYPEKEYBOARD;
    value.header.dwSize = sizeof(RAWINPUTHEADER) + sizeof(RAWKEYBOARD);
    value.header.hDevice = keyboardDevice;
    value.header.wParam = RIM_INPUT;
    auto scan = MapVirtualKeyW(vk, MAPVK_VK_TO_VSC_EX);
    value.data.keyboard.MakeCode = static_cast<USHORT>(scan & 0xff);
    value.data.keyboard.VKey = static_cast<USHORT>(vk);
    value.data.keyboard.Flags = static_cast<USHORT>((flags & 2 ? RI_KEY_BREAK : RI_KEY_MAKE) |
        ((flags & 1 || (scan & 0xff00) == 0xe000) ? RI_KEY_E0 : 0));
    bool systemKey = (flags & 0x100) || keys[VK_MENU] || vk == VK_MENU || vk == VK_LMENU || vk == VK_RMENU;
    value.data.keyboard.Message = systemKey ? (flags & 2 ? WM_SYSKEYUP : WM_SYSKEYDOWN) : (flags & 2 ? WM_KEYUP : WM_KEYDOWN);
    raw.push_back(value);
    LPARAM lp = 1 | ((scan & 0xff) << 16) | (value.data.keyboard.Flags & RI_KEY_E0 ? 1LL << 24 : 0);
    if (flags & 2) lp |= (1LL << 30) | (1LL << 31);
    Message(value.data.keyboard.Message, vk, lp);
}
void SetKey(UINT vk, UINT flags) {
    if (vk >= 256) return;
    if ((flags & 2) && !owned[vk]) return;
    owned[vk] = (flags & 2) == 0;
    keys[vk] = flags & 2 ? 0 : 0x80;
    // Generic modifier polling must agree with left/right modifier events.
    for (auto pair : {std::pair<UINT, UINT>{VK_LCONTROL, VK_RCONTROL}, {VK_LSHIFT, VK_RSHIFT}, {VK_LMENU, VK_RMENU}}) {
        UINT generic = pair.first == VK_LCONTROL ? VK_CONTROL : pair.first == VK_LSHIFT ? VK_SHIFT : VK_MENU;
        if (vk == pair.first || vk == pair.second) keys[generic] = keys[pair.first] | keys[pair.second];
    }
    RawKey(vk, flags);
}
WPARAM MouseKeys() {
    return (keys[VK_LBUTTON] ? MK_LBUTTON : 0) | (keys[VK_RBUTTON] ? MK_RBUTTON : 0) |
        (keys[VK_MBUTTON] ? MK_MBUTTON : 0) | (keys[VK_XBUTTON1] ? MK_XBUTTON1 : 0) |
        (keys[VK_XBUTTON2] ? MK_XBUTTON2 : 0) | (keys[VK_CONTROL] ? MK_CONTROL : 0) | (keys[VK_SHIFT] ? MK_SHIFT : 0);
}
LPARAM ClientCursor() {
    POINT point = cursor;
    ScreenToClient(Target(), &point);
    return MAKELPARAM(static_cast<SHORT>(point.x), static_cast<SHORT>(point.y));
}
void RawMouse(UINT flags, int dx, int dy, UINT data) {
    RAWINPUT value{}; value.header.dwType = RIM_TYPEMOUSE;
    value.header.dwSize = sizeof(RAWINPUTHEADER) + sizeof(RAWMOUSE);
    value.header.hDevice = mouseDevice; value.header.wParam = RIM_INPUT;
    value.data.mouse.lLastX = dx; value.data.mouse.lLastY = dy;
    struct Button { UINT down, up, vk, downMsg, upMsg, rawDown, rawUp; };
    for (auto b : {Button{2,4,VK_LBUTTON,WM_LBUTTONDOWN,WM_LBUTTONUP,RI_MOUSE_LEFT_BUTTON_DOWN,RI_MOUSE_LEFT_BUTTON_UP},
                   Button{8,16,VK_RBUTTON,WM_RBUTTONDOWN,WM_RBUTTONUP,RI_MOUSE_RIGHT_BUTTON_DOWN,RI_MOUSE_RIGHT_BUTTON_UP},
                   Button{32,64,VK_MBUTTON,WM_MBUTTONDOWN,WM_MBUTTONUP,RI_MOUSE_MIDDLE_BUTTON_DOWN,RI_MOUSE_MIDDLE_BUTTON_UP}}) {
        if (flags & (b.down | b.up)) {
            bool up = (flags & b.up) != 0; keys[b.vk] = up ? 0 : 0x80;
            value.data.mouse.usButtonFlags |= static_cast<USHORT>(up ? b.rawUp : b.rawDown);
            Message(up ? b.upMsg : b.downMsg, MouseKeys(), ClientCursor());
        }
    }
    if (flags & (128 | 256)) {
        bool up = (flags & 256) != 0; bool second = data == XBUTTON2;
        keys[second ? VK_XBUTTON2 : VK_XBUTTON1] = up ? 0 : 0x80;
        value.data.mouse.usButtonFlags |= static_cast<USHORT>(second ? (up ? RI_MOUSE_BUTTON_5_UP : RI_MOUSE_BUTTON_5_DOWN) : (up ? RI_MOUSE_BUTTON_4_UP : RI_MOUSE_BUTTON_4_DOWN));
        Message(up ? WM_XBUTTONUP : WM_XBUTTONDOWN, MAKEWPARAM(MouseKeys(), data), ClientCursor());
    }
    if (flags & (MOUSEEVENTF_WHEEL | MOUSEEVENTF_HWHEEL)) {
        bool horizontal = (flags & MOUSEEVENTF_HWHEEL) != 0;
        value.data.mouse.usButtonFlags |= horizontal ? RI_MOUSE_HWHEEL : RI_MOUSE_WHEEL;
        value.data.mouse.usButtonData = static_cast<USHORT>(data);
        Message(horizontal ? WM_MOUSEHWHEEL : WM_MOUSEWHEEL, MAKEWPARAM(MouseKeys(), data), MAKELPARAM(cursor.x, cursor.y));
    }
    if (flags & MOUSEEVENTF_MOVE) Message(WM_MOUSEMOVE, MouseKeys(), ClientCursor());
    raw.push_back(value);
}
void ReleaseKeys() {
    // Preserve already queued commands so every accepted down has a corresponding up.
    for (UINT vk = 8; vk < 256; ++vk) if (owned[vk]) SetKey(vk, 2);
    for (auto pair : {std::pair<UINT,UINT>{VK_LBUTTON,4}, {VK_RBUTTON,16}, {VK_MBUTTON,64}, {VK_XBUTTON1,256}, {VK_XBUTTON2,256}})
        if (keys[pair.first]) RawMouse(pair.second, 0, 0, pair.first == VK_XBUTTON2 ? XBUTTON2 : XBUTTON1);
    ZeroMemory(keys, sizeof(keys)); ZeroMemory(owned, sizeof(owned)); cursorSet = false;
}
// Each hook calls the actual API first. These functions are imported by this DLL,
// whose IAT is deliberately left untouched, so forwarding cannot recurse.
UINT WINAPI Buffer(PRAWINPUT data, PUINT bytes, UINT header) {
    UINT capacity = bytes ? *bytes : 0;
    UINT count = GetRawInputBuffer(data, bytes, header);
    if (!UIThread() || !bytes || header != sizeof(RAWINPUTHEADER) || count == UINT(-1)) return count;
    std::lock_guard<std::mutex> lock(gate);
    if (!data) { if (!raw.empty()) *bytes = std::max(*bytes, static_cast<UINT>(raw.front().header.dwSize)); return count; }
    size_t used = 0;
    auto ptr = reinterpret_cast<BYTE*>(data);
    for (UINT i = 0; i < count; ++i) {
        if (used + sizeof(RAWINPUTHEADER) > capacity) return count;
        auto block = reinterpret_cast<PRAWINPUT>(ptr + used);
        size_t aligned = (static_cast<size_t>(block->header.dwSize) + 7) & ~size_t(7);
        if (!block->header.dwSize || used + aligned > capacity) return count;
        used += aligned;
    }
    while (!raw.empty()) {
        size_t size = raw.front().header.dwSize;
        size_t aligned = (size + 7) & ~size_t(7);
        if (used + aligned > capacity) break;
        memcpy(ptr + used, &raw.front(), size);
        if (aligned > size) ZeroMemory(ptr + used + size, aligned - size);
        used += aligned; raw.pop_front(); ++count;
        InterlockedIncrement64(&shared->rawReads);
    }
    if (!count && !raw.empty() && capacity < raw.front().header.dwSize) {
        *bytes = raw.front().header.dwSize; SetLastError(ERROR_INSUFFICIENT_BUFFER); return UINT(-1);
    }
    return count;
}
BOOL Peek(bool unicode, LPMSG msg, HWND hwnd, UINT first, UINT last, UINT remove) {
    BOOL found = unicode ? PeekMessageW(msg, hwnd, first, last, remove) : PeekMessageA(msg, hwnd, first, last, remove);
    if (!UIThread() || !msg) return found;
    if (found) {
        // Focus changes are virtualized only inside this client's input loop.
        if (Live() && msg->hwnd == Target()) {
            if (msg->message == WM_ACTIVATEAPP) msg->wParam = TRUE;
            if (msg->message == WM_ACTIVATE && LOWORD(msg->wParam) == WA_INACTIVE) msg->wParam = WA_ACTIVE;
            if (msg->message == WM_KILLFOCUS) { msg->message = WM_SETFOCUS; msg->wParam = 0; }
        }
        return found;
    }
    if (hwnd == reinterpret_cast<HWND>(-1) || (hwnd && hwnd != Target())) return FALSE;
    std::lock_guard<std::mutex> lock(gate);
    for (auto it = messages.begin(); it != messages.end(); ++it) {
        if ((first || last) && (it->message < first || it->message > last)) continue;
        *msg = *it;
        if (it->message >= WM_MOUSEFIRST && it->message <= WM_MOUSELAST) { cursor = it->pt; cursorSet = true; }
        if (remove & PM_REMOVE) { messages.erase(it); InterlockedIncrement64(&shared->messageReads); }
        return TRUE;
    }
    return FALSE;
}
BOOL WINAPI PeekW(LPMSG m, HWND w, UINT a, UINT b, UINT r) { return Peek(true,m,w,a,b,r); }
BOOL WINAPI PeekA(LPMSG m, HWND w, UINT a, UINT b, UINT r) { return Peek(false,m,w,a,b,r); }
SHORT WINAPI AsyncKey(int key) {
    SHORT original = GetAsyncKeyState(key);
    if (!Live() || key < 0 || key >= 256) return original;
    std::lock_guard<std::mutex> lock(gate);
    return static_cast<SHORT>((GetForegroundWindow() == Target() ? original : 0) | (keys[key] ? 0x8000 : 0));
}
SHORT WINAPI KeyState(int key) {
    SHORT original = GetKeyState(key);
    if (!Live() || key < 0 || key >= 256) return original;
    std::lock_guard<std::mutex> lock(gate);
    return static_cast<SHORT>((GetForegroundWindow() == Target() ? original : 0) | (keys[key] ? 0x8000 : 0));
}
BOOL WINAPI KeyboardState(PBYTE out) {
    BOOL result = GetKeyboardState(out);
    if (!result || !Live()) return result;
    std::lock_guard<std::mutex> lock(gate);
    if (GetForegroundWindow() != Target()) ZeroMemory(out, 256);
    for (int i = 0; i < 256; ++i) out[i] |= keys[i];
    return result;
}
BOOL WINAPI CursorPos(LPPOINT point) {
    BOOL result = GetCursorPos(point);
    if (Live() && point) { std::lock_guard<std::mutex> lock(gate); if (cursorSet) { *point = cursor; return TRUE; } }
    return result;
}
BOOL WINAPI SetPosition(int x, int y) {
    if (Live()) { std::lock_guard<std::mutex> lock(gate); cursor = {x,y}; cursorSet = true; return TRUE; }
    return SetCursorPos(x,y);
}
HWND WINAPI Foreground() { HWND original = GetForegroundWindow(); return Live() && UIThread() ? Target() : original; }
HWND WINAPI Focus() { HWND original = GetFocus(); return Live() && UIThread() ? Target() : original; }
DWORD WINAPI QueueStatus(UINT flags) {
    DWORD result = GetQueueStatus(flags);
    if (UIThread()) {
        std::lock_guard<std::mutex> lock(gate);
        UINT pending = raw.empty() ? 0 : QS_RAWINPUT;
        for (auto& message : messages) {
            if (message.message >= WM_KEYFIRST && message.message <= WM_KEYLAST) pending |= QS_KEY;
            else if (message.message == WM_MOUSEMOVE) pending |= QS_MOUSEMOVE;
            else if (message.message >= WM_MOUSEFIRST && message.message <= WM_MOUSELAST) pending |= QS_MOUSEBUTTON;
            else pending |= QS_POSTMESSAGE;
        }
        pending &= flags;
        result |= MAKELONG(pending, pending);
    }
    return result;
}
struct Hook { const char* name; void* function; bool installed; };
Hook hooks[] = {
    {"GetRawInputBuffer", reinterpret_cast<void*>(Buffer)},
    {"PeekMessageW", reinterpret_cast<void*>(PeekW)}, {"PeekMessageA", reinterpret_cast<void*>(PeekA)},
    {"GetAsyncKeyState", reinterpret_cast<void*>(AsyncKey)}, {"GetKeyState", reinterpret_cast<void*>(KeyState)},
    {"GetKeyboardState", reinterpret_cast<void*>(KeyboardState)}, {"GetCursorPos", reinterpret_cast<void*>(CursorPos)},
    {"SetCursorPos", reinterpret_cast<void*>(SetPosition)}, {"GetForegroundWindow", reinterpret_cast<void*>(Foreground)},
    {"GetFocus", reinterpret_cast<void*>(Focus)}, {"GetQueueStatus", reinterpret_cast<void*>(QueueStatus)}
};
bool Install() {
    auto base = reinterpret_cast<BYTE*>(GetModuleHandleW(nullptr));
    auto dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
    auto nt = reinterpret_cast<IMAGE_NT_HEADERS64*>(base + dos->e_lfanew);
    auto directory = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (!directory.VirtualAddress) return false;
    auto import = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(base + directory.VirtualAddress);
    for (; import->Name; ++import) {
        if (!import->OriginalFirstThunk) continue;
        auto names = reinterpret_cast<IMAGE_THUNK_DATA64*>(base + import->OriginalFirstThunk);
        auto addresses = reinterpret_cast<IMAGE_THUNK_DATA64*>(base + import->FirstThunk);
        for (; names->u1.AddressOfData; ++names, ++addresses) {
            if (IMAGE_SNAP_BY_ORDINAL64(names->u1.Ordinal)) continue;
            auto entry = reinterpret_cast<IMAGE_IMPORT_BY_NAME*>(base + names->u1.AddressOfData);
            for (auto& hook : hooks) if (!strcmp(reinterpret_cast<char*>(entry->Name), hook.name)) {
                DWORD old;
                if (!VirtualProtect(&addresses->u1.Function, sizeof(void*), PAGE_READWRITE, &old)) return false;
                InterlockedExchangePointer(reinterpret_cast<PVOID volatile*>(&addresses->u1.Function), hook.function);
                DWORD unused; VirtualProtect(&addresses->u1.Function, sizeof(void*), old, &unused);
                hook.installed = true;
            }
        }
    }
    return hooks[0].installed && (hooks[1].installed || hooks[2].installed) && hooks[3].installed && hooks[6].installed;
}
void Devices() {
    UINT count = 0;
    if (GetRawInputDeviceList(nullptr, &count, sizeof(RAWINPUTDEVICELIST)) != 0 || !count) return;
    auto list = new RAWINPUTDEVICELIST[count];
    UINT received = GetRawInputDeviceList(list, &count, sizeof(RAWINPUTDEVICELIST));
    if (received != UINT(-1)) for (UINT i = 0; i < received; ++i) {
        if (list[i].dwType == RIM_TYPEKEYBOARD && !keyboardDevice) keyboardDevice = list[i].hDevice;
        if (list[i].dwType == RIM_TYPEMOUSE && !mouseDevice) mouseDevice = list[i].hDevice;
    }
    delete[] list;
}
DWORD WINAPI Worker(void*) {
    startupStage = 1;
    wchar_t name[100]; kathana::MappingName(name, _countof(name));
    HANDLE mapping = OpenFileMappingW(FILE_MAP_ALL_ACCESS, FALSE, name);
    if (!mapping) { startupError = GetLastError(); startupStage = 2; return 1; }
    shared = static_cast<kathana::Shared*>(MapViewOfFile(mapping, FILE_MAP_ALL_ACCESS,0,0,sizeof(kathana::Shared)));
    if (!shared) { startupError = GetLastError(); startupStage = 3; CloseHandle(mapping); return 2; }
    // Patched imports must remain valid after a temporary Windows message hook
    // is removed. Keep this module resident for the target process lifetime.
    HMODULE resident{};
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
        reinterpret_cast<LPCWSTR>(&Worker), &resident)) {
        InterlockedExchange(&shared->error, GetLastError());
        startupError = GetLastError(); startupStage = 4;
        InterlockedExchange(&shared->ready, -1); return 3;
    }
    Devices();
    if (shared->magic != kathana::Magic || shared->version != kathana::Version || !Install()) {
        InterlockedExchange(&shared->error, ERROR_NOT_SUPPORTED); InterlockedExchange(&shared->ready,-1); return 3;
    }
    InterlockedExchange(&shared->ready,1);
    startupStage = 5;
    for (;;) {
        {
            std::lock_guard<std::mutex> lock(gate);
            bool live = Live();
            if (wasLive && (!live || epoch != shared->generation)) {
                raw.clear(); messages.clear(); ReleaseKeys();
                if (!live && GetForegroundWindow() != Target()) {
                    Message(WM_ACTIVATEAPP, FALSE, 0);
                    Message(WM_ACTIVATE, WA_INACTIVE, 0);
                    Message(WM_KILLFOCUS, reinterpret_cast<WPARAM>(GetForegroundWindow()), 0);
                }
            }
            if (live && (!wasLive || epoch != shared->generation)) {
                windowThread = GetWindowThreadProcessId(Target(), nullptr);
                if (!cursorSet) GetCursorPos(&cursor);
                Message(WM_ACTIVATEAPP, TRUE, 0); Message(WM_ACTIVATE, WA_ACTIVE, 0); Message(WM_SETFOCUS, 0, 0);
            }
            wasLive = live; epoch = shared->generation;
            if (!live) {
                InterlockedExchange(&shared->tail, InterlockedCompareExchange(&shared->head,0,0));
            } else {
                while (shared->tail != shared->head && raw.size() < 1000 && messages.size() < 1000) {
                    auto tail = static_cast<uint32_t>(InterlockedCompareExchange(&shared->tail,0,0));
                    MemoryBarrier(); auto command = shared->commands[tail % kathana::Capacity];
                    if (command.sequence != tail + 1) { InterlockedExchange(&shared->error,ERROR_INVALID_DATA); break; }
                    switch (command.kind) {
                    case kathana::Key: SetKey(command.key, command.flags); break;
                    case kathana::Text: Message(WM_CHAR, command.key & 0xffff, 1); break;
                    case kathana::Cursor: {
                        POINT old = cursor; cursor = {command.x,command.y}; cursorSet = true;
                        RawMouse(MOUSEEVENTF_MOVE, cursor.x-old.x, cursor.y-old.y,0); break;
                    }
                    case kathana::Mouse: RawMouse(command.flags,command.x,command.y,command.data); break;
                    case kathana::Release: raw.clear(); messages.clear(); ReleaseKeys(); break;
                    default: InterlockedExchange(&shared->error, ERROR_INVALID_DATA); break;
                    }
                    InterlockedExchange(&shared->tail,static_cast<LONG>(tail+1));
                }
            }
        }
        Sleep(2);
    }
}
}
// A documented, thread-scoped loader entry point. The callback preserves every
// message; initialization and input state remain in the existing IPC worker.
extern "C" __declspec(dllexport) LRESULT CALLBACK KathanaMessageHook(int code, WPARAM wp, LPARAM lp) {
    if (code >= 0) EnumWindows(ReportStartup, 0);
    return CallNextHookEx(nullptr, code, wp, lp);
}
extern "C" __declspec(dllexport) void CALLBACK KathanaWinEventHook(HWINEVENTHOOK, DWORD, HWND, LONG, LONG, DWORD, DWORD) {
    lastEventProcess = GetCurrentProcessId();
    EnumWindows(ReportStartup, 0);
}
extern "C" __declspec(dllexport) DWORD WINAPI KathanaLastWinEventProcess() { return lastEventProcess.load(); }
BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(instance);
        // Windows starts this thread after loader notifications have completed.
        // No waiting, patching, or IPC is performed under the loader lock.
        HANDLE thread = CreateThread(nullptr,0,Worker,nullptr,0,nullptr);
        if (thread) CloseHandle(thread);
    }
    return TRUE;
}
