#define NOMINMAX
#include <windows.h>

// Loader diagnostic only. No CRT, threads, import patches, or generated input.
namespace {
DWORD markerError{};
volatile LONG lastEventProcess{};
BOOL CALLBACK Report(HWND window, LPARAM) {
    DWORD process{}; GetWindowThreadProcessId(window, &process);
    if (process == GetCurrentProcessId() && IsWindowVisible(window) && !GetWindow(window, GW_OWNER)) {
        SetPropW(window, L"KathanaInputHook.StartupStage", reinterpret_cast<HANDLE>(8));
        SetPropW(window, L"KathanaInputHook.StartupError", reinterpret_cast<HANDLE>(static_cast<ULONG_PTR>(markerError)));
    }
    return TRUE;
}
}
void Mark(bool debug = false) {
        wchar_t name[100];
        wsprintfW(name, L"Local\\KathanaInputHookV1_%lu", GetCurrentProcessId());
        HANDLE mapping = OpenFileMappingW(FILE_MAP_ALL_ACCESS, FALSE, name);
        markerError = mapping ? ERROR_SUCCESS : GetLastError();
        if (mapping) {
            auto state = static_cast<BYTE*>(MapViewOfFile(mapping, FILE_MAP_ALL_ACCESS, 0, 0, 64));
            if (state) {
                if (*reinterpret_cast<DWORD*>(state) == 0x4B484931 && *reinterpret_cast<DWORD*>(state + 4) == 1) {
                    // Diagnostic-only ABI: offset 40 counts debug callbacks, not input reads.
                    // Marker readiness is always 2 and can never activate the real bridge.
                    if (debug) InterlockedIncrement64(reinterpret_cast<volatile LONG64*>(state + 40));
                    InterlockedExchange(reinterpret_cast<volatile LONG*>(state + 24), 2);
                }
                UnmapViewOfFile(state);
            } else markerError = GetLastError();
            CloseHandle(mapping);
        }
        EnumWindows(Report, 0);
}
extern "C" __declspec(dllexport) LRESULT CALLBACK KathanaMessageHook(int code, WPARAM wp, LPARAM lp) {
    if (code >= 0) Mark();
    return CallNextHookEx(nullptr, code, wp, lp);
}
extern "C" __declspec(dllexport) LRESULT CALLBACK KathanaDebugHook(int code, WPARAM wp, LPARAM lp) {
    if (code >= 0 && wp == WH_CALLWNDPROC) Mark(true);
    return CallNextHookEx(nullptr, code, wp, lp);
}
extern "C" __declspec(dllexport) void CALLBACK KathanaWinEventHook(HWINEVENTHOOK, DWORD, HWND, LONG, LONG, DWORD, DWORD) {
    InterlockedExchange(&lastEventProcess, GetCurrentProcessId());
    Mark();
}
extern "C" __declspec(dllexport) DWORD WINAPI KathanaLastWinEventProcess() {
    return InterlockedCompareExchange(&lastEventProcess, 0, 0);
}
extern "C" BOOL WINAPI DllMain(HINSTANCE, DWORD, LPVOID) { return TRUE; }
