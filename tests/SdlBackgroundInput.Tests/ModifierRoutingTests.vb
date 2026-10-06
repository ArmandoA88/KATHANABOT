Imports System.Drawing

' Exercise explicit modifier identity and state through the public input API.
' The fake platform receives target messages only; these checks send no live input.
Friend Module ModifierRoutingTests
    Friend Function Verify() As Integer
        Dim assertions As Integer
        VerifyDistinctSides(&HA0, &HA1, &H2A, &H36, False, "Shift", assertions)
        VerifyDistinctSides(&HA2, &HA3, &H1D, &H1D, True, "Ctrl", assertions)
        VerifyDistinctSides(&HA4, &HA5, &H38, &H38, True, "Alt", assertions)
        VerifyMouseModifierState(&HA0, &HA1, &H10, 4L, "Shift", assertions)
        VerifyMouseModifierState(&HA2, &HA3, &H11, 8L, "Ctrl", assertions)
        VerifyCombinedMouseState(assertions)
        VerifyRightAltCleanup(assertions)
        Return assertions
    End Function

    Private Sub Check(condition As Boolean, message As String, ByRef assertions As Integer)
        If Not condition Then Throw New Exception("Modifier routing: " & message)
        assertions += 1
    End Sub

    Private Function CreateInput(ByRef platform As FakeBackgroundPlatform) As SdlBackgroundWindowsInput
        platform = New FakeBackgroundPlatform()
        Dim game = New IntPtr(789)
        platform.Pids(game) = 33
        platform.Origins(game) = New Point(100, 200)
        Dim input As New SdlBackgroundWindowsInput(platform, Sub(ms) Return)
        If Not input.Activate(game) Then Throw New Exception("Modifier test target could not bind")
        Return input
    End Function

    Private Sub VerifyDistinctSides(left As Byte, right As Byte, leftScan As Byte, rightScan As Byte,
                                    rightExtended As Boolean, label As String, ByRef assertions As Integer)
        Dim platform As FakeBackgroundPlatform = Nothing
        Dim input = CreateInput(platform)
        input.Keyboard(left, leftScan, 0UI, UIntPtr.Zero)
        input.Keyboard(right, rightScan, If(rightExtended, 1UI, 0UI), UIntPtr.Zero)
        Check(platform.Events.Count = 2, label & " sides can be held independently", assertions)
        Check(platform.Events.Select(Function(e) e.WParam).SequenceEqual({CLng(left), CLng(right)}),
              label & " explicit left/right virtual-key identity is preserved", assertions)
        Check(platform.Events.All(Function(e) e.Message = &H100UI AndAlso e.LParam = 1L),
              label & " sides use the zero-scan key-down path even with supplied scan/extended fields", assertions)

        input.Keyboard(left, leftScan, 2UI, UIntPtr.Zero)
        Check(platform.Events.Last().Message = &H101UI AndAlso platform.Events.Last().WParam = left,
              label & " left release retains its own identity", assertions)
        input.Keyboard(right, rightScan, If(rightExtended, 3UI, 2UI), UIntPtr.Zero)
        Check(platform.Events.Last().Message = &H101UI AndAlso platform.Events.Last().WParam = right,
              label & " right release retains its own identity", assertions)
        Check(platform.Events.TakeLast(2).All(Function(e) e.LParam = &HC0000001L),
              label & " releases retain transition bits and zero scan", assertions)
        Dim before = platform.Events.Count
        input.Keyboard(left, leftScan, 2UI, UIntPtr.Zero)
        input.Keyboard(right, rightScan, 2UI, UIntPtr.Zero)
        input.ReleaseAll()
        Check(platform.Events.Count = before, label & " fully released holds have no duplicate cleanup", assertions)
    End Sub

    Private Sub VerifyMouseModifierState(left As Byte, right As Byte, generic As Byte, mask As Long,
                                         label As String, ByRef assertions As Integer)
        Dim platform As FakeBackgroundPlatform = Nothing
        Dim input = CreateInput(platform)
        For Each key In {left, right, generic}
            input.Keyboard(key, 0, 0UI, UIntPtr.Zero)
            Check(input.MoveCursor(120, 225) AndAlso platform.Events.Last().WParam = mask,
                  label & " mouse state recognizes virtual key " & key.ToString(), assertions)
            input.Keyboard(key, 0, 2UI, UIntPtr.Zero)
            Check(input.MoveCursor(121, 225) AndAlso platform.Events.Last().WParam = 0L,
                  label & " released single-side hold clears mouse state", assertions)
        Next

        input.Keyboard(left, 0, 0UI, UIntPtr.Zero)
        input.Keyboard(right, 0, 0UI, UIntPtr.Zero)
        Check(input.MoveCursor(122, 225) AndAlso platform.Events.Last().WParam = mask,
              label & " two held sides contribute one mouse modifier bit", assertions)
        input.Keyboard(left, 0, 2UI, UIntPtr.Zero)
        Check(input.MoveCursor(123, 225) AndAlso platform.Events.Last().WParam = mask,
              label & " right hold remains after left release", assertions)
        input.Keyboard(right, 0, 2UI, UIntPtr.Zero)
        Check(input.MoveCursor(124, 225) AndAlso platform.Events.Last().WParam = 0L,
              label & " bit clears after both sides release", assertions)

        input.Keyboard(generic, 0, 0UI, UIntPtr.Zero)
        input.Keyboard(right, 0, 0UI, UIntPtr.Zero)
        input.Keyboard(generic, 0, 2UI, UIntPtr.Zero)
        Check(input.MoveCursor(125, 225) AndAlso platform.Events.Last().WParam = mask,
              label & " explicit right hold survives generic modifier release", assertions)
        input.ReleaseAll()
        Check(input.MoveCursor(126, 225) AndAlso platform.Events.Last().WParam = 0L,
              label & " cleanup clears remaining side", assertions)
    End Sub

    Private Sub VerifyCombinedMouseState(ByRef assertions As Integer)
        Dim platform As FakeBackgroundPlatform = Nothing
        Dim input = CreateInput(platform)
        For Each key As Byte In {CByte(&HA0), CByte(&HA1), CByte(&HA2), CByte(&HA3)}
            input.Keyboard(key, 0, 0UI, UIntPtr.Zero)
        Next
        Check(input.MoveCursor(130, 230), "combined modifier test establishes a client cursor", assertions)
        input.Mouse(2UI, 0UI, 0UI, 0UI, UIntPtr.Zero)
        Check(platform.Events.Last().Message = &H201UI AndAlso platform.Events.Last().WParam = 13L,
              "left down carries button plus Shift/Ctrl from both sides", assertions)
        input.Keyboard(&HA0, 0, 2UI, UIntPtr.Zero)
        input.Keyboard(&HA2, 0, 2UI, UIntPtr.Zero)
        Check(input.MoveCursor(131, 230) AndAlso platform.Events.Last().WParam = 13L,
              "button and right-side modifiers survive left-side releases", assertions)
        input.Keyboard(&HA1, 0, 2UI, UIntPtr.Zero)
        input.Keyboard(&HA3, 0, 2UI, UIntPtr.Zero)
        Check(input.MoveCursor(132, 230) AndAlso platform.Events.Last().WParam = 1L,
              "modifier cleanup preserves independently held mouse button", assertions)
        input.Mouse(4UI, 0UI, 0UI, 0UI, UIntPtr.Zero)
        Check(platform.Events.Last().Message = &H202UI AndAlso platform.Events.Last().WParam = 0L,
              "mouse release clears button after all modifier sides released", assertions)
    End Sub

    Private Sub VerifyRightAltCleanup(ByRef assertions As Integer)
        Dim platform As FakeBackgroundPlatform = Nothing
        Dim input = CreateInput(platform)
        input.Keyboard(&HA5, &H38, 1UI, UIntPtr.Zero)
        Check(platform.Events.Last().WParam = &HA5 AndAlso platform.Events.Last().LParam = 1L,
              "held RALT follows the verified virtual-key path without becoming generic Alt", assertions)
        platform.FailKeyUp = &HA5UI
        Dim before = platform.Events.Count
        input.ReleaseAll()
        Check(platform.Events.Count = before, "failed RALT cleanup is deferred", assertions)
        platform.FailKeyUp = 0UI
        input.Maintain()
        Check(platform.Events.Count = before + 1 AndAlso platform.Events.Last().Message = &H101UI AndAlso
              platform.Events.Last().WParam = &HA5 AndAlso platform.Events.Last().LParam = &HC0000001L,
              "RALT cleanup retries the same owned identity with zero scan", assertions)
        before = platform.Events.Count
        input.Maintain()
        input.ReleaseAll()
        Check(platform.Events.Count = before, "successful RALT cleanup is idempotent", assertions)

        Dim game = New IntPtr(789)
        Check(input.Post(game, &H104UI, New IntPtr(&HA5), New IntPtr(&H1380001L)),
              "explicit RALT system-key down is accepted", assertions)
        Check(platform.Events.Last().Message = &H104UI AndAlso platform.Events.Last().WParam = &HA5 AndAlso
              platform.Events.Last().LParam = 1L, "system-key RALT also preserves identity and strips scan bits", assertions)
        input.ReleaseTarget(game)
        Check(platform.Events.Last().Message = &H105UI AndAlso platform.Events.Last().WParam = &HA5 AndAlso
              platform.Events.Last().LParam = &HC0000001L, "target cleanup retains RALT system-key release type", assertions)
    End Sub
End Module
