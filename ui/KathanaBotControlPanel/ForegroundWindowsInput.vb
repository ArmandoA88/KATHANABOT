Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading
Imports System.Drawing
Imports System.Diagnostics

Public Structure ForegroundInputEvent
    Public Keyboard As Boolean
    Public Key As UShort
    Public Scan As UShort
    Public Flags As UInteger
    Public X As Integer
    Public Y As Integer
    Public Data As UInteger
    Public Extra As UIntPtr
End Structure

Public MustInherit Class InputPlatform
    Public MustOverride Function Foreground() As IntPtr
    Public MustOverride Function ProcessId(hwnd As IntPtr) As UInteger
    Public MustOverride Function Valid(hwnd As IntPtr) As Boolean
    Public MustOverride Function Activate(hwnd As IntPtr) As Boolean
    Public Overridable Function RestoreGameForeground(hwnd As IntPtr, expectedPid As UInteger) As Boolean
        If hwnd = IntPtr.Zero OrElse expectedPid = 0 OrElse ProcessId(hwnd) <> expectedPid OrElse Not Valid(hwnd) Then Return False
        If Foreground() <> hwnd AndAlso Not Activate(hwnd) Then Return False
        Return Valid(hwnd) AndAlso ProcessId(hwnd) = expectedPid AndAlso Foreground() = hwnd
    End Function
    Public MustOverride Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As System.Drawing.Point?
    Public MustOverride Function CursorOnTarget(hwnd As IntPtr) As Boolean
    Public MustOverride Function Desktop() As Rectangle
    Public MustOverride Function Send(value As ForegroundInputEvent) As Boolean

    ' ---- Background mode (borrowed focus) hooks. The defaults keep platforms that predate it unchanged. ----
    ' Monotonic clock used for linger/backoff decisions.
    Public Overridable Function NowMs() As Long
        Return Environment.TickCount64
    End Function
    ' Remembers where the game sits in the window stack before it is brought forward.
    Public Overridable Function CaptureZOrder(hwnd As IntPtr) As ZOrderAnchor
        Return Nothing
    End Function
    ' Puts the window back where CaptureZOrder found it without activating it.
    Public Overridable Function RestoreZOrder(hwnd As IntPtr, anchor As ZOrderAnchor) As Boolean
        Return True
    End Function
    ' Returns focus to the window the user was working in (any application, not only the game).
    Public Overridable Function ActivateOther(hwnd As IntPtr) As Boolean
        Return hwnd <> IntPtr.Zero AndAlso (Foreground() = hwnd OrElse Activate(hwnd))
    End Function
    ' True while the window is still an ordinary visible, un-minimized top-level window.
    Public Overridable Function CanRestore(hwnd As IntPtr) As Boolean
        Return hwnd <> IntPtr.Zero
    End Function
    ' A reason when taking the keyboard now would disturb the user: the taskbar/Start/Alt+Tab, a locked
    ' screen or, optionally, a full-screen app or presentation. Empty when it is fine to borrow.
    Public Overridable Function BorrowBlocker(foreground As IntPtr, includeFullScreen As Boolean) As String
        Return ""
    End Function
    ' Milliseconds since the user last used the keyboard or a mouse button, ignoring the bot's own input.
    Public Overridable Function UserIdleMs() As Long
        Return Long.MaxValue
    End Function
    ' Milliseconds since the user last pressed a mouse button.
    Public Overridable Function UserButtonIdleMs() As Long
        Return Long.MaxValue
    End Function
    ' Takes one sample of user activity; called by the 20 ms watchdog while Background mode is on.
    Public Overridable Sub SampleUserActivity(botHeldKeys As IReadOnlyCollection(Of Integer))
    End Sub
    ' The user changed the foreground window themselves during a borrow; treat it as activity.
    Public Overridable Sub NoteUserActivity()
    End Sub
End Class

