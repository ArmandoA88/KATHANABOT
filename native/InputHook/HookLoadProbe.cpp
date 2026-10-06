#include "Protocol.h"
#include <winternl.h>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
HWND gameWindow{};
volatile LONG observedEvents{};
volatile LONG lastEventThread{}, lastEventKind{};
void CALLBACK ObserveWinEvent(HWINEVENTHOOK, DWORD event, HWND window, LONG, LONG, DWORD thread, DWORD) {
    if (window != gameWindow) return;
    InterlockedIncrement(&observedEvents);
    InterlockedExchange(&lastEventThread, thread); InterlockedExchange(&lastEventKind, event);
}
BOOL CALLBACK FindWindowForProcess(HWND hwnd, LPARAM pid) {
    DWORD owner{}; GetWindowThreadProcessId(hwnd, &owner);
    if (owner == static_cast<DWORD>(pid) && IsWindowVisible(hwnd) && !GetWindow(hwnd, GW_OWNER)) {
        gameWindow = hwnd; return FALSE;
    }
    return TRUE;
}
void Check(bool ok, const char* reason) {
    if (!ok) throw std::runtime_error(std::string(reason) + "; Windows error " + std::to_string(GetLastError()));
}
void TokenInfo(HANDLE process, const char* label) {
    HANDLE token{};
    if (!OpenProcessToken(process, TOKEN_QUERY, &token)) {
        std::cout << label << " token query error=" << GetLastError() << "\n"; return;
    }
    BYTE buffer[512]{}; DWORD returned{}, elevated{}, appContainer{}, uiAccess{};
    DWORD integrity{}, hasRestrictions{}, restrictedCount{}, restrictionsError{}, restrictedSidsError{};
    if (GetTokenInformation(token, TokenIntegrityLevel, buffer, sizeof(buffer), &returned)) {
        auto sid = reinterpret_cast<TOKEN_MANDATORY_LABEL*>(buffer)->Label.Sid;
        integrity = *GetSidSubAuthority(sid, *GetSidSubAuthorityCount(sid) - 1);
    }
    GetTokenInformation(token, TokenElevation, &elevated, sizeof(elevated), &returned);
    GetTokenInformation(token, TokenIsAppContainer, &appContainer, sizeof(appContainer), &returned);
    GetTokenInformation(token, TokenUIAccess, &uiAccess, sizeof(uiAccess), &returned);
    if (!GetTokenInformation(token, TokenHasRestrictions, &hasRestrictions, sizeof(hasRestrictions), &returned))
        restrictionsError = GetLastError();
    returned = 0;
    GetTokenInformation(token, TokenRestrictedSids, nullptr, 0, &returned);
    if (returned >= sizeof(DWORD) && returned <= 1024 * 1024) {
        std::vector<BYTE> groups(returned);
        if (GetTokenInformation(token, TokenRestrictedSids, groups.data(), returned, &returned))
            restrictedCount = reinterpret_cast<TOKEN_GROUPS*>(groups.data())->GroupCount;
        else restrictedSidsError = GetLastError();
    } else restrictedSidsError = GetLastError();
    std::cout << label << " integrity=" << integrity << "; elevated=" << elevated
        << "; app-container=" << appContainer << "; UIAccess=" << uiAccess
        << "; has-restrictions=" << hasRestrictions << "; restricted-SID-count=" << restrictedCount
        << "; restrictions-query-error=" << restrictionsError << "; restricted-SID-query-error=" << restrictedSidsError << "\n";
    CloseHandle(token);
}
void GrantedProcessAccess(DWORD pid, DWORD requested) {
    HANDLE handle = OpenProcess(requested, FALSE, pid);
    if (!handle) {
        std::cout << "Process rights requested=0x" << std::hex << requested << std::dec
            << "; open error=" << GetLastError() << "\n"; return;
    }
    using QueryObject = NTSTATUS(NTAPI*)(HANDLE, OBJECT_INFORMATION_CLASS, PVOID, ULONG, PULONG);
    auto query = reinterpret_cast<QueryObject>(GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "NtQueryObject"));
    PUBLIC_OBJECT_BASIC_INFORMATION info{}; ULONG returned{};
    if (query && query(handle, ObjectBasicInformation, &info, sizeof(info), &returned) >= 0) {
        std::cout << "Process rights requested=0x" << std::hex << requested << "; granted=0x" << info.GrantedAccess
            << "; missing=0x" << (requested & ~info.GrantedAccess) << std::dec << "\n";
    }
    CloseHandle(handle);
}
int TestTarget() {
    HWND window = CreateWindowExW(0, L"STATIC", L"Kathana hook loader test", WS_OVERLAPPED,
        -32000, -32000, 200, 100, nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);
    Check(window != nullptr, "Cannot create test window");
    ShowWindow(window, SW_SHOWNOACTIVATE);
    auto deadline = GetTickCount64() + 15000;
    ULONGLONG lastChild{};
    while (GetTickCount64() < deadline) {
        MSG msg{}; while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) DispatchMessageW(&msg);
        alignas(8) BYTE buffer[1024]{}; UINT bytes = sizeof(buffer);
        GetRawInputBuffer(reinterpret_cast<PRAWINPUT>(buffer), &bytes, sizeof(RAWINPUTHEADER));
        GetAsyncKeyState('I'); POINT point{}; GetCursorPos(&point);
        NotifyWinEvent(EVENT_OBJECT_LOCATIONCHANGE, window, OBJID_WINDOW, CHILDID_SELF);
        if (GetTickCount64() - lastChild >= 200) {
            // Generate HCBT_CREATEWND on this controlled target's own thread.
            // The child is never visible and does not change desktop focus.
            HWND child = CreateWindowExW(0, L"STATIC", L"", WS_CHILD, 0, 0, 1, 1,
                window, nullptr, GetModuleHandleW(nullptr), nullptr);
            if (child) DestroyWindow(child);
            lastChild = GetTickCount64();
        }
        Sleep(5);
    }
    return 0;
}
}

