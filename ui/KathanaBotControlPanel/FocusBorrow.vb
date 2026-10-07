' Background mode support types.
'
' Windows delivers SendInput keystrokes only to the foreground window, so "background" operation
' cannot mean typing into a window that is not active. Background mode keeps using the same
' foreground SendInput path, but the game does not have to be kept in front: the backend borrows
' keyboard focus for the moment a key is pressed, keeps the game window where it was in the window
' stack, and hands focus back to the window the user was using.

''' <summary>Immutable Background-mode settings shared with the input backend.</summary>
Public NotInheritable Class FocusBorrowSettings
    Public Const DefaultLingerMs As Integer = 250
    Public Const DefaultYieldMs As Integer = 1000
    Public Const MaximumYieldMs As Integer = 30000

    Public Shared ReadOnly Disabled As New FocusBorrowSettings(False, 0, DefaultLingerMs, True)

    Public ReadOnly Property Enabled As Boolean
    ''' <summary>
    ''' The bot only borrows the keyboard after the user has been idle (no keys or mouse buttons) for
    ''' this long. Zero never waits, which gives the bot priority over the user's typing.
    ''' </summary>
    Public ReadOnly Property YieldToUserMs As Integer
    ''' <summary>Focus is kept this long after the last bot key so a burst of keys shares one borrow.</summary>
    Public ReadOnly Property LingerMs As Integer
    ''' <summary>Do not take the keyboard from a full-screen app or while presentation mode is on.</summary>
    Public ReadOnly Property PauseForFullScreen As Boolean

    Public Sub New(enabled As Boolean, yieldToUserMs As Integer, Optional lingerMs As Integer = DefaultLingerMs,
                   Optional pauseForFullScreen As Boolean = True)
        Me.Enabled = enabled
        Me.YieldToUserMs = Math.Clamp(yieldToUserMs, 0, MaximumYieldMs)
        Me.LingerMs = Math.Clamp(lingerMs, 0, 5000)
        Me.PauseForFullScreen = pauseForFullScreen
    End Sub
End Class

''' <summary>Where a window sat in the z-order, so it can be put back after a borrow.</summary>
Public NotInheritable Class ZOrderAnchor
    ''' <summary>
    ''' The nearest visible window above the game that belongs to another process. Hidden helper windows
    ''' (IME/input helpers) that Windows stacks directly above their owner are skipped on purpose; they
    ''' follow the game when it moves. Zero when the game was above every other visible window.
    ''' </summary>
    Public ReadOnly Property Above As IntPtr
    Public ReadOnly Property WasTopmost As Boolean

    Public Sub New(above As IntPtr, wasTopmost As Boolean)
        Me.Above = above
        Me.WasTopmost = wasTopmost
    End Sub
End Class

''' <summary>Counters and the last note for the Background-mode status line.</summary>
Public NotInheritable Class FocusBorrowSnapshot
    Public Property Enabled As Boolean
    Public Property Active As Boolean
    Public Property Borrowed As Long
    Public Property WaitedForUser As Long
    Public Property Paused As Long
    Public Property Refused As Long
    Public Property TakenByUser As Long
    Public Property LastNote As String = ""
End Class

''' <summary>
''' Separates the user's own keyboard/mouse-button activity from the bot's injected input, so Background
''' mode can wait for the user to stop typing before borrowing the keyboard. Pure logic: the native
''' platform feeds it samples.
''' </summary>
Public NotInheritable Class UserActivityTracker
    ' An OS input event this close to one of the bot's own SendInput calls is the bot's.
    Public Const BotInputEchoMs As Long = 45
    ' A key or button held down this long is stuck or a hold-to-scroll, not typing.
    Public Const StuckKeyMs As Long = 4000

    Private ReadOnly sync As New Object
    Private lastKeyMs As Long = Long.MinValue
    Private lastButtonMs As Long = Long.MinValue
    Private lastOtherMs As Long = Long.MinValue
    Private lastSeenInputAtMs As Long = Long.MinValue
    Private keysDownSinceMs As Long = -1
    Private buttonDownSinceMs As Long = -1

    ''' <param name="nowMs">Monotonic clock (Environment.TickCount64 domain).</param>
    ''' <param name="systemIdleMs">Milliseconds since the OS last saw ANY input (GetLastInputInfo).</param>
    ''' <param name="cursorMoved">The pointer moved since the previous sample (pure mouse motion is not typing).</param>
    ''' <param name="userKeyDown">A key the bot is not holding is physically down.</param>
    ''' <param name="userButtonDown">A mouse button is down.</param>
    ''' <param name="botInjectedAtMs">When the bot last called SendInput (0 when never).</param>
    Public Sub Sample(nowMs As Long, systemIdleMs As Long, cursorMoved As Boolean,
                      userKeyDown As Boolean, userButtonDown As Boolean, botInjectedAtMs As Long)
        SyncLock sync
            If userKeyDown Then
                If keysDownSinceMs < 0 Then keysDownSinceMs = nowMs
                If nowMs - keysDownSinceMs <= StuckKeyMs Then lastKeyMs = Math.Max(lastKeyMs, nowMs)
            Else
                keysDownSinceMs = -1
            End If
            If userButtonDown Then
                If buttonDownSinceMs < 0 Then buttonDownSinceMs = nowMs
                If nowMs - buttonDownSinceMs <= StuckKeyMs Then lastButtonMs = Math.Max(lastButtonMs, nowMs)
            Else
                buttonDownSinceMs = -1
            End If

            ' An OS input event nobody explains (not the bot, not pure pointer motion) is the user: a key
            ' tapped between samples, the wheel, touch.
            Dim lastInputAt = nowMs - Math.Max(0L, systemIdleMs)
            If lastInputAt > lastSeenInputAtMs Then
                lastSeenInputAtMs = lastInputAt
                Dim echoOfBot = botInjectedAtMs > 0 AndAlso Math.Abs(lastInputAt - botInjectedAtMs) <= BotInputEchoMs
                If Not echoOfBot AndAlso Not cursorMoved Then lastOtherMs = Math.Max(lastOtherMs, lastInputAt)
            End If
        End SyncLock
    End Sub

    ''' <summary>Counts an event the tracker cannot see directly (the user took the foreground themselves).</summary>
    Public Sub MarkActivity(nowMs As Long)
        SyncLock sync
            lastOtherMs = Math.Max(lastOtherMs, nowMs)
        End SyncLock
    End Sub

    ''' <summary>Milliseconds since the user's last keyboard/button activity; Long.MaxValue when none was seen.</summary>
    Public Function IdleMs(nowMs As Long) As Long
        SyncLock sync
            Return Since(nowMs, Math.Max(lastKeyMs, Math.Max(lastButtonMs, lastOtherMs)))
        End SyncLock
    End Function

    ''' <summary>Milliseconds since the user last pressed a mouse button; Long.MaxValue when none was seen.</summary>
    Public Function ButtonIdleMs(nowMs As Long) As Long
        SyncLock sync
            Return Since(nowMs, lastButtonMs)
        End SyncLock
    End Function

    Private Shared Function Since(nowMs As Long, at As Long) As Long
        If at = Long.MinValue Then Return Long.MaxValue
        Return Math.Max(0L, nowMs - at)
    End Function
End Class