Public NotInheritable Class ForegroundWindowsInput
    Implements IWindowsInput
    Private ReadOnly platform As InputPlatform
    Private ReadOnly actionDelay As Action(Of Integer)
    Private ReadOnly allowActivation As Boolean
    Private ReadOnly target As New AsyncLocal(Of Binding)
    Private ReadOnly gate As New Object
    Private ReadOnly held As New Dictionary(Of String, HeldInput)
    Private Class Binding
        Public Hwnd As IntPtr
        Public Pid As UInteger
    End Class
    Private Class HeldInput
        Public Owner As Binding
        Public Release As ForegroundInputEvent
        Public ReleaseRequested As Boolean
    End Class

    ' ---- Background mode: borrow keyboard focus for a key press, then give it back ----
    ' Locking: leaseLock (focus hand-offs, may call window APIs) is taken before gate (key state, short).
    ' The UI thread never waits on leaseLock for long: a hand-off can need the UI thread to respond.
    Private Const RefusedRetryMs As Long = 750
    Private Const LeaseLockWaitMs As Integer = 150
    Private Const HeldLeaseCapMs As Long = 120000
    Private Const UserEventToleranceMs As Long = 20
    Private ReadOnly leaseLock As New Object
    Private borrow As FocusBorrowSettings = FocusBorrowSettings.Disabled
    Private lease As FocusLease
    Private nextBorrowAttemptMs As Long
    Private anchorUnsafe As Boolean
    Private borrowedCount As Long
    Private waitedForUserCount As Long
    Private pausedCount As Long
    Private refusedCount As Long
    Private takenByUserCount As Long
    Private lastBorrowNote As String = ""
    Private Class FocusLease
        Public Owner As Binding
        Public Previous As IntPtr
        Public Anchor As ZOrderAnchor
        Public AcquiredMs As Long
        Public LastActivityMs As Long
        ' Set by Stop/F12/shutdown; belongs to this borrow only, so it can never cut a later one short.
        Public EndRequested As Boolean
    End Class

    Public Sub New(backend As InputPlatform, Optional delay As Action(Of Integer) = Nothing, Optional allowActivation As Boolean = True)
        platform = backend
        actionDelay = If(delay, New Action(Of Integer)(AddressOf Thread.Sleep))
        Me.allowActivation = allowActivation
    End Sub
    Public Shared Function NextActionDelayMs() As Integer
        Return Random.Shared.Next(5, 16)
    End Function
    Public Sub BindTarget(hwnd As IntPtr)
        target.Value = New Binding With {.Hwnd = hwnd, .Pid = platform.ProcessId(hwnd)}
    End Sub
    ' The captured window still exists, is not minimized and is the same process.
    Private Function LiveTarget(owner As Binding) As Boolean
        Return owner IsNot Nothing AndAlso owner.Hwnd <> IntPtr.Zero AndAlso owner.Pid <> 0 AndAlso
            platform.Valid(owner.Hwnd) AndAlso platform.ProcessId(owner.Hwnd) = owner.Pid
    End Function
    Private Function Focused(owner As Binding) As Boolean
        Return LiveTarget(owner) AndAlso platform.Foreground() = owner.Hwnd
    End Function
    Public Function IsTargetForeground(hwnd As IntPtr) As Boolean
        Return target.Value IsNot Nothing AndAlso target.Value.Hwnd = hwnd AndAlso Focused(target.Value)
    End Function
    Public Function IsWindowForeground(hwnd As IntPtr) As Boolean
        Return Focused(New Binding With {.Hwnd = hwnd, .Pid = platform.ProcessId(hwnd)})
    End Function
    Public Function Activate(hwnd As IntPtr) As Boolean Implements IWindowsInput.Activate
        BindTarget(hwnd)
        If hwnd = IntPtr.Zero Then Return False
        If BorrowEnabled Then
            ' Focus is borrowed when a key is really pressed, so a request that ends up sending nothing
            ' (paused chat, blocked key) never takes the keyboard from the user.
            Return LiveTarget(target.Value)
        End If
        If allowActivation AndAlso platform.Foreground() <> hwnd Then platform.Activate(hwnd)
        Return Focused(target.Value)
    End Function
    ' Eligibility to receive a key now. Foreground-only unless Background mode can borrow the keyboard.
    Public Function CanReceiveInput(hwnd As IntPtr) As Boolean
        Dim owner As New Binding With {.Hwnd = hwnd, .Pid = platform.ProcessId(hwnd)}
        If Focused(owner) Then Return True
        If Not BorrowEnabled OrElse Not LiveTarget(owner) Then Return False
        ' A key that is already down cannot be continued once the keyboard has moved elsewhere.
        SyncLock gate
            Return Not held.Values.Any(Function(item) item.Owner.Hwnd = hwnd)
        End SyncLock
    End Function
    ' Only the running-game supervisor calls this. Individual input requests retain
    ' their strict foreground guard and cannot restore an arbitrary active window.
    Public Function TryRestoreGameForeground(hwnd As IntPtr, expectedPid As UInteger) As Boolean
        If hwnd = IntPtr.Zero OrElse expectedPid = 0 OrElse platform.ProcessId(hwnd) <> expectedPid Then Return False
        ReleaseUnfocused()
        If Not platform.RestoreGameForeground(hwnd, expectedPid) Then Return False
        Dim owner As New Binding With {.Hwnd = hwnd, .Pid = expectedPid}
        If Not Focused(owner) Then Return False
        ' A release blocked while another window was active must stay pending and
        ' retry once the verified game has regained focus, before later game input.
        ReleaseUnfocused()
        If Not Focused(owner) Then Return False
        target.Value = owner
        Return True
    End Function
    Private Function Emit(value As ForegroundInputEvent) As Boolean
        If platform.Send(value) Then Return True
        RuntimeJournal.Record("Input failure", "SendInput was blocked or failed; no message fallback was used.")
        Return False
    End Function
    Private Sub SendKeyEvent(key As UShort, scan As UShort, flags As UInteger, extra As UIntPtr, Optional afterEmitted As Action = Nothing)
        ' F12 is the user's global stop key. The production backend cannot generate
        ' its own stop press; owned key-up cleanup and injected test backends remain valid.
        If (flags And (2UI Or 4UI)) = 0 AndAlso key = CUShort(Keys.F12) AndAlso
            WindowsInput.IsProductionForegroundInput AndAlso Object.ReferenceEquals(Me, WindowsInput.Current) Then
            Throw New InvalidOperationException("F12 is reserved for stopping automation; its automated press was skipped.")
        End If
        ' Add pacing before down events, outside the release lock. Recheck focus afterward.
        If (flags And 2UI) = 0 Then actionDelay(NextActionDelayMs())
        ' Background mode takes the keyboard just before a key goes out, outside the send lock. Text
        ' (Unicode) never borrows: typing needs a chat box the user opened, not a hand-off.
        If (flags And (2UI Or 4UI)) = 0 AndAlso BorrowEnabled Then BorrowFocusForKey(target.Value)
        SyncLock gate
            Dim id = $"key:{If((flags And 4UI) <> 0, "unicode", "vk")}:{If((flags And 4UI) <> 0, scan, key)}"
            Dim up = (flags And 2UI) <> 0
            If up Then
                Dim owned As HeldInput = Nothing
                If Not held.TryGetValue(id, owned) Then Return
                owned.ReleaseRequested = True
                If Not Emit(owned.Release) Then Throw New InvalidOperationException("SendInput key release failed.")
                held.Remove(id)
                TouchLease()
                Return
            End If
            If Not Focused(target.Value) Then Throw New InvalidOperationException("Selected game is not foreground; key skipped.")
            If held.ContainsKey(id) Then Throw New InvalidOperationException("Bot key is already held.")
            Dim value As New ForegroundInputEvent With {.Keyboard = True, .Key = key, .Scan = scan, .Flags = flags, .Extra = extra}
            If Not Emit(value) Then Throw New InvalidOperationException("SendInput key down failed.")
            value.Flags = flags Or 2UI
            held(id) = New HeldInput With {.Owner = target.Value, .Release = value}
            TouchLease()
            afterEmitted?.Invoke()
        End SyncLock
    End Sub

    ' Trade needs to distinguish a delivered submit key from focus changing immediately
    ' after SendInput. The receipt follows emission and owned-release registration.
    Public Function PostKeyDownWithReceipt(hwnd As IntPtr, key As UShort, scan As UShort, afterEmitted As Action) As Boolean
        BindTarget(hwnd)
        Try
            SendKeyEvent(key, scan, 0UI, UIntPtr.Zero, afterEmitted)
            Return Focused(target.Value)
        Catch ex As Exception
            RuntimeJournal.Record("Input skipped", ex.Message)
            Return False
        End Try
    End Function
    Public Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr) Implements IWindowsInput.Keyboard
        SendKeyEvent(key, scan, flags, extra)
    End Sub
    Public Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr) Implements IWindowsInput.Mouse
        If (flags And (2UI Or 8UI Or 32UI Or 128UI)) <> 0 AndAlso (flags And (4UI Or 16UI Or 64UI Or 256UI)) = 0 Then actionDelay(NextActionDelayMs())
        SyncLock gate
            Dim downFlags As UInteger() = {2UI, 8UI, 32UI, 128UI}
            Dim upFlags As UInteger() = {4UI, 16UI, 64UI, 256UI}
            For i = 0 To downFlags.Length - 1
                Dim id = $"mouse:{i}:{If(i = 3, data, 0UI)}"
                If (flags And upFlags(i)) <> 0 Then
                    Dim owned As HeldInput = Nothing
                    If held.TryGetValue(id, owned) Then
                        owned.ReleaseRequested = True
                        If Not Emit(owned.Release) Then Throw New InvalidOperationException("SendInput mouse release failed.")
                        held.Remove(id)
                    End If
                    Return
                End If
                If (flags And downFlags(i)) <> 0 Then
                    If Not Focused(target.Value) OrElse Not platform.CursorOnTarget(target.Value.Hwnd) Then Throw New InvalidOperationException("Game not foreground or cursor covered; click skipped.")
                    If held.ContainsKey(id) Then Throw New InvalidOperationException("Bot mouse button is already held.")
                    Dim value As New ForegroundInputEvent With {.Flags = downFlags(i), .Data = data, .Extra = extra}
                    If Not Emit(value) Then Throw New InvalidOperationException("SendInput mouse down failed.")
                    value.Flags = upFlags(i)
                    held(id) = New HeldInput With {.Owner = target.Value, .Release = value}
                    Return
                End If
            Next
            If Not Focused(target.Value) Then Throw New InvalidOperationException("Game not foreground; mouse input skipped.")
            If Not Emit(New ForegroundInputEvent With {.Flags = flags, .X = CInt(x), .Y = CInt(y), .Data = data, .Extra = extra}) Then Throw New InvalidOperationException("SendInput mouse event failed.")
        End SyncLock
    End Sub
    Public Function MoveCursor(x As Integer, y As Integer) As Boolean Implements IWindowsInput.MoveCursor
        SyncLock gate
            If Not Focused(target.Value) Then Return False
            Dim bounds = platform.Desktop()
            If bounds.Width <= 1 OrElse bounds.Height <= 1 OrElse Not bounds.Contains(x, y) Then Return False
            Dim nx = CInt(Math.Round((x - CDbl(bounds.Left)) * 65535 / (bounds.Width - 1)))
            Dim ny = CInt(Math.Round((y - CDbl(bounds.Top)) * 65535 / (bounds.Height - 1)))
            Return Emit(New ForegroundInputEvent With {.Flags = &HC001UI, .X = nx, .Y = ny})
        End SyncLock
    End Function
    Public Function Post(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean Implements IWindowsInput.Post
        BindTarget(hwnd)
        Try
            Select Case message
                Case &H100UI, &H101UI, &H104UI, &H105UI
                    Dim up = message = &H101UI OrElse message = &H105UI
                    Dim bits = lParam.ToInt64()
                    Dim flags As UInteger = If(up, 2UI, 0UI) Or If((bits And &H1000000L) <> 0, 1UI, 0UI)
                    SendKeyEvent(CUShort(wParam.ToInt64() And &HFFFFL), CUShort((bits >> 16) And &HFFL), flags, UIntPtr.Zero)
                Case &H102UI ' WM_CHAR compatibility -> UTF-16 SendInput pair
                    Dim code = CUShort(wParam.ToInt64() And &HFFFFL)
                    Try
                        SendKeyEvent(0, code, 4UI, UIntPtr.Zero)
                    Finally
                        SendKeyEvent(0, code, 6UI, UIntPtr.Zero)
                    End Try
                Case &H200UI, &H201UI, &H202UI, &H204UI, &H205UI
                    Dim up = message = &H202UI OrElse message = &H205UI
                    If Not up Then
                        Dim bits = lParam.ToInt64()
                        Dim x = CInt(bits And &HFFFFL), y = CInt((bits >> 16) And &HFFFFL)
                        If x >= 32768 Then x -= 65536
                        If y >= 32768 Then y -= 65536
                        Dim point = platform.ClientPoint(hwnd, x, y)
                        If Not point.HasValue OrElse Not MoveCursor(point.Value.X, point.Value.Y) Then Return False
                    End If
                    Select Case message
                        Case &H201UI : Mouse(2UI, 0, 0, 0, UIntPtr.Zero)
                        Case &H202UI : Mouse(4UI, 0, 0, 0, UIntPtr.Zero)
                        Case &H204UI : Mouse(8UI, 0, 0, 0, UIntPtr.Zero)
                        Case &H205UI : Mouse(16UI, 0, 0, 0, UIntPtr.Zero)
                    End Select
                Case Else
                    Return False
            End Select
            Return Focused(target.Value)
        Catch ex As Exception
            RuntimeJournal.Record("Input skipped", ex.Message)
            Return False
        End Try
    End Function
    Public Sub ReleaseAll()
        SyncLock gate
            For Each pair In held.Values
                pair.ReleaseRequested = True
            Next
        End SyncLock
        ReleaseUnfocused()
        ' Stopping hands the keyboard back at once instead of waiting out the linger.
        RequestLeaseEnd(IntPtr.Zero)
    End Sub
    Public Sub ReleaseTarget(hwnd As IntPtr)
        SyncLock gate
            For Each value In held.Values
                If value.Owner.Hwnd = hwnd Then value.ReleaseRequested = True
            Next
        End SyncLock
        ReleaseUnfocused()
        RequestLeaseEnd(hwnd)
    End Sub
    Public Sub ReleaseUnfocused()
        SyncLock gate
            For Each pair In held.ToArray()
                If pair.Value.ReleaseRequested OrElse Not Focused(pair.Value.Owner) Then
                    pair.Value.ReleaseRequested = True
                    If Emit(pair.Value.Release) Then held.Remove(pair.Key)
                End If
            Next
        End SyncLock
    End Sub

#Region "Background mode (borrowed focus)"
    Public Property FocusBorrow As FocusBorrowSettings
        Get
            Return Volatile.Read(borrow)
        End Get
        Set(value As FocusBorrowSettings)
            Dim updated = If(value, FocusBorrowSettings.Disabled)
            Volatile.Write(borrow, updated)
            If Not updated.Enabled Then RequestLeaseEnd(IntPtr.Zero)
        End Set
    End Property

    Public ReadOnly Property BorrowEnabled As Boolean
        Get
            Return Volatile.Read(borrow).Enabled
        End Get
    End Property

    ' True from just before the game is brought forward until the keyboard is handed back.
    Public ReadOnly Property BorrowActive As Boolean
        Get
            Return Volatile.Read(lease) IsNot Nothing
        End Get
    End Property

    Public Function FocusBorrowStatus() As FocusBorrowSnapshot
        Return New FocusBorrowSnapshot With {
            .Enabled = BorrowEnabled,
            .Active = Volatile.Read(lease) IsNot Nothing,
            .Borrowed = Interlocked.Read(borrowedCount),
            .WaitedForUser = Interlocked.Read(waitedForUserCount),
            .Paused = Interlocked.Read(pausedCount),
            .Refused = Interlocked.Read(refusedCount),
            .TakenByUser = Interlocked.Read(takenByUserCount),
            .LastNote = Volatile.Read(lastBorrowNote)}
    End Function

    ' Asks the watchdog to hand the keyboard back now. Never blocks, so the UI thread can call it.
    Private Sub RequestLeaseEnd(hwnd As IntPtr)
        Dim current = Volatile.Read(lease)
        If current Is Nothing Then Return
        If hwnd <> IntPtr.Zero AndAlso current.Owner.Hwnd <> hwnd Then Return
        Volatile.Write(current.EndRequested, True)
    End Sub

    Private Sub SetNote(note As String)
        Volatile.Write(lastBorrowNote, note)
    End Sub

    Private Sub TouchLease()
        Dim current = Volatile.Read(lease)
        If current IsNot Nothing Then Volatile.Write(current.LastActivityMs, platform.NowMs())
    End Sub

    Private Sub BorrowFocusForKey(owner As Binding)
        If Focused(owner) Then Return ' already in front: the user's own focus, or a borrow in progress
        Dim reason As String = Nothing
        If Not TryBorrowFocus(owner, reason) Then Throw New InvalidOperationException(reason)
    End Sub

    Private Function Refuse(now As Long, owner As Binding, anchor As ZOrderAnchor, ByRef reason As String) As Boolean
        Interlocked.Increment(refusedCount)
        nextBorrowAttemptMs = now + RefusedRetryMs
        ' A failed activation can still have raised the window; put it back where it was.
        If anchor IsNot Nothing AndAlso Not anchorUnsafe Then platform.RestoreZOrder(owner.Hwnd, anchor)
        reason = "Background mode: Windows refused to give the game focus; key skipped."
        SetNote(reason)
        Return False
    End Function

    ' Takes the keyboard for the game. Runs under leaseLock only (never the send lock): activating a
    ' window can require the previously active application, possibly this one, to respond.
    Private Function TryBorrowFocus(owner As Binding, ByRef reason As String) As Boolean
        If Not Monitor.TryEnter(leaseLock, LeaseLockWaitMs) Then
            reason = "Background mode: the focus hand-off is busy; key skipped."
            Return False
        End If
        Try
            If Focused(owner) Then Return True
            Dim settings = Volatile.Read(borrow)
            If Not settings.Enabled Then
                reason = "Selected game is not foreground; key skipped."
                Return False
            End If
            If Not LiveTarget(owner) Then
                reason = "Selected game window is unavailable (closed or minimized); key skipped."
                Return False
            End If
            Dim now = platform.NowMs()
            If now < nextBorrowAttemptMs Then
                reason = "Background mode: Windows refused the game focus a moment ago; key skipped."
                Return False
            End If
            ' A borrow for another game window must give the keyboard back before this one takes it.
            Dim existing = lease
            If existing IsNot Nothing AndAlso existing.Owner.Hwnd <> owner.Hwnd Then EndLeaseCore(existing, True)

            Dim front = platform.Foreground()
            Dim blocker = platform.BorrowBlocker(front, settings.PauseForFullScreen)
            If blocker <> "" Then
                Interlocked.Increment(pausedCount)
                reason = "Background mode paused: " & blocker & "; key skipped."
                SetNote(reason)
                Return False
            End If
            If settings.YieldToUserMs > 0 AndAlso platform.UserIdleMs() < settings.YieldToUserMs Then
                Interlocked.Increment(waitedForUserCount)
                reason = "Background mode: waiting for you to stop typing; key skipped."
                SetNote(reason)
                Return False
            End If

            Dim anchor = platform.CaptureZOrder(owner.Hwnd)
            ' Published before the game is brought forward so overlays that follow the foreground window
            ' (the in-game toggle) never flash over the user's windows during the hand-off.
            Dim pending As New FocusLease With {
                .Owner = owner, .Previous = If(front = owner.Hwnd, IntPtr.Zero, front), .Anchor = If(anchorUnsafe, Nothing, anchor),
                .AcquiredMs = now, .LastActivityMs = now}
            Volatile.Write(lease, pending)
            If Not platform.Activate(owner.Hwnd) OrElse Not Focused(owner) Then
                Volatile.Write(lease, Nothing)
                Return Refuse(now, owner, anchor, reason)
            End If
            If pending.Anchor IsNot Nothing Then
                ' Activation raised the window; put it back behind the user's windows without activating it.
                platform.RestoreZOrder(owner.Hwnd, pending.Anchor)
                If Not Focused(owner) Then
                    ' Re-stacking moved activation away on this system. Never do it again, take focus once more.
                    anchorUnsafe = True
                    pending.Anchor = Nothing
                    If Not platform.Activate(owner.Hwnd) OrElse Not Focused(owner) Then
                        Volatile.Write(lease, Nothing)
                        Return Refuse(now, owner, Nothing, reason)
                    End If
                End If
            End If
            Interlocked.Increment(borrowedCount)
            Return True
        Finally
            Monitor.Exit(leaseLock)
        End Try
    End Function

    ' Gives the keyboard back. Runs under leaseLock. restorePrevious is False when the user has just
    ' chosen where focus should be (they clicked), so it must not be moved again.
    Private Sub EndLeaseCore(current As FocusLease, restorePrevious As Boolean)
        If current Is Nothing Then Return
        Volatile.Write(lease, Nothing)
        ' Anything still held is released while the game still has the keyboard.
        SyncLock gate
            For Each pair In held.Values
                If pair.Owner.Hwnd = current.Owner.Hwnd Then pair.ReleaseRequested = True
            Next
        End SyncLock
        ReleaseUnfocused()
        If platform.Foreground() <> current.Owner.Hwnd Then Return ' someone else already has the keyboard
        If Not restorePrevious Then Return
        Dim destination = current.Previous
        If Not platform.CanRestore(destination) Then destination = IntPtr.Zero
        If destination = IntPtr.Zero AndAlso current.Anchor IsNot Nothing AndAlso platform.CanRestore(current.Anchor.Above) Then
            destination = current.Anchor.Above
        End If
        If destination = IntPtr.Zero Then Return ' nowhere sensible to return to; the game keeps the keyboard
        If platform.ActivateOther(destination) Then
            If current.Anchor IsNot Nothing AndAlso Not anchorUnsafe Then platform.RestoreZOrder(current.Owner.Hwnd, current.Anchor)
        Else
            Interlocked.Increment(refusedCount)
            SetNote("Background mode: Windows refused to hand focus back; the game kept it.")
        End If
    End Sub

    ' 20 ms watchdog: samples user activity and decides when a borrow is over. Never blocks.
    Public Sub ServiceFocusLease()
        Dim settings = Volatile.Read(borrow)
        If settings.Enabled Then platform.SampleUserActivity(HeldVirtualKeys())
        If Volatile.Read(lease) Is Nothing Then Return
        If Not Monitor.TryEnter(leaseLock) Then Return
        Try
            Dim current = lease
            If current Is Nothing Then Return
            Dim now = platform.NowMs()
            If platform.Foreground() <> current.Owner.Hwnd Then
                ' The user (or another app) took the keyboard: stand down without fighting for it.
                Volatile.Write(lease, Nothing)
                Interlocked.Increment(takenByUserCount)
                platform.NoteUserActivity()
                SetNote("Background mode: you switched windows; the bot waits for you.")
                ReleaseUnfocused()
                Return
            End If
            Dim sinceAcquired = now - current.AcquiredMs
            If Volatile.Read(current.EndRequested) Then
                EndLeaseCore(current, True)
            ElseIf platform.UserButtonIdleMs() < sinceAcquired - UserEventToleranceMs Then
                ' A click while the bot held focus: the user is steering, so focus is not moved again.
                Interlocked.Increment(takenByUserCount)
                SetNote("Background mode: you clicked; the bot waits for you.")
                EndLeaseCore(current, False)
            ElseIf settings.YieldToUserMs > 0 AndAlso platform.UserIdleMs() < sinceAcquired - UserEventToleranceMs Then
                Interlocked.Increment(waitedForUserCount)
                SetNote("Background mode: you started typing; focus handed back.")
                EndLeaseCore(current, True)
            Else
                Dim holding As Boolean
                SyncLock gate
                    holding = held.Count > 0
                End SyncLock
                Dim quietFor = now - Volatile.Read(current.LastActivityMs)
                If holding Then
                    If quietFor >= HeldLeaseCapMs Then EndLeaseCore(current, True)
                ElseIf quietFor >= settings.LingerMs Then
                    EndLeaseCore(current, True)
                End If
            End If
        Finally
            Monitor.Exit(leaseLock)
        End Try
    End Sub

    ' Blocking variant for shutdown paths that are not holding other locks.
    Public Sub EndFocusLeaseNow()
        If Not Monitor.TryEnter(leaseLock, 500) Then Return
        Try
            EndLeaseCore(lease, True)
        Finally
            Monitor.Exit(leaseLock)
        End Try
    End Sub

    Private Function HeldVirtualKeys() As IReadOnlyCollection(Of Integer)
        SyncLock gate
            If held.Count = 0 Then Return Array.Empty(Of Integer)()
            Dim keys As New List(Of Integer)
            For Each item In held.Values
                If item.Release.Keyboard AndAlso (item.Release.Flags And 4UI) = 0 Then keys.Add(item.Release.Key)
            Next
            Return keys
        End SyncLock
    End Function
#End Region
End Class

Friend NotInheritable Class NativeInputPlatform
    Inherits InputPlatform
    ' Reuse exact game-image validation only; this helper never dispatches input here.
    Private ReadOnly gameTarget As New NativeBackgroundInputPlatform()
    Private ReadOnly explicitTarget As Boolean
    Private ReadOnly useKathanaProcessNameValidation As Boolean
    Private ReadOnly targetWindow As IntPtr
    Private ReadOnly targetPid As UInteger

    Public Sub New()
    End Sub

    ' The shared foreground backend accepts Kathana installations selected by the
    ' user. Exact HWND/PID ownership is captured by ForegroundWindowsInput.Binding.
    Public Sub New(useKathanaProcessNameValidation As Boolean)
        Me.useKathanaProcessNameValidation = useKathanaProcessNameValidation
    End Sub

    ' Explicit foreground Trade binds the user's selected window for the entire run.
    ' It does not need process-image access or the background backend's installation path.
    Public Sub New(hwnd As IntPtr, expectedPid As UInteger)
        explicitTarget = True
        targetWindow = hwnd
        targetPid = expectedPid
    End Sub
    <StructLayout(LayoutKind.Sequential)>
    Private Structure KeyboardPacket
        Public Key As UShort
        Public Scan As UShort
        Public Flags As UInteger
        Public Time As UInteger
        Public Extra As UIntPtr
    End Structure
    <StructLayout(LayoutKind.Sequential)>
    Private Structure MousePacket
        Public X As Integer
        Public Y As Integer
        Public Data As UInteger
        Public Flags As UInteger
        Public Time As UInteger
        Public Extra As UIntPtr
    End Structure
    <StructLayout(LayoutKind.Explicit)>
    Private Structure PacketUnion
        <FieldOffset(0)> Public Keyboard As KeyboardPacket
        <FieldOffset(0)> Public Mouse As MousePacket
    End Structure
    <StructLayout(LayoutKind.Sequential)>
    Private Structure InputPacket
        Public Kind As UInteger
        Public Value As PacketUnion
    End Structure
    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SendInput(count As UInteger, packets As InputPacket(), size As Integer) As UInteger
    End Function
    <DllImport("user32.dll")>
    Private Shared Function WindowFromPoint(point As NativeMethods.POINT) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Private Shared Function IsWindow(hwnd As IntPtr) As Boolean
    End Function

    ' ---- Background mode (borrowed focus) native helpers: window-stack, class and idle queries only ----
    <StructLayout(LayoutKind.Sequential)>
    Private Structure LastInputInfo
        Public Size As UInteger
        Public Time As UInteger
    End Structure
    <DllImport("user32.dll")>
    Private Shared Function GetWindow(hwnd As IntPtr, command As UInteger) As IntPtr
    End Function
    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SetWindowPos(hwnd As IntPtr, insertAfter As IntPtr, x As Integer, y As Integer, cx As Integer, cy As Integer, flags As UInteger) As Boolean
    End Function
    <DllImport("user32.dll", EntryPoint:="GetWindowLongPtrW")>
    Private Shared Function GetWindowLongPtr(hwnd As IntPtr, index As Integer) As IntPtr
    End Function
    <DllImport("user32.dll", EntryPoint:="GetClassNameW", CharSet:=CharSet.Unicode)>
    Private Shared Function GetClassName(hwnd As IntPtr, text As StringBuilder, count As Integer) As Integer
    End Function
    <DllImport("user32.dll")>
    Private Shared Function IsWindowVisible(hwnd As IntPtr) As Boolean
    End Function
    <DllImport("user32.dll")>
    Private Shared Function GetAsyncKeyState(virtualKey As Integer) As Short
    End Function
    <DllImport("user32.dll")>
    Private Shared Function GetLastInputInfo(ByRef info As LastInputInfo) As Boolean
    End Function
    <DllImport("shell32.dll")>
    Private Shared Function SHQueryUserNotificationState(ByRef state As Integer) As Integer
    End Function
    Private Const GW_HWNDPREV As UInteger = 3UI
    Private Const GW_OWNER As UInteger = 4UI
    Private Const GWL_EXSTYLE As Integer = -20
    Private Const WS_EX_TOPMOST As Long = &H8L
    Private Const SWP_NOSIZE As UInteger = &H1UI
    Private Const SWP_NOMOVE As UInteger = &H2UI
    Private Const SWP_NOACTIVATE As UInteger = &H10UI
    Private Shared ReadOnly HwndTopmost As New IntPtr(-1)
    Private Shared ReadOnly HwndNoTopmost As New IntPtr(-2)
    ' Windows that belong to the shell rather than to an app the user is working in. Taking the keyboard
    ' from them would close the Start menu, the Alt+Tab switcher or the task view under the user's hands.
    Private Shared ReadOnly ShellSurfaceClasses As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland",
        "XamlExplorerHostIslandWindow", "Windows.UI.Core.CoreWindow", "MultitaskingViewFrame", "ForegroundStaging",
        "TaskSwitcherWnd", "TaskListThumbnailWnd", "#32768"}
    Private ReadOnly activity As New UserActivityTracker()
    Private lastInjectMs As Long
    Private lastCursor As NativeMethods.POINT
    Private haveCursor As Boolean

    Friend Shared Function IsShellSurfaceClass(name As String) As Boolean
        Return Not String.IsNullOrEmpty(name) AndAlso ShellSurfaceClasses.Contains(name)
    End Function
    Private Shared Function WindowClassName(hwnd As IntPtr) As String
        Dim text As New StringBuilder(128)
        Return If(GetClassName(hwnd, text, text.Capacity) > 0, text.ToString(), "")
    End Function
    Private Shared Function IsTopmost(hwnd As IntPtr) As Boolean
        Return (GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64() And WS_EX_TOPMOST) <> 0
    End Function
    Public Overrides Function Foreground() As IntPtr
        Return NativeMethods.GetForegroundWindow()
    End Function
    Public Overrides Function ProcessId(hwnd As IntPtr) As UInteger
        Dim pid As UInteger
        NativeMethods.GetWindowThreadProcessId(hwnd, pid)
        Return pid
    End Function
    Public Overrides Function Valid(hwnd As IntPtr) As Boolean
        If explicitTarget Then
            Return hwnd <> IntPtr.Zero AndAlso hwnd = targetWindow AndAlso targetPid <> 0 AndAlso
                targetPid <> CUInt(Environment.ProcessId) AndAlso IsWindow(hwnd) AndAlso Not NativeMethods.IsIconic(hwnd) AndAlso
                ProcessId(hwnd) = targetPid
        End If
        If useKathanaProcessNameValidation Then
            If hwnd = IntPtr.Zero OrElse Not IsWindow(hwnd) OrElse NativeMethods.IsIconic(hwnd) Then Return False
            Dim pid = ProcessId(hwnd)
            If pid = 0 OrElse pid = CUInt(Environment.ProcessId) OrElse pid > Integer.MaxValue Then Return False
            Try
                Using game = Process.GetProcessById(CInt(pid))
                    Return Not game.HasExited AndAlso String.Equals(game.ProcessName, "KathanaGame", StringComparison.OrdinalIgnoreCase) AndAlso
                        IsWindow(hwnd) AndAlso Not NativeMethods.IsIconic(hwnd) AndAlso ProcessId(hwnd) = pid
                End Using
            Catch
                Return False
            End Try
        End If
        Return gameTarget.Valid(hwnd)
    End Function
    Public Overrides Function Activate(hwnd As IntPtr) As Boolean
        If Not Valid(hwnd) Then Return False
        If NativeMethods.RawSetForegroundWindow(hwnd) AndAlso Foreground() = hwnd Then Return True
        Dim currentThread = NativeMethods.GetCurrentThreadId()
        Dim unusedPid As UInteger
        Dim foregroundThread = NativeMethods.GetWindowThreadProcessId(Foreground(), unusedPid)
        Dim attached = foregroundThread <> 0 AndAlso foregroundThread <> currentThread AndAlso NativeMethods.AttachThreadInput(currentThread, foregroundThread, True)
        Try
            NativeMethods.RawBringWindowToTop(hwnd)
            NativeMethods.RawSetForegroundWindow(hwnd)
        Finally
            If attached Then NativeMethods.AttachThreadInput(currentThread, foregroundThread, False)
        End Try
        Return Foreground() = hwnd
    End Function
    Private Function MatchesGameWindow(hwnd As IntPtr, expectedPid As UInteger, allowMinimized As Boolean) As Boolean
        If hwnd = IntPtr.Zero OrElse expectedPid = 0 OrElse expectedPid = CUInt(Environment.ProcessId) OrElse
            expectedPid > Integer.MaxValue OrElse Not IsWindow(hwnd) OrElse ProcessId(hwnd) <> expectedPid Then Return False
        If Not allowMinimized AndAlso NativeMethods.IsIconic(hwnd) Then Return False
        If explicitTarget AndAlso (hwnd <> targetWindow OrElse expectedPid <> targetPid) Then Return False
        Try
            Using game = Process.GetProcessById(CInt(expectedPid))
                Return Not game.HasExited AndAlso String.Equals(game.ProcessName, "KathanaGame", StringComparison.OrdinalIgnoreCase) AndAlso
                    IsWindow(hwnd) AndAlso ProcessId(hwnd) = expectedPid AndAlso (allowMinimized OrElse Not NativeMethods.IsIconic(hwnd))
            End Using
        Catch
            Return False
        End Try
    End Function
    Public Overrides Function RestoreGameForeground(hwnd As IntPtr, expectedPid As UInteger) As Boolean
        ' Verify the selected game even while minimized, before restoring any window.
        If Not MatchesGameWindow(hwnd, expectedPid, allowMinimized:=True) Then Return False
        Try
            If NativeMethods.IsIconic(hwnd) Then NativeMethods.RawShowWindow(hwnd, NativeMethods.SW_RESTORE)
            For attempt As Integer = 0 To 1
                If Not MatchesGameWindow(hwnd, expectedPid, allowMinimized:=False) Then Return False
                If Foreground() = hwnd Then Return True
                NativeMethods.RawSetForegroundWindow(hwnd)
                If Foreground() = hwnd AndAlso MatchesGameWindow(hwnd, expectedPid, allowMinimized:=False) Then Return True
                Dim currentThread = NativeMethods.GetCurrentThreadId()
                Dim unusedPid As UInteger
                Dim foregroundThread = NativeMethods.GetWindowThreadProcessId(Foreground(), unusedPid)
                Dim attached = foregroundThread <> 0 AndAlso foregroundThread <> currentThread AndAlso
                    NativeMethods.AttachThreadInput(currentThread, foregroundThread, True)
                Try
                    If Not MatchesGameWindow(hwnd, expectedPid, allowMinimized:=False) Then Return False
                    NativeMethods.RawBringWindowToTop(hwnd)
                    If Not MatchesGameWindow(hwnd, expectedPid, allowMinimized:=False) Then Return False
                    NativeMethods.RawSetForegroundWindow(hwnd)
                Finally
                    If attached Then NativeMethods.AttachThreadInput(currentThread, foregroundThread, False)
                End Try
                If Foreground() = hwnd AndAlso MatchesGameWindow(hwnd, expectedPid, allowMinimized:=False) Then Return True
            Next
        Catch
            Return False
        End Try
        Return False
    End Function
    Public Overrides Function CaptureZOrder(hwnd As IntPtr) As ZOrderAnchor
        If hwnd = IntPtr.Zero OrElse Not IsWindow(hwnd) Then Return Nothing
        Dim gamePid = ProcessId(hwnd)
        ' Skip the game's own hidden helper windows (Windows keeps them directly above their owner):
        ' the anchor is the nearest visible window of another process.
        Dim above = GetWindow(hwnd, GW_HWNDPREV)
        Dim guard = 0
        While above <> IntPtr.Zero AndAlso guard < 4096
            If IsWindowVisible(above) AndAlso GetWindow(above, GW_OWNER) <> hwnd AndAlso ProcessId(above) <> gamePid Then Exit While
            above = GetWindow(above, GW_HWNDPREV)
            guard += 1
        End While
        Return New ZOrderAnchor(above, IsTopmost(hwnd))
    End Function

    Public Overrides Function RestoreZOrder(hwnd As IntPtr, anchor As ZOrderAnchor) As Boolean
        If anchor Is Nothing OrElse hwnd = IntPtr.Zero OrElse Not IsWindow(hwnd) Then Return False
        Dim insertAfter As IntPtr
        If anchor.Above = IntPtr.Zero Then
            insertAfter = If(anchor.WasTopmost, HwndTopmost, IntPtr.Zero) ' it was above every other window
        ElseIf Not IsWindow(anchor.Above) Then
            Return False ' the neighbour is gone; leave the stack as it is
        ElseIf IsTopmost(anchor.Above) AndAlso Not anchor.WasTopmost Then
            insertAfter = HwndNoTopmost ' first window below the always-on-top band
        Else
            insertAfter = anchor.Above
        End If
        Return SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOACTIVATE)
    End Function

    Public Overrides Function CanRestore(hwnd As IntPtr) As Boolean
        Return hwnd <> IntPtr.Zero AndAlso IsWindow(hwnd) AndAlso IsWindowVisible(hwnd) AndAlso Not NativeMethods.IsIconic(hwnd)
    End Function

    ' Returns focus to whatever the user was working in, which is not necessarily the game and so is
    ' not validated as one. Same activation sequence as the game: direct, then attached to the
    ' foreground thread (Windows blocks a plain request from a background process).
    Public Overrides Function ActivateOther(hwnd As IntPtr) As Boolean
        If Not CanRestore(hwnd) Then Return False
        If Foreground() = hwnd Then Return True
        If NativeMethods.RawSetForegroundWindow(hwnd) AndAlso Foreground() = hwnd Then Return True
        Dim currentThread = NativeMethods.GetCurrentThreadId()
        Dim unusedPid As UInteger
        Dim foregroundThread = NativeMethods.GetWindowThreadProcessId(Foreground(), unusedPid)
        Dim attached = foregroundThread <> 0 AndAlso foregroundThread <> currentThread AndAlso
            NativeMethods.AttachThreadInput(currentThread, foregroundThread, True)
        Try
            NativeMethods.RawBringWindowToTop(hwnd)
            NativeMethods.RawSetForegroundWindow(hwnd)
        Finally
            If attached Then NativeMethods.AttachThreadInput(currentThread, foregroundThread, False)
        End Try
        Return Foreground() = hwnd
    End Function

    Public Overrides Function BorrowBlocker(foreground As IntPtr, includeFullScreen As Boolean) As String
        If foreground = IntPtr.Zero Then Return "the screen is locked or a secure prompt is open"
        If IsShellSurfaceClass(WindowClassName(foreground)) Then Return "the taskbar, Start menu or Alt+Tab is in use"
        Dim state As Integer
        If SHQueryUserNotificationState(state) = 0 Then
            Select Case state
                Case 1 ' QUNS_NOT_PRESENT: screen saver or locked
                    Return "the screen is locked or a screen saver is running"
                Case 2, 3 ' QUNS_BUSY, QUNS_RUNNING_D3D_FULL_SCREEN
                    If includeFullScreen Then Return "a full-screen app is in front"
                Case 4 ' QUNS_PRESENTATION_MODE
                    If includeFullScreen Then Return "presentation mode is on"
            End Select
        End If
        Return ""
    End Function

    Public Overrides Function UserIdleMs() As Long
        Return activity.IdleMs(Environment.TickCount64)
    End Function

    Public Overrides Function UserButtonIdleMs() As Long
        Return activity.ButtonIdleMs(Environment.TickCount64)
    End Function

    Public Overrides Sub NoteUserActivity()
        activity.MarkActivity(Environment.TickCount64)
    End Sub

    Public Overrides Sub SampleUserActivity(botHeldKeys As IReadOnlyCollection(Of Integer))
        Dim info As New LastInputInfo With {.Size = CUInt(Marshal.SizeOf(Of LastInputInfo)())}
        If Not GetLastInputInfo(info) Then Return
        Dim now = Environment.TickCount64
        ' GetLastInputInfo reports a 32-bit tick; modulo arithmetic keeps the difference right across wrap.
        Dim systemIdleMs = (now - CLng(info.Time)) And &HFFFFFFFFL

        Dim cursor As NativeMethods.POINT
        Dim moved = False
        If NativeMethods.GetCursorPos(cursor) Then
            moved = haveCursor AndAlso (cursor.X <> lastCursor.X OrElse cursor.Y <> lastCursor.Y)
            lastCursor = cursor
            haveCursor = True
        End If

        ' Which keys are down that the bot is not holding? Left/right modifiers and the generic
        ' Shift/Ctrl/Alt codes mirror each other, so a bot-held modifier covers all of its aliases.
        Dim keyDown = False
        If now - Interlocked.Read(lastInjectMs) > UserActivityTracker.BotInputEchoMs Then
            Dim ignored As New HashSet(Of Integer)
            For Each key In botHeldKeys
                ignored.Add(key)
                Select Case key
                    Case &H10, &HA0, &HA1 : ignored.UnionWith({&H10, &HA0, &HA1})
                    Case &H11, &HA2, &HA3 : ignored.UnionWith({&H11, &HA2, &HA3})
                    Case &H12, &HA4, &HA5 : ignored.UnionWith({&H12, &HA4, &HA5})
                End Select
            Next
            For virtualKey = &H8 To &HFE
                ' Generic modifiers duplicate the left/right codes; VK_PACKET is Unicode text injection.
                If virtualKey = &H10 OrElse virtualKey = &H11 OrElse virtualKey = &H12 OrElse virtualKey = &HE7 Then Continue For
                If ignored.Contains(virtualKey) Then Continue For
                If (GetAsyncKeyState(virtualKey) And &H8000S) <> 0 Then
                    keyDown = True
                    Exit For
                End If
            Next
        End If
        Dim buttonDown = (GetAsyncKeyState(&H1) And &H8000S) <> 0 OrElse (GetAsyncKeyState(&H2) And &H8000S) <> 0 OrElse
            (GetAsyncKeyState(&H4) And &H8000S) <> 0
        activity.Sample(now, systemIdleMs, moved, keyDown, buttonDown, Interlocked.Read(lastInjectMs))
    End Sub

    Public Overrides Function Desktop() As Rectangle
        Return System.Windows.Forms.SystemInformation.VirtualScreen
    End Function
    Public Overrides Function ClientPoint(hwnd As IntPtr, x As Integer, y As Integer) As System.Drawing.Point?
        Dim rect As NativeMethods.RECT
        If Not NativeMethods.GetClientRect(hwnd, rect) OrElse x < 0 OrElse y < 0 OrElse x >= rect.Right OrElse y >= rect.Bottom Then Return Nothing
        Dim point As New NativeMethods.POINT With {.X = x, .Y = y}
        If Not NativeMethods.ClientToScreen(hwnd, point) Then Return Nothing
        Return New System.Drawing.Point(point.X, point.Y)
    End Function
    Public Overrides Function CursorOnTarget(hwnd As IntPtr) As Boolean
        Dim point As NativeMethods.POINT
        If Not NativeMethods.GetCursorPos(point) Then Return False
        Return NativeMethods.GetAncestor(WindowFromPoint(point), 2UI) = hwnd
    End Function
    Public Overrides Function Send(value As ForegroundInputEvent) As Boolean
        ' Lets the user-activity sampler tell the bot's own input events from the user's.
        Interlocked.Exchange(lastInjectMs, Environment.TickCount64)
        Dim packet As New InputPacket With {.Kind = If(value.Keyboard, 1UI, 0UI)}
        If value.Keyboard Then
            packet.Value.Keyboard = New KeyboardPacket With {.Key = value.Key, .Scan = value.Scan, .Flags = value.Flags, .Extra = value.Extra}
        Else
            packet.Value.Mouse = New MousePacket With {.X = value.X, .Y = value.Y, .Flags = value.Flags, .Data = value.Data, .Extra = value.Extra}
        End If
        Return SendInput(1, {packet}, Marshal.SizeOf(Of InputPacket)()) = 1
    End Function
End Class