// Connection-only diagnostic: never activates the game or sends key commands.
int wmain(int argc, wchar_t** argv) {
    std::cout.setf(std::ios::unitbuf); std::wcout.setf(std::ios::unitbuf);
    HANDLE process{}, mapping{};
    HMODULE module{}; HHOOK hook{}, secondaryHook{}; kathana::Shared* shared{};
    HWINEVENTHOOK eventHook{}, observer{};
    PROCESS_INFORMATION testProcess{};
    int result = 2;
    try {
        if (argc == 2 && !wcscmp(argv[1], L"--test-target")) return TestTarget();
        if (argc < 2 || argc > 6) throw std::runtime_error("Usage: HookLoadProbe.exe <KathanaGame PID>|--self-test [get|call|return|cbt|debug|event|inspect] [marker] [normal] [frame]");
        bool selfTest = !wcscmp(argv[1], L"--self-test");
        bool callHook{}, returnHook{}, marker{}, normalLoad{}, winEvent{}, frameTrigger{}, inspectOnly{}, cbtHook{}, debugHook{};
        for (int i = 2; i < argc; ++i) {
            if (!wcscmp(argv[i], L"call")) callHook = true;
            else if (!wcscmp(argv[i], L"return")) { callHook = true; returnHook = true; }
            else if (!wcscmp(argv[i], L"get")) {}
            else if (!wcscmp(argv[i], L"event")) winEvent = true;
            else if (!wcscmp(argv[i], L"marker")) marker = true;
            else if (!wcscmp(argv[i], L"normal")) normalLoad = true;
            else if (!wcscmp(argv[i], L"frame")) frameTrigger = true;
            else if (!wcscmp(argv[i], L"inspect")) inspectOnly = true;
            else if (!wcscmp(argv[i], L"cbt")) cbtHook = true;
            else if (!wcscmp(argv[i], L"debug")) { debugHook = true; callHook = true; marker = true; }
            else throw std::runtime_error("Unknown probe option");
        }
        DWORD pid{};
        wchar_t ownPath[32768]{}; GetModuleFileNameW(nullptr, ownPath, _countof(ownPath));
        if (selfTest) {
            auto command = std::wstring(L"\"") + ownPath + L"\" --test-target";
            STARTUPINFOW startup{}; startup.cb = sizeof(startup);
            Check(CreateProcessW(ownPath, command.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW,
                nullptr, nullptr, &startup, &testProcess), "Cannot launch the controlled test target");
            pid = testProcess.dwProcessId;
        } else pid = std::stoul(argv[1]);
        process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
        Check(process != nullptr, "Cannot query the target");
        wchar_t path[32768]{}; DWORD length = _countof(path);
        Check(QueryFullProcessImageNameW(process, 0, path, &length), "Cannot validate the target path");
        if (_wcsicmp(path, selfTest ? ownPath : L"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Kathana\\KathanaGame.exe"))
            throw std::runtime_error("Only the confirmed Steam KathanaGame installation is allowed");
        USHORT machine{}, nativeMachine{};
        Check(IsWow64Process2(process, &machine, &nativeMachine) && !machine && nativeMachine == IMAGE_FILE_MACHINE_AMD64,
            "Target must be native x64");
        auto windowDeadline = GetTickCount64() + 3000;
        do { EnumWindows(FindWindowForProcess, pid); if (!gameWindow) Sleep(10); }
        while (!gameWindow && GetTickCount64() < windowDeadline);
        Check(gameWindow != nullptr, "No game window found");
        std::cout << "Game foreground at connection: " << (GetForegroundWindow() == gameWindow) << "\n";
        std::cout << "Caller console attached=" << (GetConsoleWindow() != nullptr) << "\n";
        TokenInfo(GetCurrentProcess(), "Caller"); TokenInfo(process, "Target");
        GrantedProcessAccess(pid, PROCESS_QUERY_INFORMATION | PROCESS_VM_READ);
        GrantedProcessAccess(pid, PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_CREATE_THREAD);
        PROCESS_MITIGATION_BINARY_SIGNATURE_POLICY signatures{};
        if (GetProcessMitigationPolicy(process, ProcessSignaturePolicy, &signatures, sizeof(signatures)))
            std::cout << "Signature policy: Microsoft-only=" << signatures.MicrosoftSignedOnly
                << "; Store-only=" << signatures.StoreSignedOnly << "\n";
        PROCESS_MITIGATION_EXTENSION_POINT_DISABLE_POLICY extensions{};
        if (GetProcessMitigationPolicy(process, ProcessExtensionPointDisablePolicy, &extensions, sizeof(extensions)))
            std::cout << "Extension-point policy disabled=" << extensions.DisableExtensionPoints << "\n";
        PROCESS_MITIGATION_CONTROL_FLOW_GUARD_POLICY cfg{};
        if (GetProcessMitigationPolicy(process, ProcessControlFlowGuardPolicy, &cfg, sizeof(cfg)))
            std::cout << "CFG enabled=" << cfg.EnableControlFlowGuard << "; strict=" << cfg.StrictMode << "\n";
        PROCESS_MITIGATION_USER_SHADOW_STACK_POLICY cet{};
        if (GetProcessMitigationPolicy(process, ProcessUserShadowStackPolicy, &cet, sizeof(cet)))
            std::cout << "CET policy flags=" << cet.Flags << "\n";
        PROCESS_MITIGATION_IMAGE_LOAD_POLICY images{};
        if (GetProcessMitigationPolicy(process, ProcessImageLoadPolicy, &images, sizeof(images)))
            std::cout << "Image-load policy flags=" << images.Flags << "\n";
        PROCESS_PROTECTION_LEVEL_INFORMATION protection{};
        if (GetProcessInformation(process, ProcessProtectionLevelInfo, &protection, sizeof(protection)))
            std::cout << "Protection level=" << protection.ProtectionLevel << "\n";
        DWORD thread = GetWindowThreadProcessId(gameWindow, nullptr);
        wchar_t callerDesktop[256]{}, targetDesktop[256]{}; DWORD needed{};
        GetUserObjectInformationW(GetThreadDesktop(GetCurrentThreadId()), UOI_NAME, callerDesktop, sizeof(callerDesktop), &needed);
        GetUserObjectInformationW(GetThreadDesktop(thread), UOI_NAME, targetDesktop, sizeof(targetDesktop), &needed);
        std::wcout << L"Caller desktop=" << callerDesktop << L"; target desktop=" << targetDesktop << L"; thread=" << thread << L"\n";
        if (inspectOnly) { CloseHandle(process); process = nullptr; return 0; }
        wchar_t name[100]{}; kathana::MappingNameForProcess(name, _countof(name), pid);
        mapping = CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, sizeof(kathana::Shared), name);
        Check(mapping != nullptr, "Cannot create the input mapping");
        if (GetLastError() == ERROR_ALREADY_EXISTS) throw std::runtime_error("An input mapping already exists; stop its owner before this probe");
        shared = static_cast<kathana::Shared*>(MapViewOfFile(mapping, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(kathana::Shared)));
        Check(shared != nullptr, "Cannot map input state");
        ZeroMemory(shared, sizeof(*shared));
        shared->magic = kathana::Magic; shared->version = kathana::Version;
        shared->hwnd = reinterpret_cast<uint64_t>(gameWindow);
        shared->owner = GetCurrentProcessId(); shared->generation = 1;
        // Heartbeat stays zero: connecting cannot activate virtual input.
        auto slash = std::wstring(ownPath).find_last_of(L"\\/");
        auto dllPath = std::wstring(ownPath).substr(0, slash + 1) + (marker ? L"HookMarker.dll" : L"KathanaInputHook.dll");
        std::wcout << L"DLL=" << dllPath << L"; normal loading=" << normalLoad << L"\n";
        module = LoadLibraryExW(dllPath.c_str(), nullptr, normalLoad ? 0 : DONT_RESOLVE_DLL_REFERENCES);
        Check(module != nullptr, "Cannot map the diagnostic DLL");
        // Normal loading starts the full DLL's worker in this caller too. Keep
        // its code valid until caller exit, even if installing the hook fails.
        if (normalLoad) { HMODULE pinned{}; Check(GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_PIN,
            dllPath.c_str(), &pinned), "Cannot retain the local probe DLL"); }
        if (winEvent) {
            auto callback = reinterpret_cast<WINEVENTPROC>(GetProcAddress(module, "KathanaWinEventHook"));
            Check(callback != nullptr, "Missing WinEvent export");
            eventHook = SetWinEventHook(EVENT_MIN, EVENT_MAX, module, callback, pid, 0, WINEVENT_INCONTEXT);
            Check(eventHook != nullptr, "Windows rejected the in-context WinEvent hook");
            // Events about this HWND may be raised by another process/thread.
            // Observe out-of-context, immediately discarding every other HWND.
            // The DLL hook itself remains scoped only to the validated target PID.
            observer = SetWinEventHook(EVENT_MIN, EVENT_MAX, nullptr, ObserveWinEvent, 0, 0, WINEVENT_OUTOFCONTEXT);
            Check(observer != nullptr, "Cannot observe target event delivery");
            std::cout << "WinEvent hooks registered; waiting for a game window event\n";
            if (frameTrigger) {
                Check(SetWindowPos(gameWindow, nullptr, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED),
                    "Cannot request an unchanged window-frame refresh");
                std::cout << "Requested frame refresh; position, size, stacking and activation unchanged\n";
            }
        } else {
            auto callback = reinterpret_cast<HOOKPROC>(GetProcAddress(module, debugHook ? "KathanaDebugHook" : "KathanaMessageHook"));
            Check(callback != nullptr, "Missing message-hook export");
            hook = SetWindowsHookExW(debugHook ? WH_DEBUG : (cbtHook ? WH_CBT : (returnHook ? WH_CALLWNDPROCRET : (callHook ? WH_CALLWNDPROC : WH_GETMESSAGE))), callback, module, thread);
            Check(hook != nullptr, "Windows rejected the game-thread hook");
            if (debugHook) {
                auto secondary = reinterpret_cast<HOOKPROC>(GetProcAddress(module, "KathanaMessageHook"));
                Check(secondary != nullptr, "Missing secondary message-hook export");
                secondaryHook = SetWindowsHookExW(WH_CALLWNDPROC, secondary, module, thread);
                Check(secondaryHook != nullptr, "Windows rejected the secondary game-thread hook");
                std::cout << "Thread-scoped WH_DEBUG and WH_CALLWNDPROC markers registered; both hooks require cleanup\n";
            }
        }
        bool cbtForeground = GetForegroundWindow() == gameWindow;
        unsigned cbtActivations{}, cbtDeactivations{};
        if (cbtHook && !selfTest) std::cout << "CBT wait initial game foreground=" << cbtForeground << "\n";
        // WM_NULL only prompts the loader callback; it carries no bot input.
        if (winEvent || cbtHook) {}
        else if (callHook) {
            DWORD_PTR ignored{}; SetLastError(ERROR_SUCCESS);
            Check(SendMessageTimeoutW(gameWindow, WM_NULL, 0, 0, SMTO_ABORTIFHUNG | SMTO_ERRORONEXIT, 2000, &ignored) != 0,
                "Cannot notify the game window");
        } else Check(PostMessageW(gameWindow, WM_NULL, 0, 0), "Cannot notify the game message loop");
        auto deadline = GetTickCount64() + ((winEvent || cbtHook) && !selfTest ? 25000 : 5000);
        while ((!shared->ready || (debugHook && !shared->rawReads)) && GetTickCount64() < deadline) {
            MSG msg{}; while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) DispatchMessageW(&msg);
            if (cbtHook && !selfTest) {
                bool foreground = GetForegroundWindow() == gameWindow;
                if (foreground != cbtForeground) {
                    if (foreground) ++cbtActivations; else ++cbtDeactivations;
                    std::cout << "Observed game foreground transition " << cbtForeground << " -> " << foreground << " during CBT wait\n";
                    cbtForeground = foreground;
                }
            }
            Sleep(10);
        }
        if (cbtHook && !selfTest) {
            std::cout << "CBT game activation transitions=" << cbtActivations << "; deactivation transitions=" << cbtDeactivations
                << "; final foreground=" << (GetForegroundWindow() == gameWindow) << "\n";
            if (!cbtActivations) std::cout << "No game activation transition was observed; this trigger did not establish a CBT activation callback opportunity\n";
        }
        if (winEvent) {
            auto drainDeadline = GetTickCount64() + 100;
            do {
                MSG msg{}; while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) DispatchMessageW(&msg);
                Sleep(5);
            } while (GetTickCount64() < drainDeadline);
            // An in-context request may fall back to this caller. That is not
            // an injected callback and must never count as target connection.
            auto context = reinterpret_cast<DWORD(WINAPI*)()>(GetProcAddress(module, "KathanaLastWinEventProcess"));
            std::cout << "Observed target WinEvents=" << observedEvents << "; callback executed in caller PID="
                << (context ? context() : 0) << "\n";
            HANDLE source = OpenThread(THREAD_QUERY_LIMITED_INFORMATION, FALSE, lastEventThread);
            std::cout << "Last target event=" << lastEventKind << "; source thread=" << lastEventThread
                << "; source PID=" << (source ? GetProcessIdOfThread(source) : 0) << "\n";
            if (source) CloseHandle(source);
            if (selfTest) Check(observedEvents > 0, "Controlled target events were not observed");
        }
        if (winEvent || cbtHook) {}
        else if (callHook) {
            DWORD_PTR ignored{};
            SendMessageTimeoutW(gameWindow, WM_NULL, 0, 0, SMTO_ABORTIFHUNG | SMTO_ERRORONEXIT, 1000, &ignored);
        } else {
            PostMessageW(gameWindow, WM_NULL, 0, 0); Sleep(50);
        }
        auto stage = reinterpret_cast<ULONG_PTR>(GetPropW(gameWindow, L"KathanaInputHook.StartupStage"));
        auto error = reinterpret_cast<ULONG_PTR>(GetPropW(gameWindow, L"KathanaInputHook.StartupError"));
        std::cout << "Callback observed=" << (stage != 0) << "; DLL startup stage=" << (stage ? stage - 1 : 0)
            << "; DLL startup error=" << error << "\n";
        std::cout << "Message hook installed; bridge ready=" << shared->ready << "; bridge error=" << shared->error << "\n";
        if (debugHook) {
            // Marker-only ABI: rawReads counts WH_DEBUG callbacks for WH_CALLWNDPROC.
            // Secondary marker readiness alone never establishes a debug callback.
            std::cout << "In-process WH_DEBUG callbacks for WH_CALLWNDPROC=" << shared->rawReads << "\n";
        }
        if (shared->ready != (marker ? 2 : 1)) throw std::runtime_error("The game did not initialize the diagnostic DLL");
        if (debugHook && !shared->rawReads) throw std::runtime_error("No in-process WH_DEBUG callback was observed");
        std::cout << (marker ? "PASS: minimal DLL callback ran and opened IPC; no game input was sent\n"
            : "PASS: thread-scoped Windows hook loaded the bridge; no game input was sent\n");
        result = 0;
    } catch (const std::exception& ex) { std::cerr << "FAILED: " << ex.what() << "\n"; }
    if (shared) InterlockedExchange64(&shared->heartbeat, 0);
    if (secondaryHook) UnhookWindowsHookEx(secondaryHook);
    if (hook) UnhookWindowsHookEx(hook);
    if (eventHook) UnhookWinEvent(eventHook);
    if (observer) UnhookWinEvent(observer);
    if (gameWindow) {
        RemovePropW(gameWindow, L"KathanaInputHook.StartupStage");
        RemovePropW(gameWindow, L"KathanaInputHook.StartupError");
    }
    if (module) FreeLibrary(module);
    if (shared) UnmapViewOfFile(shared);
    if (mapping) CloseHandle(mapping);
    if (process) CloseHandle(process);
    if (testProcess.hProcess) {
        TerminateProcess(testProcess.hProcess, 0); // Only the controlled child created above.
        CloseHandle(testProcess.hThread); CloseHandle(testProcess.hProcess);
    }
    return result;
}
