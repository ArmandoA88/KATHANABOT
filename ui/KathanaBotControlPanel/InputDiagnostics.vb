Imports System.Drawing.Imaging
Imports System.IO
Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Threading

' Uses the same capture and input methods as normal bot operation, without starting
' combat or loading a profile. This provides a controlled live inventory-key check.
Friend NotInheritable Class InputDiagnostics
    <DllImport("user32.dll", EntryPoint:="PostMessageW", SetLastError:=True)>
    Private Shared Function PostDiagnosticMessage(hwnd As IntPtr, message As UInteger, key As UIntPtr, flags As IntPtr) As Boolean
    End Function
    <DllImport("user32.dll", EntryPoint:="SendMessageTimeoutW", SetLastError:=True)>
    Private Shared Function SendDiagnosticMessage(hwnd As IntPtr, message As UInteger, key As UIntPtr, flags As IntPtr, options As UInteger, timeout As UInteger, ByRef result As UIntPtr) As IntPtr
    End Function

    Private Shared Function SendInventoryMessage(hwnd As IntPtr, message As UInteger, flags As Long, synchronous As Boolean) As Boolean
        Return SendTargetMessage(hwnd, message, 73UI, flags, synchronous)
    End Function

    Private Shared Function SendTargetMessage(hwnd As IntPtr, message As UInteger, value As UInteger, flags As Long, synchronous As Boolean) As Boolean
        If Not synchronous Then Return PostDiagnosticMessage(hwnd, message, New UIntPtr(value), New IntPtr(flags))
        Dim result As UIntPtr
        Return SendDiagnosticMessage(hwnd, message, New UIntPtr(value), New IntPtr(flags), &H23UI, 1000UI, result) <> IntPtr.Zero
    End Function

    Private Shared Sub CheckBackground(hwnd As IntPtr, expected As IntPtr)
        Dim actual = NativeMethods.GetForegroundWindow()
        If actual = IntPtr.Zero OrElse actual = hwnd OrElse actual <> expected Then Throw New InvalidOperationException("Foreground ownership changed; background diagnostic stopped.")
    End Sub

    Private Shared Sub WaitBackground(hwnd As IntPtr, foreground As IntPtr, milliseconds As Integer)
        Dim deadline = Environment.TickCount64 + milliseconds
        While Environment.TickCount64 < deadline
            CheckBackground(hwnd, foreground)
            Thread.Sleep(CInt(Math.Max(1L, Math.Min(25L, deadline - Environment.TickCount64))))
        End While
        CheckBackground(hwnd, foreground)
    End Sub

    Private Shared Function ClientCoordinates(platform As NativeBackgroundInputPlatform, hwnd As IntPtr, x As Integer, y As Integer) As Long
        If x > Short.MaxValue OrElse y > Short.MaxValue OrElse Not platform.ClientPoint(hwnd, x, y).HasValue Then
            Throw New ArgumentException("Diagnostic coordinates must be inside the game client and fit signed 16-bit messages.")
        End If
        Return (CLng(x) And &HFFFFL) Or ((CLng(y) And &HFFFFL) << 16)
    End Function

    Private Shared Sub SendBackgroundMouse(platform As NativeBackgroundInputPlatform, hwnd As IntPtr, pid As UInteger, foreground As IntPtr, message As UInteger, value As UInteger, coordinates As Long)
        CheckBackground(hwnd, foreground)
        If platform.ProcessId(hwnd) <> pid OrElse Not platform.Valid(hwnd) Then Throw New InvalidOperationException("Original game target is no longer valid.")
        If Not SendTargetMessage(hwnd, message, value, coordinates, True) Then Throw New Win32Exception(Marshal.GetLastWin32Error(), "Background mouse message timed out or failed.")
        CheckBackground(hwnd, foreground)
    End Sub

    Private Shared Sub ReleaseBackgroundMouse(platform As NativeBackgroundInputPlatform, hwnd As IntPtr, pid As UInteger, coordinates As Long)
        ' Cleanup remains target-only even when foreground ownership changed during the hold.
        If platform.ProcessId(hwnd) <> pid OrElse Not platform.Valid(hwnd) Then Throw New InvalidOperationException("Original game target became invalid before mouse release.")
        If Not SendTargetMessage(hwnd, &H202UI, 0UI, coordinates, True) Then Throw New Win32Exception(Marshal.GetLastWin32Error(), "Background mouse release timed out or failed.")
    End Sub

    Public Shared Function Run(args As String()) As Integer
        Dim index = Array.IndexOf(args, "--input-diagnostics")
        Dim action = If(index >= 0 AndAlso index + 1 < args.Length, args(index + 1), "capture")
        Dim backendInventory = action = "background-backend-inventory"
        Dim backendEscape = action = "background-backend-escape"
        Dim backendKey = action = "background-backend-key"
        Dim backendMouse = action = "background-backend-mouse"
        Dim engineInventory = action = "background-engine-inventory"
        Dim postedMouse = action = "background-post-mouse"
        Dim hoverPair = action = "background-hover-pair"
        Dim mouseState = action = "background-mouse-state"
        Dim backgroundObserve = action = "background-observe"
        Dim background = {"background-post-zero", "background-send-zero", "background-post-scan", "background-send-scan"}.Contains(action) OrElse backendInventory OrElse backendEscape OrElse backendKey OrElse backendMouse OrElse postedMouse OrElse engineInventory OrElse hoverPair OrElse mouseState OrElse backgroundObserve
        If action <> "capture" AndAlso action <> "inventory" AndAlso action <> "escape" AndAlso Not background Then Throw New ArgumentException("Use capture, inventory, escape, or a background-post/send-zero/scan diagnostic.")
        Dim folder = Path.Combine(AppContext.BaseDirectory, "input-diagnostics")
        Directory.CreateDirectory(folder)
        Dim stem = Path.Combine(folder, action & "_" & Environment.TickCount64.ToString())
        Dim report As New List(Of String) From {"Input backend: " & WindowsInput.InputMode,
            "Diagnostic: " & action, "Input method: " & If(background, action & " (target-only client input)", WindowsInput.InputMode)}
        Try
            If (backendMouse OrElse postedMouse OrElse hoverPair OrElse mouseState) AndAlso WindowsInput.KeyboardOnlyMode Then Throw New InvalidOperationException("This build supports background key presses only.")
            If (backendInventory OrElse backendEscape OrElse backendKey OrElse backendMouse OrElse engineInventory) AndAlso
                Not TypeOf WindowsInput.Current Is SdlBackgroundWindowsInput Then
                Throw New InvalidOperationException("Production background diagnostics require the SDL background build; this release uses foreground input.")
            End If
            Dim games = Process.GetProcessesByName("KathanaGame")
            If games.Length <> 1 Then Throw New InvalidOperationException("Open exactly one Kathana game for this controlled test.")
            Dim hwnd = games(0).MainWindowHandle
            report.Add("Game PID: " & games(0).Id.ToString())
            If hwnd = IntPtr.Zero Then Throw New InvalidOperationException("The game has no usable window.")
            Dim foreground = NativeMethods.GetForegroundWindow()
            If background Then
                report.Add("Game HWND: " & hwnd.ToString() & "; foreground HWND: " & foreground.ToString())
                CheckBackground(hwnd, foreground)
            ElseIf action <> "capture" Then
                If Not WindowsInput.Current.Activate(hwnd) Then Throw New InvalidOperationException("Game activation was denied. Click the game before retrying.")
                Thread.Sleep(150)
            End If
            SaveCapture(hwnd, stem & "_before.png", report)
            If backgroundObserve Then
                WaitBackground(hwnd, foreground, 1700)
                SaveCapture(hwnd, stem & "_after.png", report)
                CheckBackground(hwnd, foreground)
                report.Add("No input sent; observed game for 1700 ms with unchanged foreground HWND.")
            ElseIf hoverPair OrElse mouseState Then
                Dim requiredIndex = index + If(hoverPair, 5, 3)
                If requiredIndex >= args.Length Then Throw New ArgumentException(If(hoverPair, "background-hover-pair requires client x1 y1 x2 y2.", "background-mouse-state requires client x y."))
                Dim platform As New NativeBackgroundInputPlatform()
                Dim pid = CUInt(games(0).Id)
                If platform.ProcessId(hwnd) <> pid OrElse Not platform.Valid(hwnd) Then Throw New ArgumentException("Invalid exact-game diagnostic target.")
                Dim x = Integer.Parse(args(index + 2)), y = Integer.Parse(args(index + 3))
                Dim first = ClientCoordinates(platform, hwnd, x, y)
                If hoverPair Then
                    Dim x2 = Integer.Parse(args(index + 4)), y2 = Integer.Parse(args(index + 5))
                    Dim second = ClientCoordinates(platform, hwnd, x2, y2)
                    If first = second Then Throw New ArgumentException("Hover pair requires two distinct client points.")
                    report.Add($"Hover points: ({x},{y}) then ({x2},{y2}); dwell 700 ms each.")
                    SendBackgroundMouse(platform, hwnd, pid, foreground, &H200UI, 0UI, first)
                    WaitBackground(hwnd, foreground, 700)
                    SaveCapture(hwnd, stem & "_hover1.png", report)
                    CheckBackground(hwnd, foreground)
                    SendBackgroundMouse(platform, hwnd, pid, foreground, &H200UI, 0UI, second)
                    WaitBackground(hwnd, foreground, 700)
                    SaveCapture(hwnd, stem & "_hover2.png", report)
                    CheckBackground(hwnd, foreground)
                    SaveCapture(hwnd, stem & "_after.png", report)
                    CheckBackground(hwnd, foreground)
                    report.Add("Synchronous hover messages acknowledged; foreground HWND stayed unchanged. ACK alone does not establish client hover acceptance.")
                Else
                    Dim holdMs = If(index + 4 < args.Length, Integer.Parse(args(index + 4)), 150)
                    If holdMs < 1 OrElse holdMs > 1200 Then Throw New ArgumentException("Diagnostic mouse hold must be between 1 and 1200 ms.")
                    Dim nearby As Long? = Nothing
                    For Each offset In {New System.Drawing.Point(8, 0), New System.Drawing.Point(-8, 0), New System.Drawing.Point(0, 8), New System.Drawing.Point(0, -8)}
                        Dim nx = CLng(x) + offset.X, ny = CLng(y) + offset.Y
                        If nx < 0 OrElse ny < 0 OrElse nx > Short.MaxValue OrElse ny > Short.MaxValue Then Continue For
                        If platform.ClientPoint(hwnd, CInt(nx), CInt(ny)).HasValue Then
                            nearby = ClientCoordinates(platform, hwnd, CInt(nx), CInt(ny))
                            Exit For
                        End If
                    Next
                    If Not nearby.HasValue Then Throw New ArgumentException("No distinct nearby in-client point is available.")
                    report.Add($"Mouse-state target: ({x},{y}); explicit up reset, then {holdMs} ms down/up hold.")
                    SendBackgroundMouse(platform, hwnd, pid, foreground, &H200UI, 0UI, nearby.Value)
                    WaitBackground(hwnd, foreground, 25)
                    SendBackgroundMouse(platform, hwnd, pid, foreground, &H200UI, 0UI, first)
                    SendBackgroundMouse(platform, hwnd, pid, foreground, &H202UI, 0UI, first)
                    WaitBackground(hwnd, foreground, 50)
                    SaveCapture(hwnd, stem & "_hover.png", report)
                    SendBackgroundMouse(platform, hwnd, pid, foreground, &H200UI, 0UI, first)
                    Try
                        SendBackgroundMouse(platform, hwnd, pid, foreground, &H201UI, 1UI, first)
                        WaitBackground(hwnd, foreground, holdMs)
                    Finally
                        ReleaseBackgroundMouse(platform, hwnd, pid, first)
                    End Try
                    WaitBackground(hwnd, foreground, 500)
                    SaveCapture(hwnd, stem & "_after.png", report)
                    CheckBackground(hwnd, foreground)
                    report.Add("Synchronous mouse reset/down/up messages acknowledged; foreground HWND stayed unchanged. ACK alone does not establish client click acceptance.")
                End If
            ElseIf engineInventory Then
                Dim engine As New BotEngine()
                engine.UpdateConfig(New BotConfig With {
                    .SelectedWindowHandle = hwnd, .WindowTitle = "", .LiteModeEnabled = True,
                    .LoopMs = 50, .NormalRetargetEnabled = False, .ForcedRetargetEnabled = False,
                    .HighMaxHpSpecialEnabled = False, .BypassStuckTarget = False,
                    .PartyInviteAutoAcceptEnabled = False, .PartyRessAutoAcceptEnabled = False,
                    .LootScannerEnabled = False, .ChatTranslationEnabled = False,
                    .DisconnectMessageRect = New RectRegion(0, 0, 1, 1),
                    .Actions = New List(Of ActionRule) From {
                        New ActionRule With {.KeyName = "I", .Role = "buff", .Enabled = True,
                            .CooldownId = "background-inventory-diagnostic", .CooldownMs = 60000}}})
                Dim sent As Boolean
                Try
                    CheckBackground(hwnd, foreground)
                    engine.Start()
                    Dim deadline = Environment.TickCount64 + 3000
                    While Environment.TickCount64 < deadline
                        CheckBackground(hwnd, foreground)
                        If engine.GetStatus().LastAction = "I (buff)" Then
                            sent = True
                            Exit While
                        End If
                        Thread.Sleep(25)
                    End While
                Finally
                    engine.Stop()
                End Try
                If Not sent Then Throw New InvalidOperationException("The isolated bot engine did not complete its inventory action.")
                Thread.Sleep(500)
                CheckBackground(hwnd, foreground)
                SaveCapture(hwnd, stem & "_after.png", report)
                report.Add("Isolated Lite engine completed I (buff) with its normal key timing, then stopped. Foreground HWND stayed unchanged.")
            ElseIf postedMouse Then
                If index + 3 >= args.Length Then Throw New ArgumentException("background-post-mouse requires client x and y.")
                Dim x = Integer.Parse(args(index + 2)), y = Integer.Parse(args(index + 3))
                Dim platform As New NativeBackgroundInputPlatform()
                If Not platform.Valid(hwnd) OrElse Not platform.ClientPoint(hwnd, x, y).HasValue Then Throw New ArgumentException("Invalid game mouse target.")
                Dim point = New IntPtr((CLng(x) And &HFFFFL) Or ((CLng(y) And &HFFFFL) << 16))
                CheckBackground(hwnd, foreground)
                If Not PostDiagnosticMessage(hwnd, &H200UI, UIntPtr.Zero, point) Then Throw New Win32Exception(Marshal.GetLastWin32Error())
                Thread.Sleep(50)
                Try
                    CheckBackground(hwnd, foreground)
                    If Not PostDiagnosticMessage(hwnd, &H201UI, New UIntPtr(1UI), point) Then Throw New Win32Exception(Marshal.GetLastWin32Error())
                    For stepIndex = 1 To 15
                        Thread.Sleep(10)
                        CheckBackground(hwnd, foreground)
                    Next
                Finally
                    If Not PostDiagnosticMessage(hwnd, &H202UI, UIntPtr.Zero, point) Then Throw New Win32Exception(Marshal.GetLastWin32Error())
                End Try
                Thread.Sleep(500)
                CheckBackground(hwnd, foreground)
                SaveCapture(hwnd, stem & "_after.png", report)
                CheckBackground(hwnd, foreground)
                report.Add("Posted mouse messages accepted; foreground HWND stayed unchanged. Verify captures for client acceptance.")
            ElseIf backendInventory OrElse backendEscape OrElse backendKey OrElse backendMouse Then
                CheckBackground(hwnd, foreground)
                If backendInventory OrElse backendEscape OrElse backendKey Then
                    If backendKey AndAlso index + 2 >= args.Length Then Throw New ArgumentException("background-backend-key requires a supported key name.")
                    Dim keyName = If(backendKey, args(index + 2), If(backendEscape, "ESC", "I"))
                    If Not BotEngine.IsSupportedKeyName(keyName) Then Throw New ArgumentException("Unsupported diagnostic key name.")
                    report.Add("Production key: " & keyName & "; background short hold sampled independently from 150-250 ms.")
                    If Not BotEngine.SendKey(hwnd, keyName, 12) Then Throw New InvalidOperationException("Production background " & keyName & " request failed.")
                Else
                    If index + 3 >= args.Length Then Throw New ArgumentException("background-backend-mouse requires client x and y.")
                    Dim x = Integer.Parse(args(index + 2)), y = Integer.Parse(args(index + 3))
                    If Not BotEngine.ClickClientPoint(hwnd, x, y) Then Throw New InvalidOperationException("Production background mouse request failed.")
                End If
                For stepIndex = 1 To 50
                    Thread.Sleep(10)
                    CheckBackground(hwnd, foreground)
                Next
                SaveCapture(hwnd, stem & "_after.png", report)
                CheckBackground(hwnd, foreground)
                report.Add("Production backend request accepted; foreground HWND stayed unchanged. Verify the captures for client acceptance.")
            ElseIf background Then
                Dim synchronous = action.Contains("-send-")
                Dim scan As Long = If(action.EndsWith("-scan"), &H17L << 16, 0L)
                CheckBackground(hwnd, foreground)
                Try
                    If Not SendInventoryMessage(hwnd, &H100UI, 1L Or scan, synchronous) Then Throw New Win32Exception(Marshal.GetLastWin32Error(), "Inventory key-down message was rejected.")
                    For stepIndex = 1 To 15
                        Thread.Sleep(10)
                        CheckBackground(hwnd, foreground)
                    Next
                Finally
                    If Not SendInventoryMessage(hwnd, &H101UI, &HC0000001L Or scan, synchronous) Then Throw New Win32Exception(Marshal.GetLastWin32Error(), "Inventory key-up message was rejected.")
                End Try
                For stepIndex = 1 To 50
                    Thread.Sleep(10)
                    CheckBackground(hwnd, foreground)
                Next
                SaveCapture(hwnd, stem & "_after.png", report)
                CheckBackground(hwnd, foreground)
                report.Add("Inventory down/up messages accepted; game remained background and foreground HWND stayed unchanged.")
                report.Add("This proves message delivery only. Inspect captures for inventory acceptance before choosing a production backend.")
            ElseIf action <> "capture" Then
                Dim key = If(action = "inventory", "I", "ESC")
                Dim sent = BotEngine.SendKey(hwnd, key, 150)
                report.Add(key & " key down/up accepted by input backend: " & sent.ToString())
                If Not sent Then Throw New InvalidOperationException("The input backend rejected the key.")
                Thread.Sleep(500)
                SaveCapture(hwnd, stem & "_after.png", report)
            End If
            report.Add("Compare the captures to confirm game acceptance; backend success alone does not prove it.")
            Return 0
        Catch ex As Exception
            report.Add("FAILED: " & ex.Message)
            Return 2
        Finally
            WindowsInput.ReleaseAll()
            File.WriteAllLines(stem & ".txt", report)
            For Each line In report
                Console.WriteLine(line)
            Next
            Console.WriteLine("Report: " & stem & ".txt")
        End Try
    End Function
    Private Shared Sub SaveCapture(hwnd As IntPtr, file As String, report As List(Of String))
        Using frame = BotEngine.CaptureClient(hwnd)
            If frame Is Nothing Then
                report.Add("Capture unavailable: " & file)
                Return
            End If
            frame.Save(file, ImageFormat.Png)
            report.Add("Capture: " & file)
        End Using
    End Sub
End Class
