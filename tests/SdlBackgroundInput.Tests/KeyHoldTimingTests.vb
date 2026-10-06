Friend Module KeyHoldTimingTests
    Friend Function Verify() As Integer
        Dim assertions As Integer
        Dim originalInput = WindowsInput.Current
        Dim platform As New FakeBackgroundPlatform
        Try
            For Each input As IWindowsInput In New IWindowsInput() {New SdlBackgroundWindowsInput(platform, Sub(milliseconds) Return),
                                                                  New ForegroundWindowsInput(New InertInputPlatform, Sub(milliseconds) Return)}
                WindowsInput.Current = input
                Dim backend = If(TypeOf input Is SdlBackgroundWindowsInput, "SDL", "foreground")
                For Each sample In {(Integer.MinValue, 150), (-1, 150), (0, 150), (5, 150), (12, 150),
                                    (149, 150), (150, 150), (151, 151), (200, 200), (249, 249)}
                    VerifyShortHold(sample.Item1, sample.Item2, backend, assertions)
                Next
                For Each requested In {250, 300, 5000, Integer.MaxValue}
                    Dim durations = Enumerable.Range(0, 32).Select(Function(index) WindowsInput.KeyPressDurationMs(requested)).ToArray()
                    Check(durations.All(Function(duration) duration = requested),
                          backend & " holds of " & requested.ToString() & " ms retain their configured duration", assertions)
                Next
            Next
            Check(platform.Events.Count = 0, "duration selection emits no target input", assertions)

            ' Timing selection must not invoke a backend or introduce randomness
            ' for injected test implementations other than the real input backends.
            WindowsInput.Current = New InertWindowsInput
            For Each sample In {(Integer.MinValue, 5), (-1, 5), (0, 5), (1, 5), (5, 5), (12, 12),
                                (149, 149), (150, 150), (249, 249), (250, 250), (300, 300),
                                (5000, 5000), (Integer.MaxValue, Integer.MaxValue)}
                Dim durations = Enumerable.Range(0, 32).Select(Function(index) WindowsInput.KeyPressDurationMs(sample.Item1)).ToArray()
                Check(durations.All(Function(duration) duration = sample.Item2),
                      "injected request " & sample.Item1.ToString() & " ms retains its existing minimum", assertions)
            Next
        Finally
            WindowsInput.Current = originalInput
        End Try
        Check(Object.ReferenceEquals(WindowsInput.Current, originalInput), "previous input backend is restored", assertions)
        Return assertions
    End Function

    Private Sub VerifyShortHold(requested As Integer, minimum As Integer, backend As String, ByRef assertions As Integer)
        ' Even the narrowest 249-250 ms range should vary across repeated presses.
        ' With 256 samples, a correct two-value generator's all-equal probability
        ' is 2^-255; wider ranges have an even smaller all-equal probability.
        Dim durations = Enumerable.Range(0, 256).Select(Function(index) WindowsInput.KeyPressDurationMs(requested)).ToArray()
        Check(durations.All(Function(duration) duration >= minimum AndAlso duration <= 250),
              backend & " request " & requested.ToString() & " ms stays within " & minimum.ToString() & "-250 ms", assertions)
        Check(durations.Distinct().Count() > 1,
              backend & " request " & requested.ToString() & " ms varies between presses", assertions)
    End Sub

    Private Sub Check(condition As Boolean, message As String, ByRef assertions As Integer)
        If Not condition Then Throw New Exception("Key hold timing: " & message)
        assertions += 1
    End Sub

    Private NotInheritable Class InertInputPlatform
        Inherits InputPlatform

        Public Overrides Function Foreground() As IntPtr
            Throw New Exception("Duration selection must not inspect window focus")
        End Function
        Public Overrides Function ProcessId(hwnd As IntPtr) As UInteger
            Throw New Exception("Duration selection must not inspect a target process")
        End Function
        Public Overrides Function Valid(hwnd As IntPtr) As Boolean
            Throw New Exception("Duration selection must not inspect target validity")
        End Function
        Public Overrides Function Activate(hwnd As IntPtr) As Boolean
            Throw New Exception("Duration selection must not activate a target")
        End Function
        Public Overrides Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As System.Drawing.Point?
            Throw New Exception("Duration selection must not convert target coordinates")
        End Function
        Public Overrides Function CursorOnTarget(hwnd As IntPtr) As Boolean
            Throw New Exception("Duration selection must not inspect the cursor")
        End Function
        Public Overrides Function Desktop() As System.Drawing.Rectangle
            Throw New Exception("Duration selection must not inspect desktop bounds")
        End Function
        Public Overrides Function Send(value As ForegroundInputEvent) As Boolean
            Throw New Exception("Duration selection must not send foreground input")
        End Function
    End Class

    Private NotInheritable Class InertWindowsInput
        Implements IWindowsInput

        Public Function Post(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean Implements IWindowsInput.Post
            Throw New Exception("Duration selection must not post input")
        End Function
        Public Function Activate(hwnd As IntPtr) As Boolean Implements IWindowsInput.Activate
            Throw New Exception("Duration selection must not activate a target")
        End Function
        Public Function MoveCursor(x As Integer, y As Integer) As Boolean Implements IWindowsInput.MoveCursor
            Throw New Exception("Duration selection must not move the cursor")
        End Function
        Public Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr) Implements IWindowsInput.Keyboard
            Throw New Exception("Duration selection must not send keyboard input")
        End Sub
        Public Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr) Implements IWindowsInput.Mouse
            Throw New Exception("Duration selection must not send mouse input")
        End Sub
    End Class
End Module
