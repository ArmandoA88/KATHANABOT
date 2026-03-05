import sys

file_path = r"c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\BotEngine.vb"

with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Add _lastRightAltAt var
content = content.replace(
    "Private _zeroPairConfirmCount As Integer = 0\n    Private Shared ReadOnly MovementStopVks",
    "Private _zeroPairConfirmCount As Integer = 0\n    Private _lastRightAltAt As DateTime = DateTime.MinValue\n    Private Shared ReadOnly MovementStopVks"
)

# 2. Add RMENU to KeyMap
content = content.replace(
    "{\"Z\", &H5A},\n        {\"SPACE\"",
    "{\"Z\", &H5A},\n        {\"RMENU\", &HA5}, {\"RALT\", &HA5},\n        {\"SPACE\""
)

# 3. Reset in Start()
content = content.replace(
    "_zeroPairConfirmCount = 0\n            _task = Task.Run",
    "_zeroPairConfirmCount = 0\n            _lastRightAltAt = DateTime.MinValue\n            _task = Task.Run"
)

# 4. Add the 10s loop logic
content = content.replace(
    "Dim now As DateTime = DateTime.UtcNow\n            SavePeriodicSnapshot(frame, now)\n            Dim monsterFilterActive",
    "Dim now As DateTime = DateTime.UtcNow\n\n            If (now - _lastRightAltAt).TotalMilliseconds >= 10000 Then\n                If SendKey(hwnd, \"RMENU\", 200) Then\n                    _lastRightAltAt = now\n                    SetLastAction(\"RMENU (auto right-alt)\")\n                End If\n            End If\n\n            SavePeriodicSnapshot(frame, now)\n            Dim monsterFilterActive"
)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

print("Patch applied.")
