import re
import sys

file_path = r"c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\BotEngine.vb"

with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# Pattern to find all GetForegroundWindow definitions
pattern = r"[ \t]*<DllImport\(\"user32\.dll\", SetLastError:=True\)>[ \t\r\n]*Friend Function GetForegroundWindow\(\) As IntPtr[ \t\r\n]*End Function[ \t\r\n]*"

# Remove all of them
content = re.sub(pattern, "", content)

# Insert it exactly once in NativeMethods
insert_point = r"    Friend Delegate Function EnumWindowsProc\(hWnd As IntPtr, lParam As IntPtr\) As Boolean"
replacement = """    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function GetForegroundWindow() As IntPtr
    End Function

    Friend Delegate Function EnumWindowsProc(hWnd As IntPtr, lParam As IntPtr) As Boolean"""

content = re.sub(insert_point, replacement, content, count=1)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

print("Duplicates removed and single valid definition inserted.")
