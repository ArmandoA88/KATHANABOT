#include "Protocol.h"
#include <stdexcept>
#include <iostream>
#include <vector>
using QWORD = ULONGLONG;

namespace {
kathana::Shared* shared;
void Check(bool ok, const char* reason) { if (!ok) throw std::runtime_error(reason); }
void Send(uint32_t kind, uint32_t key = 0, uint32_t flags = 0, int x = 0, int y = 0, uint32_t data = 0) {
    uint32_t head = shared->head;
    shared->commands[head % kathana::Capacity] = {kind,key,flags,x,y,data,head+1,0};
    InterlockedExchange64(&shared->heartbeat, GetTickCount64());
    InterlockedExchange(&shared->head, head+1);
    auto deadline = GetTickCount64() + 1000;
    while (static_cast<uint32_t>(shared->tail) <= head && GetTickCount64() < deadline) Sleep(1);
    Check(static_cast<uint32_t>(shared->tail) > head, "command acknowledgement timed out");
}
std::vector<MSG> Drain() {
    std::vector<MSG> values; MSG msg{};
    while (PeekMessageW(&msg,nullptr,0,0,PM_REMOVE)) {
        values.push_back(msg);
        if (values.size() > 3000) throw std::runtime_error("message loop did not terminate");
    }
    return values;
}
void RawDrain() {
    alignas(8) BYTE buffer[65536]{};
    UINT bytes = sizeof(buffer);
    Check(GetRawInputBuffer(reinterpret_cast<PRAWINPUT>(buffer), &bytes, sizeof(RAWINPUTHEADER)) != UINT(-1), "raw read failed");
}
}
int main() {
    try {
        HWND window = CreateWindowExW(0,L"STATIC",L"Kathana hook test",WS_OVERLAPPED,40,40,640,480,nullptr,nullptr,GetModuleHandleW(nullptr),nullptr);
        Check(window != nullptr,"test window creation");
        wchar_t name[100]; kathana::MappingName(name,_countof(name));
        HANDLE map = CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,sizeof(kathana::Shared),name);
        shared = static_cast<kathana::Shared*>(MapViewOfFile(map,FILE_MAP_ALL_ACCESS,0,0,sizeof(kathana::Shared)));
        Check(shared != nullptr,"test mapping");
        ZeroMemory(shared,sizeof(*shared)); shared->magic=kathana::Magic; shared->version=kathana::Version;
        shared->hwnd=reinterpret_cast<uint64_t>(window); shared->owner=GetCurrentProcessId(); shared->generation=1;
        shared->heartbeat=GetTickCount64();
        Check(LoadLibraryW(L"KathanaInputHook.dll") != nullptr,"load hook DLL");
        auto deadline=GetTickCount64()+2000;
        while (!shared->ready && GetTickCount64()<deadline) Sleep(1);
        Check(shared->ready==1,"hook handshake/import coverage");
        // Ensure every mandatory hook is exercised through this executable's IAT.
        POINT physical{}; GetCursorPos(&physical);
        auto originalCursor=reinterpret_cast<BOOL(WINAPI*)(LPPOINT)>(GetProcAddress(GetModuleHandleW(L"user32.dll"),"GetCursorPos"));
        Send(kathana::Key,'1');
        Check((GetAsyncKeyState('1') & 0x8000)!=0,"async key overlay");
        Check((GetKeyState('1') & 0x8000)!=0,"queued key overlay");
        BYTE keys[256]{}; Check(GetKeyboardState(keys) && keys['1']==0x80,"keyboard buffer overlay");
        Check(GetForegroundWindow()==window && GetFocus()==window,"process-local focus");
        auto queue=GetQueueStatus(QS_RAWINPUT);
        Check((HIWORD(queue)&QS_RAWINPUT)!=0,"raw queue notification");
        UINT required=0; Check(GetRawInputBuffer(nullptr,&required,sizeof(RAWINPUTHEADER))==0 && required>=sizeof(RAWINPUTHEADER)+sizeof(RAWKEYBOARD),"raw size query");
        alignas(8) BYTE tooSmall[8]{}; UINT tinyCapacity=sizeof(tooSmall);
        Check(GetRawInputBuffer(reinterpret_cast<PRAWINPUT>(tooSmall),&tinyCapacity,sizeof(RAWINPUTHEADER))==UINT(-1),"undersized raw buffer must not consume command");
        Send(kathana::Key,'1',2);
        alignas(8) BYTE buffer[1024]{}; UINT bytes=sizeof(buffer);
        UINT count=GetRawInputBuffer(reinterpret_cast<PRAWINPUT>(buffer),&bytes,sizeof(RAWINPUTHEADER));
        Check(count==2,"raw down/up count");
        auto first=reinterpret_cast<PRAWINPUT>(buffer);
        auto second=NEXTRAWINPUTBLOCK(first);
        Check(first->header.dwType==RIM_TYPEKEYBOARD && first->data.keyboard.VKey=='1' && !(first->data.keyboard.Flags&RI_KEY_BREAK),"raw key down content");
        Check(second->data.keyboard.Flags&RI_KEY_BREAK,"aligned raw key up content");
        Check((GetAsyncKeyState('1')&0x8000)==0,"released async state");
        auto messages=Drain();
        bool down=false,up=false;
        for (auto& message:messages) { down|=message.message==WM_KEYDOWN&&message.wParam=='1'; up|=message.message==WM_KEYUP&&message.wParam=='1'; }
        Check(down&&up,"legacy keyboard down/up read from PeekMessage");
        Send(kathana::Text,0x263a);
        Check((HIWORD(GetQueueStatus(QS_KEY)) & QS_KEY)!=0,"text queue notification");
        MSG msg{}; Check(!PeekMessageW(&msg,nullptr,WM_MOUSEFIRST,WM_MOUSELAST,PM_REMOVE),"message filters preserved");
        Check(PeekMessageW(&msg,nullptr,WM_CHAR,WM_CHAR,PM_NOREMOVE)&&msg.wParam==0x263a,"UTF16 peek");
        Check(PeekMessageW(&msg,nullptr,WM_CHAR,WM_CHAR,PM_REMOVE)&&msg.wParam==0x263a,"UTF16 remove");
        Check(!PeekMessageW(&msg,nullptr,WM_CHAR,WM_CHAR,PM_REMOVE),"no duplicate character");
        LASTINPUTINFO beforeInput{sizeof(LASTINPUTINFO)}, afterInput{sizeof(LASTINPUTINFO)};
        GetLastInputInfo(&beforeInput);
        originalCursor(&physical);
        Send(kathana::Cursor,0,0,200,210);
        POINT virtualPoint{}; Check(GetCursorPos(&virtualPoint)&&virtualPoint.x==200&&virtualPoint.y==210,"virtual cursor");
        POINT actual{}; Check(originalCursor(&actual),"physical cursor query");
        GetLastInputInfo(&afterInput);
        if (beforeInput.dwTime == afterInput.dwTime)
            Check(actual.x==physical.x&&actual.y==physical.y,"OS cursor must remain unchanged");
        else std::cout << "SKIP: concurrent user input prevents a stable physical cursor comparison\n";
        Check(SetCursorPos(220,230),"client cursor update");
        Check(GetCursorPos(&virtualPoint)&&virtualPoint.x==220&&virtualPoint.y==230,"client cursor remains virtual");
        Send(kathana::Mouse,0,MOUSEEVENTF_LEFTDOWN);
        Check((GetAsyncKeyState(VK_LBUTTON)&0x8000)!=0,"mouse held state");
        Send(kathana::Mouse,0,MOUSEEVENTF_LEFTUP);
        Check((GetAsyncKeyState(VK_LBUTTON)&0x8000)==0,"mouse release");
        Send(kathana::Key,VK_RIGHT,1);
        Send(kathana::Key,VK_RIGHT,3);
        RawDrain(); Drain();
        Send(kathana::Key,'F'); Send(kathana::Mouse,0,MOUSEEVENTF_RIGHTDOWN);
        InterlockedExchange64(&shared->heartbeat,0);
        Sleep(30);
        messages=Drain();
        bool keyRelease=false,mouseRelease=false,deactivated=false;
        for (auto& message:messages) { keyRelease|=message.message==WM_KEYUP&&message.wParam=='F'; mouseRelease|=message.message==WM_RBUTTONUP; deactivated|=message.message==WM_ACTIVATEAPP&&!message.wParam; }
        Check(keyRelease&&mouseRelease,"heartbeat expiry releases held keyboard and mouse");
        Check(deactivated,"heartbeat expiry restores process-local activation state");
        RawDrain();
        // New ownership epoch reconnects without installing hooks a second time.
        shared->generation++; InterlockedExchange64(&shared->heartbeat,GetTickCount64()); Sleep(10);
        Send(kathana::Key,'2'); Check((GetAsyncKeyState('2')&0x8000)!=0,"reconnect state");
        Send(kathana::Release); Check((GetAsyncKeyState('2')&0x8000)==0,"explicit release command");
        InterlockedExchange64(&shared->heartbeat,0); Sleep(10);
        std::cout<<"PASS: native x64 IAT hooks, raw alignment/size/count, keyboard and mouse state, Unicode, filters, virtual cursor, heartbeat cleanup and reconnect.\n";
        // Resident hooks and their mapping remain valid until process exit.
        return 0;
    } catch (const std::exception& ex) { std::cerr<<"FAIL: "<<ex.what()<<"\n"; return 1; }
}
