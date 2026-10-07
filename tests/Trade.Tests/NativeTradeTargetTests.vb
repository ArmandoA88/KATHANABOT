Imports System.Collections.Concurrent
Imports System.Diagnostics
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Reflection
Imports System.Threading.Tasks

Friend Module NativeTradeTargetTests
    Private checks As Integer
    Private Const ChildArgument As String = "--owned-native-trade-target"

    Public Sub RunTests()
        checks = 0
        Dim originalInput = WindowsInput.Current
        Dim originalMode = WindowsInput.InputMode
        Dim originalKeyMode = WindowsInput.BackgroundKeyMode
        Dim executable = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".exe")
        Check(File.Exists(executable), "the owned external target apphost is unavailable")
        Dim start As New ProcessStartInfo(executable) With {
            .WorkingDirectory = Path.GetDirectoryName(executable), .UseShellExecute = False,
            .CreateNoWindow = True, .RedirectStandardInput = True, .RedirectStandardOutput = True,
            .RedirectStandardError = True}
        start.ArgumentList.Add(ChildArgument)
        Using child As New Process With {.StartInfo = start}
            Check(child.Start(), "the owned external target did not start")
            Try
                Dim ready = ReadReply(child).Split(" "c)
                Check(ready.Length = 3 AndAlso ready(0) = "READY", "the child returned an invalid window handshake")
                Dim hwnd As New IntPtr(Long.Parse(ready(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                Dim pid = UInteger.Parse(ready(2), CultureInfo.InvariantCulture)
                Check(hwnd <> IntPtr.Zero AndAlso pid = CUInt(child.Id) AndAlso pid <> CUInt(Environment.ProcessId),
                      "the target is not the exact owned external process")

                Dim bound = CreatePlatform(hwnd, pid)
                Dim background = CreatePlatform()
                Dim processNamed = CreatePlatform(True)
                Dim supervisor As New ForegroundWindowsInput(processNamed, Sub(milliseconds) Return, allowActivation:=False)
                Check(bound.ProcessId(hwnd) = pid, "the native window does not belong to the handshake PID")
                Check(bound.Valid(hwnd), "explicit foreground Trade rejected a restored target outside the installed game path")
                Check(Not background.Valid(hwnd), "the parameterless background platform accepted the alternate executable path")
                Check(processNamed.ProcessId(hwnd) = pid, "process-name validation changed the owned window's exact PID")
                Check(Not processNamed.Valid(hwnd), "production foreground validation accepted a non-Kathana external process")
                Check(Not CreatePlatform(False).Valid(hwnd), "disabling process-name validation weakened the retained exact-image validation")
                Check(Not processNamed.Valid(IntPtr.Zero), "process-name validation accepted a zero HWND")
                Check(Not processNamed.Valid(New IntPtr(-1)), "process-name validation accepted an invalid/stale HWND")
                Dim previousForeground = processNamed.Foreground()
                Check(Not processNamed.RestoreGameForeground(hwnd, pid), "the focus supervisor accepted a non-Kathana external process")
                Check(Not supervisor.TryRestoreGameForeground(hwnd, pid), "the foreground supervisor accepted a non-Kathana external process")
                Check(Not WindowsInput.TryRestoreGameForeground(hwnd, pid), "the shared supervisor accepted a non-Kathana external process")
                Check(processNamed.Foreground() = previousForeground, "negative supervisor validation changed foreground ownership")
                Check(Not processNamed.RestoreGameForeground(hwnd, pid + 1UI), "the focus supervisor accepted a mismatched selected PID")
                Check(Not processNamed.RestoreGameForeground(hwnd, 0UI), "the focus supervisor accepted a zero selected PID")
                Check(Not processNamed.RestoreGameForeground(IntPtr.Zero, pid), "the focus supervisor accepted a zero HWND")
                Check(Not processNamed.RestoreGameForeground(New IntPtr(-1), pid), "the focus supervisor accepted a stale HWND")
                Check(Not bound.Valid(IntPtr.Zero), "the explicit platform accepted a zero HWND")
                Check(Not bound.Valid(New IntPtr(-1)), "the explicit platform accepted an unrelated invalid HWND")
                Check(Not CreatePlatform(hwnd, pid + 1UI).Valid(hwnd), "a mismatched expected PID was accepted")
                Check(Not CreatePlatform(hwnd, 0UI).Valid(hwnd), "a zero expected PID was accepted")
                Check(Not CreatePlatform(IntPtr.Zero, pid).Valid(hwnd), "a zero bound HWND accepted the live target")
                Check(Not CreatePlatform(New IntPtr(-1), pid).Valid(New IntPtr(-1)), "a stale/non-window bound handle was accepted")

                Using ownWindow As New OwnedTargetForm()
                    Dim ownHwnd = ownWindow.Handle
                    Check(bound.ProcessId(ownHwnd) = CUInt(Environment.ProcessId), "the own-process negative fixture is not owned")
                    Check(Not CreatePlatform(ownHwnd, CUInt(Environment.ProcessId)).Valid(ownHwnd), "explicit Trade accepted a window in the bot's own process")
                    Check(Not processNamed.Valid(ownHwnd), "production foreground validation accepted the bot's own process")
                    Check(Not processNamed.RestoreGameForeground(ownHwnd, CUInt(Environment.ProcessId)), "the focus supervisor accepted the bot's own window")
                    Check(Not bound.Valid(ownHwnd), "a different live HWND was accepted by an existing binding")
                    Check(Not CreatePlatform(ownHwnd, pid).Valid(ownHwnd), "a bound live HWND with another process's PID was accepted")
                End Using

                Command(child, "MINIMIZE")
                Check(Not bound.Valid(hwnd), "a minimized owned external target remained valid")
                Check(Not background.Valid(hwnd), "a minimized external target became valid for the background platform")
                Check(Not processNamed.Valid(hwnd), "process-name validation accepted a minimized external window")
                Check(IsMinimized(hwnd), "the owned negative fixture was not minimized")
                Check(Not processNamed.RestoreGameForeground(hwnd, pid), "the focus supervisor restored a minimized non-Kathana window")
                Check(Not supervisor.TryRestoreGameForeground(hwnd, pid), "the foreground supervisor restored a minimized non-Kathana window")
                Check(IsMinimized(hwnd), "negative focus validation changed a non-Kathana window's minimized state")
                Command(child, "RESTORE")
                Check(bound.Valid(hwnd) AndAlso bound.ProcessId(hwnd) = pid, "restoring the exact owned target did not recover explicit validation")
                Check(Not background.Valid(hwnd), "restoring the alternate executable weakened background path validation")
                Check(Not processNamed.Valid(hwnd), "restoring a non-Kathana external window bypassed process-name validation")
                Command(child, "CLOSE")
                Check(child.WaitForExit(5000), "the child did not exit after closing its owned window")
                Check(child.ExitCode = 0, "the child fixture failed while closing")
                Check(Not bound.Valid(hwnd), "a closed owned target remained valid")
                Check(Not processNamed.Valid(hwnd), "process-name validation accepted a closed owned target")
                Check(Not processNamed.RestoreGameForeground(hwnd, pid), "the focus supervisor accepted a closed HWND/PID")
                Check(bound.ProcessId(hwnd) = 0UI, "a closed target still reports a live window PID")
            Finally
                If Not child.HasExited Then
                    Try
                        child.StandardInput.WriteLine("CLOSE")
                        child.StandardInput.Flush()
                    Catch ex As IOException
                    End Try
                    If Not child.WaitForExit(2000) Then
                        child.Kill(entireProcessTree:=True)
                        Check(child.WaitForExit(5000), "the owned fixture could not be cleaned up")
                    End If
                End If
            End Try
        End Using
        Check(Object.ReferenceEquals(WindowsInput.Current, originalInput) AndAlso WindowsInput.InputMode = originalMode AndAlso
              WindowsInput.BackgroundKeyMode = originalKeyMode, "native validation changed the existing combat input backend")
        Console.WriteLine($"PASS: {checks} offline native foreground target assertions: explicit Trade HWND/PID, alternate-path acceptance, exact-image and production process-name rejection, zero/stale/self-process/minimized/closed guards; no activation or input calls.")
    End Sub

    Private Sub Check(condition As Boolean, reason As String)
        checks += 1
        If Not condition Then Throw New InvalidOperationException("Native Trade target test: " & reason)
    End Sub

    Private Function CreatePlatform(ParamArray arguments As Object()) As InputPlatform
        Dim nativeType = GetType(ForegroundWindowsInput).Assembly.GetType("KathanaBotControlPanel.NativeInputPlatform", throwOnError:=True)
        Return DirectCast(Activator.CreateInstance(nativeType, BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic,
            binder:=Nothing, args:=arguments, culture:=CultureInfo.InvariantCulture), InputPlatform)
    End Function

    Private Function IsMinimized(hwnd As IntPtr) As Boolean
        Dim nativeType = GetType(ForegroundWindowsInput).Assembly.GetType("KathanaBotControlPanel.NativeMethods", throwOnError:=True)
        Return CBool(nativeType.GetMethod("IsIconic", BindingFlags.Static Or BindingFlags.Public Or BindingFlags.NonPublic).Invoke(Nothing, {hwnd}))
    End Function

    Private Function ReadReply(child As Process) As String
        Dim line = child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult()
        If line Is Nothing Then Throw New InvalidOperationException("Native Trade target test: child exited without its protocol reply.")
        Return line
    End Function

    Private Sub Command(child As Process, value As String)
        child.StandardInput.WriteLine(value)
        child.StandardInput.Flush()
        Check(ReadReply(child) = "ACK " & value, "the child did not acknowledge " & value)
    End Sub

    Public Sub RunOwnedTarget()
        ' This branch runs before the main suite and never constructs the bot UI,
        ' accesses saved profiles, activates windows, or emits keyboard input.
        Dim commands As New ConcurrentQueue(Of String)()
        Dim deadline = Stopwatch.StartNew()
        Using window As New OwnedTargetForm(), timer As New System.Windows.Forms.Timer With {.Interval = 25}
            AddHandler window.Shown,
                Sub()
                    Console.WriteLine("READY " & window.Handle.ToInt64().ToString("X", CultureInfo.InvariantCulture) & " " &
                        Environment.ProcessId.ToString(CultureInfo.InvariantCulture))
                    Console.Out.Flush()
                    Dim reader = Task.Run(
                        Sub()
                            Try
                                While True
                                    Dim commandLine = Console.In.ReadLine()
                                    commands.Enqueue(If(commandLine, "CLOSE"))
                                    If commandLine Is Nothing OrElse commandLine = "CLOSE" Then Return
                                End While
                            Catch ex As IOException
                                commands.Enqueue("CLOSE")
                            End Try
                        End Sub)
                    timer.Start()
                End Sub
            AddHandler timer.Tick,
                Sub()
                    If deadline.Elapsed > TimeSpan.FromSeconds(30) Then
                        Console.WriteLine("ERROR fixture timed out")
                        Console.Out.Flush()
                        window.Close()
                        Return
                    End If
                    Dim value As String = Nothing
                    If Not commands.TryDequeue(value) Then Return
                    Select Case value
                        Case "MINIMIZE"
                            window.WindowState = FormWindowState.Minimized
                        Case "RESTORE"
                            window.WindowState = FormWindowState.Normal
                        Case "CLOSE"
                            Console.WriteLine("ACK CLOSE")
                            Console.Out.Flush()
                            window.Close()
                            Return
                        Case Else
                            Console.WriteLine("ERROR unexpected command")
                            Console.Out.Flush()
                            window.Close()
                            Return
                    End Select
                    Console.WriteLine("ACK " & value)
                    Console.Out.Flush()
                End Sub
            Application.Run(window)
            timer.Stop()
        End Using
    End Sub

    Private NotInheritable Class OwnedTargetForm
        Inherits Form

        Public Sub New()
            Text = "Owned offline native Trade target"
            ShowInTaskbar = False
            StartPosition = FormStartPosition.Manual
            Location = New Point(-30000, -30000)
            Size = New Size(160, 100)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Dim parameters = MyBase.CreateParams
                parameters.ExStyle = parameters.ExStyle Or &H8000000 ' WS_EX_NOACTIVATE
                Return parameters
            End Get
        End Property
    End Class
End Module
