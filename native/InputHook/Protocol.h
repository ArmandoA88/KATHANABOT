#pragma once
#define NOMINMAX
#include <windows.h>
#include <cstdint>
#include <cwchar>

namespace kathana {
constexpr uint32_t Magic = 0x4B484931;
constexpr uint32_t Version = 1;
constexpr uint32_t Capacity = 512;
enum Kind : uint32_t { Key = 1, Mouse = 2, Cursor = 3, Text = 4, Release = 5 };
struct Command {
    uint32_t kind, key, flags;
    int32_t x, y;
    uint32_t data, sequence, reserved;
};
struct Shared {
    uint32_t magic, version;
    uint64_t hwnd;
    volatile LONG head, tail, ready, error;
    volatile LONG64 heartbeat, rawReads, messageReads;
    uint32_t owner, generation;
    Command commands[Capacity];
};
static_assert(sizeof(Command) == 32);
static_assert(offsetof(Shared, commands) == 64);
inline void MappingNameForProcess(wchar_t* name, size_t size, DWORD processId) {
    swprintf_s(name, size, L"Local\\KathanaInputHookV1_%lu", processId);
}
inline void MappingName(wchar_t* name, size_t size) {
    MappingNameForProcess(name, size, GetCurrentProcessId());
}
}
