Imports System.Drawing
Imports System.Reflection
Imports System.Threading

Friend Module LootAfterKillFailureTests
    Private ReadOnly Flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
    Private checks As Integer

    Public Sub Run()
        checks = 0
        Dim originalInput = WindowsInput.Current
        Dim originalMode = WindowsInput.InputMode
        Dim originalKeyboardMode = WindowsInput.BackgroundKeyMode
        Try
            For Each mode In {BackgroundKeyboardMode.PostedScanCode, BackgroundKeyboardMode.SynchronousScanCode,
                              BackgroundKeyboardMode.PostedZeroScanCode, BackgroundKeyboardMode.SynchronousZeroScanCode}
                For Each failure In {"down", "release", "cancel"}
                    VerifyConsumedDisappearance(mode, failure)
                Next
            Next
        Finally
            WindowsInput.Current = originalInput
        End Try
        Check(Object.ReferenceEquals(WindowsInput.Current, originalInput) AndAlso WindowsInput.InputMode = originalMode AndAlso
              WindowsInput.BackgroundKeyMode = originalKeyboardMode, "the fixture changed the production combat backend or keyboard mode")
        Console.WriteLine($"PASS: {checks} offline post-kill pickup failure assertions: real background backend, four delivery modes, uncertain down/release, cancellation, owned cleanup and one attempt per disappearance.")
    End Sub

    Private Sub VerifyConsumedDisappearance(mode As BackgroundKeyboardMode, failure As String)
        Dim hwnd As New IntPtr(812345)
        Dim platform As New LootBackgroundPlatform(hwnd) With {.Failure = failure}
        Dim input As New SdlBackgroundWindowsInput(platform,
            Sub(milliseconds)
            End Sub,
            Function() New Rectangle(0, 0, 1920, 1080), keyboardMode:=mode, keyboardOnly:=True)
        Dim engine As New BotEngine
        Dim config As New BotConfig With {.LootAfterKillEnabled = True, .LootAfterKillHoldMs = 300,
            .LootScannerEnabled = False, .LootPickupEnabled = False}
        Dim pickup = GetType(BotEngine).GetMethod("TryHandleLootAfterKill", Flags)
        Dim now = New DateTime(2026, 10, 6, 16, 0, 0, DateTimeKind.Utc)
        Dim invokeTick As Action(Of Boolean, Integer) =
            Sub(alive, milliseconds) pickup.Invoke(engine, {config, hwnd, alive, now.AddMilliseconds(milliseconds), True})
        Dim logs As New List(Of String)
        AddHandler engine.LogLine, Sub(line) logs.Add(line)
        Using cancellation As New CancellationTokenSource
            GetType(BotEngine).GetField("_cts", Flags).SetValue(engine, cancellation)
            If failure = "cancel" Then platform.AfterDown = Sub() cancellation.Cancel()
            WindowsInput.Current = input
            Try
                GetType(BotEngine).GetField("_lastAttackAction", Flags).SetValue(engine, now)
                invokeTick(True, 0)
                invokeTick(False, 100)
                Check(platform.Events.Where(Function(value) value.Message = &H100UI).Count() = 1,
                      mode.ToString() & " " & failure & " must attempt exactly one F down")
                Check(logs.Any(Function(line) line.Contains("F attempt failed or was cancelled", StringComparison.Ordinal)) AndAlso
                      Not logs.Any(Function(line) line.Contains("held F for", StringComparison.Ordinal)),
                      "uncertain pickup was reported as a successful held F")

                ' Permit the backend's owned key-up retry, then replay the same missing target.
                ' Neither an uncertain down nor a delivered down with a failed release may
                ' authorize another F press when the game becomes writable again.
                platform.Failure = ""
                platform.AfterDown = Nothing
                input.Maintain()
                Check(platform.Events.Any(Function(value) value.Message = &H101UI AndAlso value.Accepted),
                      "failure/cancellation did not release the original owned F")
                Dim afterCleanup = platform.Events.Count
                GetType(BotEngine).GetField("_cts", Flags).SetValue(engine, Nothing)
                invokeTick(False, 200)
                invokeTick(False, 500)
                invokeTick(False, 2500)
                Check(platform.Events.Count = afterCleanup,
                      "repeated missing-target frames retried the consumed post-kill pickup")

                ' A new attacked living target can arm a genuinely new pickup.
                GetType(BotEngine).GetField("_lastAttackAction", Flags).SetValue(engine, now.AddSeconds(3))
                invokeTick(True, 3000)
                invokeTick(False, 3100)
                Check(platform.Events.Skip(afterCleanup).Select(Function(value) value.Message).SequenceEqual({&H100UI, &H101UI}),
                      "a new attacked target did not receive one complete F pair")
                Check(platform.Events.All(Function(value) value.Hwnd = hwnd AndAlso value.Key = 70 AndAlso value.Mode = mode),
                      "pickup or cleanup changed the configured delivery method, HWND or key")
                Check(input.KeyboardOnly AndAlso input.KeyboardMode = mode AndAlso config.LootAfterKillHoldMs = 300,
                      "pickup changed the requested hold, keyboard-only restriction or backend mode")
                Dim afterNewPickup = platform.Events.Count
                input.ReleaseAll()
                invokeTick(False, 3200)
                Check(platform.Events.Count = afterNewPickup, "a completed new pickup left a held key or repeated the same disappearance")
                Check(logs.Any(Function(line) line.Contains("held F for 300ms", StringComparison.Ordinal)),
                      "successful pickup did not retain the configured hold")
            Finally
                platform.Failure = ""
                platform.AfterDown = Nothing
                input.ReleaseAll()
                GetType(BotEngine).GetField("_cts", Flags).SetValue(engine, Nothing)
                WindowsInput.Current = Nothing
            End Try
        End Using
    End Sub

    Private Sub Check(condition As Boolean, message As String)
        checks += 1
        If Not condition Then Throw New InvalidOperationException("Post-kill pickup failure test: " & message)
    End Sub

    Private NotInheritable Class LootBackgroundEvent
        Public Hwnd As IntPtr
        Public Message As UInteger
        Public Key As Long
        Public Mode As BackgroundKeyboardMode
        Public Accepted As Boolean
    End Class

    Private NotInheritable Class LootBackgroundPlatform
        Inherits BackgroundInputPlatform
        Private ReadOnly target As IntPtr
        Public ReadOnly Events As New List(Of LootBackgroundEvent)
        Public Failure As String
        Public AfterDown As Action

        Public Sub New(hwnd As IntPtr)
            target = hwnd
        End Sub

        Public Overrides Function Valid(hwnd As IntPtr) As Boolean
            Return hwnd = target
        End Function

        Public Overrides Function ProcessId(hwnd As IntPtr) As UInteger
            Return If(hwnd = target, 7513UI, 0UI)
        End Function

        Public Overrides Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As Point?
            Throw New InvalidOperationException("Post-kill pickup must not move the mouse.")
        End Function

        Public Overrides Function ScreenToClient(hwnd As IntPtr, x As Integer, y As Integer) As Point?
            Throw New InvalidOperationException("Post-kill pickup must not move the mouse.")
        End Function

        Public Overrides Function Send(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
            Throw New InvalidOperationException("Post-kill pickup must use the selected background keyboard method.")
        End Function

        Public Overrides Function SendKeyboard(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr,
                                               mode As BackgroundKeyboardMode) As Boolean
            Dim accepted = Not ((message = &H100UI AndAlso Failure = "down") OrElse (message = &H101UI AndAlso Failure = "release"))
            Events.Add(New LootBackgroundEvent With {.Hwnd = hwnd, .Message = message, .Key = wParam.ToInt64(), .Mode = mode, .Accepted = accepted})
            If message = &H100UI Then AfterDown?.Invoke()
            Return accepted
        End Function
    End Class
End Module
