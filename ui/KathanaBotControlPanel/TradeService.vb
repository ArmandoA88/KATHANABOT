Imports System.Text.RegularExpressions
Imports System.Threading

Public Class TradeRecipient
    Public Property Selected As Boolean = True
    Public Property CharacterName As String = ""
    Public Property Items As String = ""
    Public Property Message As String = ""
End Class

Public Class TradeSettings
    Public Property ItemSearch As String = ""
    Public Property DiscordText As String = ""
    Public Property DiscordChannelUrl As String = "https://discord.com/channels/1483326612538654813/1556796091855278112"
    Public Property DiscordImportMessageCount As Integer = 10000
    Public Property EncryptedDiscordBotToken As String = ""
    Public Property DiscordAutoImport As Boolean = False
    Public Property WantedItems As String = "" ' Legacy profile field; AI extraction no longer requires a filter.
    Public Property ExtractedListings As New List(Of TradeListing)()
    Public Property SelectedItemKeys As New List(Of String)()
    Public Property Buying As Boolean = True
    Public Property DelaySeconds As Integer = 5
    Public Property MessageTemplate As String = "Hi! I want to buy {items}. Can you offer a good price?"
    Public Property Recipients As New List(Of TradeRecipient)()
    Public Property PriceOffers As New List(Of TradePriceOffer)()
End Class

Public NotInheritable Class TradeWhisperSubmittedException
    Inherits InvalidOperationException
    Public Sub New()
        MyBase.New("The whisper was submitted, but foreground focus or its Enter key release could not be confirmed. This row must stay marked sent. Stop the queue and verify the game before continuing.")
    End Sub
End Class

Public NotInheritable Class TradeService
    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function GetAsyncKeyState(key As Integer) As Short
    End Function
    Public Const BuyTemplate As String = "Hi! I want to buy {items}. Can you offer a good price?"
    Public Const SellTemplate As String = "Hi! I have {items} for sale. Are you interested? What price can you offer?"
    Private Shared ReadOnly Header As New Regex("^(?<name>.*?)\s*(?:APP|BOT)?\s*(?:\d{1,2}/\d{1,2}/\d{2,4}|\d{4}-\d{2}-\d{2}|Today\s+at|Yesterday\s+at)", RegexOptions.IgnoreCase)
    Private Shared ReadOnly Marker As New Regex("(?<![\p{L}\p{N}])(?<kind>SELL(?:ING)?|BUY(?:ING)?|WTS|WTB|S(?=\s*[=>:])|B(?=\s*[=>:]))\s*[=>:]?\s*", RegexOptions.IgnoreCase)

    Public Shared Function ValidName(name As String) As Boolean
        Return Regex.IsMatch(If(name, ""), "\A[\p{L}\p{N}_-]{1,32}\z")
    End Function

    Public Shared Function BuildWhisper(name As String, message As String) As String
        If Not ValidName(name) Then Throw New ArgumentException("Character names must be 1?32 letters, digits, underscores or hyphens. Preserve the game's exact capitalization.")
        If String.IsNullOrWhiteSpace(message) OrElse message.Any(Function(c) Char.IsControl(c)) Then Throw New ArgumentException("Enter a nonempty, single-line message.")
        Dim command = "/whisper " & name & " " & message.Trim()
        If command.Length > 240 Then Throw New ArgumentException("A whisper is longer than 240 characters. Shorten its message or split the items.")
        Return command
    End Function

    Public Shared Function ParsePosts(raw As String, itemsText As String, buying As Boolean, template As String) As List(Of TradeRecipient)
        Dim items = itemsText.Replace(vbCr, "").Split({vbLf, ","}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(item) item.Split("|"c).Select(Function(aliasText) aliasText.Trim()).Where(Function(aliasText) aliasText.Length > 0).ToArray()).
            Where(Function(aliases) aliases.Length > 0).ToList()
        If items.Count = 0 Then Throw New ArgumentException("Enter at least one item to buy or sell.")
        If String.IsNullOrWhiteSpace(template) Then Throw New ArgumentException("Enter a whisper message. Use {items} optionally to insert matching items.")
        Dim lines = If(raw, "").Replace(vbCr, "").Split(vbLf).Select(Function(line) line.Trim().Trim("*"c, "`"c)).Where(Function(line) line.Length > 0).ToList()
        Dim results As New List(Of TradeRecipient)()
        Dim byName As New Dictionary(Of String, List(Of String))(StringComparer.Ordinal)
        Dim order As New List(Of String)()
        Dim addItems As Action(Of String, IEnumerable(Of String)) =
            Sub(name, matches)
                If Not ValidName(name) Then Return
                Dim matchedItems = matches.ToList()
                If matchedItems.Count = 0 Then Return
                If Not byName.ContainsKey(name) Then
                    byName(name) = New List(Of String)()
                    order.Add(name)
                End If
                For Each item In matchedItems
                    If Not byName(name).Contains(item, StringComparer.OrdinalIgnoreCase) Then byName(name).Add(item)
                Next
            End Sub
        If lines.Count > 0 AndAlso lines.All(Function(line) ValidName(line)) Then
            For Each name In lines
                addItems(name, items.Select(Function(aliases) aliases(0)))
            Next
        Else
            Dim name As String = ""
            Dim body As New List(Of String)()
            Dim flush As Action =
                Sub()
                    Dim post = String.Join(" ", body)
                    Dim markers = Marker.Matches(post).Cast(Of Match)().ToList()
                    Dim matching As New List(Of String)()
                    For i = 0 To markers.Count - 1
                        Dim kind = markers(i).Groups("kind").Value.ToUpperInvariant()
                        Dim selling = kind.StartsWith("S", StringComparison.Ordinal) OrElse kind = "WTS"
                        If selling <> buying Then Continue For
                        Dim start = markers(i).Index + markers(i).Length
                        Dim finish = If(i + 1 < markers.Count, markers(i + 1).Index, post.Length)
                        Dim segment = Regex.Replace(post.Substring(start, finish - start), "\s+", " ")
                        For Each aliases In items
                            If aliases.Any(Function(aliasText) Regex.IsMatch(segment, "(?<![\p{L}\p{N}])" & Regex.Escape(Regex.Replace(aliasText, "\s+", " ")) & "(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)) Then matching.Add(aliases(0))
                        Next
                    Next
                    addItems(name, matching)
                End Sub
            For i = 0 To lines.Count - 1
                Dim line = lines(i).TrimStart("?"c, "?"c, " "c)
                If line = "APP" OrElse line = "BOT" OrElse line = "Online" OrElse line = "Offline" Then Continue For
                If line.StartsWith("Followed announcement delivery identity (not a verified game character): ", StringComparison.Ordinal) Then
                    flush()
                    name = ""
                    body.Clear()
                    Continue For
                End If
                Dim match = Header.Match(line)
                If match.Success Then
                    Dim headerName = match.Groups("name").Value.Trim()
                    If headerName.Length > 0 Then
                        flush()
                        name = headerName
                        body.Clear()
                    End If
                ElseIf ValidName(line) AndAlso i + 1 < lines.Count AndAlso (Header.IsMatch(lines(i + 1)) OrElse Marker.IsMatch(lines(i + 1)) OrElse lines(i + 1) = "APP" OrElse lines(i + 1) = "BOT") Then
                    flush()
                    name = line
                    body.Clear()
                Else
                    body.Add(line)
                End If
            Next
            flush()
        End If
        For Each name In order
            Dim wanted = String.Join(", ", byName(name))
            results.Add(New TradeRecipient With {.CharacterName = name, .Items = wanted, .Message = template.Replace("{items}", wanted)})
        Next
        Return results
    End Function

    ' This path sends exact Unicode characters through foreground SendInput: the older key-name sender uppercases every letter.
    Public Shared Function SendWhisper(hwnd As IntPtr, expectedPid As UInteger, name As String, message As String, cancellation As CancellationToken,
                                      Optional foregroundWindow As Func(Of IntPtr) = Nothing) As Boolean
        SyncLock WindowsInput.SequenceLock
        ' Injectable foreground lookup lets the message-sequence test run on a noninteractive desktop.
        If foregroundWindow Is Nothing Then foregroundWindow = Function() If(WindowsInput.UsesTargetedInput AndAlso WindowsInput.TargetCanReceiveInput(hwnd), hwnd, NativeMethods.GetForegroundWindow())
        Dim command = BuildWhisper(name, message)
        Dim chatOpen = False
        Dim sameWindow As Func(Of Boolean) =
            Function()
                Dim pid As UInteger
                Return NativeMethods.GetWindowThreadProcessId(hwnd, pid) <> 0 AndAlso pid = expectedPid AndAlso Not NativeMethods.IsIconic(hwnd)
            End Function
        Dim check As Action =
            Sub()
                cancellation.ThrowIfCancellationRequested()
                If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then Throw New OperationCanceledException()
                If Not sameWindow() Then Throw New InvalidOperationException("The selected game window closed, changed, or was minimized.")
                If foregroundWindow() <> hwnd Then Throw New InvalidOperationException("The game lost focus. Whispers stopped; select the game and retry the unsent row.")
            End Sub
        Try
            cancellation.ThrowIfCancellationRequested()
            If Not sameWindow() Then Throw New InvalidOperationException("The selected game window closed, changed, or was minimized.")
            Dim currentThread = NativeMethods.GetCurrentThreadId()
            Dim unusedPid As UInteger
            Dim foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), unusedPid)
            Dim attached = False
            Try
                If Not WindowsInput.UsesTargetedInput AndAlso foregroundThread <> 0 AndAlso foregroundThread <> currentThread Then attached = NativeMethods.AttachThreadInput(currentThread, foregroundThread, True)
                NativeMethods.SetForegroundWindow(hwnd)
            Finally
                If attached Then NativeMethods.AttachThreadInput(currentThread, foregroundThread, False)
            End Try
            If cancellation.WaitHandle.WaitOne(500) Then cancellation.ThrowIfCancellationRequested()
            check()
            ' Give activation and chat opening separate settling periods. A short first
            ' press while the control panel is active can be lost by the game.
            If Not BotEngine.SendKey(hwnd, "ENTER", 120, forceBackgroundPost:=True) Then Return False
            chatOpen = True
            If cancellation.WaitHandle.WaitOne(600) Then cancellation.ThrowIfCancellationRequested()
            For Each ch In command
                check()
                If Not NativeMethods.SendForegroundInputRequest(hwnd, &H102UI, New IntPtr(AscW(ch) And &HFFFF), New IntPtr(1)) Then Return False
                If cancellation.WaitHandle.WaitOne(25) Then cancellation.ThrowIfCancellationRequested()
            Next
            If cancellation.WaitHandle.WaitOne(200) Then cancellation.ThrowIfCancellationRequested()
            check()
            If Not BotEngine.SendKey(hwnd, "ENTER", 120, forceBackgroundPost:=True) Then Return False
            chatOpen = False
            ' Once submitted, report success even if Stop arrives during this settling delay.
            cancellation.WaitHandle.WaitOne(400)
            Return True
        Finally
            ' Never submit a partially typed whisper after Stop/F12 or a failed send.
            If chatOpen AndAlso sameWindow() AndAlso foregroundWindow() = hwnd Then BotEngine.SendKey(hwnd, "ESC", 30, forceBackgroundPost:=True)
        End Try
        End SyncLock
    End Function

    Public Shared Function SendForegroundWhisper(hwnd As IntPtr, expectedPid As UInteger, name As String, message As String, cancellation As CancellationToken,
                                                  Optional input As ForegroundWindowsInput = Nothing, Optional activateTarget As Boolean = True,
                                                  Optional delay As Action(Of Integer, CancellationToken) = Nothing,
                                                  Optional foregroundTimeoutMs As Integer = 10000) As Boolean
        Dim foreground = If(input, New ForegroundWindowsInput(New NativeInputPlatform(hwnd, expectedPid)))
        Dim command = BuildWhisper(name, message)
        SyncLock WindowsInput.SequenceLock
            Dim chatOpen As Boolean
            Dim sameWindow As Func(Of Boolean) =
                Function()
                    Dim pid As UInteger
                    Return hwnd <> IntPtr.Zero AndAlso expectedPid <> 0 AndAlso NativeMethods.GetWindowThreadProcessId(hwnd, pid) <> 0 AndAlso
                        pid = expectedPid AndAlso Not NativeMethods.IsIconic(hwnd)
                End Function
            Dim check As Action =
                Sub()
                    cancellation.ThrowIfCancellationRequested()
                    If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then Throw New OperationCanceledException()
                    If Not sameWindow() Then Throw New InvalidOperationException("The selected game window closed, changed, or was minimized.")
                    If Not foreground.IsWindowForeground(hwnd) Then Throw New InvalidOperationException("The game lost focus. Whispers stopped; close any unfinished game chat before retrying the unsent row.")
                End Sub
            Try
                cancellation.ThrowIfCancellationRequested()
                If Not sameWindow() Then Throw New InvalidOperationException("The selected game window closed, changed, or was minimized.")
                If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then Throw New OperationCanceledException()
                If activateTarget Then
                    ' Activation can finish asynchronously or Windows may require a user click.
                    ' No key is emitted until this exact HWND/PID is observed in the foreground.
                    foreground.Activate(hwnd)
                    Dim waited As Integer
                    Dim timeout = Math.Clamp(foregroundTimeoutMs, 0, 10000)
                    Dim deadline = Environment.TickCount64 + timeout
                    While Not foreground.IsWindowForeground(hwnd)
                        cancellation.ThrowIfCancellationRequested()
                        If (GetAsyncKeyState(CInt(Keys.F12)) And &H8000S) <> 0 Then Throw New OperationCanceledException()
                        If Not sameWindow() Then Throw New InvalidOperationException("The selected game window closed, changed, or was minimized.")
                        Dim remaining = Math.Min(CLng(timeout - waited), deadline - Environment.TickCount64)
                        If remaining <= 0 Then Throw New InvalidOperationException("The selected Full game window did not receive foreground focus. Click that game window with chat closed after starting whispers, or reselect it in Full and retry.")
                        Dim pause = CInt(Math.Min(50L, remaining))
                        PauseForegroundTrade(pause, cancellation, delay)
                        waited += pause
                    End While
                End If
                check()
                PauseForegroundTrade(500, cancellation, delay)
                check()
                If Not TapForegroundTradeKey(foreground, hwnd, Keys.Enter, 150, cancellation, delay, Sub() chatOpen = True) Then Return False
                PauseForegroundTrade(600, cancellation, delay)
                For Each ch In command
                    check()
                    If Not foreground.Post(hwnd, &H102UI, New IntPtr(AscW(ch) And &HFFFF), New IntPtr(1)) Then Return False
                    PauseForegroundTrade(25, cancellation, delay)
                Next
                PauseForegroundTrade(200, cancellation, delay)
                check()
                ' The submit Enter commits the fully typed command. Finish its owned release
                ' even if Stop arrives during that short hold, then report the row as sent.
                Dim submitted As Boolean
                Dim submitFocusLost As Boolean
                Dim submitReleased = TapForegroundTradeKey(foreground, hwnd, Keys.Enter, 150, CancellationToken.None, delay,
                    Sub()
                        chatOpen = False
                        submitted = True
                    End Sub, Sub() submitFocusLost = True)
                If Not submitReleased Then
                    If Not submitted Then Return False
                    Dim retryReleased = ReleaseForegroundTradeKey(foreground, hwnd, Keys.Enter)
                    If submitFocusLost OrElse Not retryReleased Then Throw New TradeWhisperSubmittedException()
                End If
                Try
                    PauseForegroundTrade(400, cancellation, delay)
                Catch ex As OperationCanceledException
                    ' Submission already completed; preserve the sent-row result.
                End Try
                Return True
            Finally
                ' Cleanup never activates a window or types into whichever app now has focus.
                If chatOpen AndAlso sameWindow() AndAlso foreground.IsWindowForeground(hwnd) Then
                    Try
                        TapForegroundTradeKey(foreground, hwnd, Keys.Escape, 30, CancellationToken.None, delay)
                    Catch ex As Exception
                        RuntimeJournal.Record("Trade input cleanup", "Could not close unfinished game chat.")
                    End Try
                End If
                Try
                    foreground.ReleaseTarget(hwnd)
                Catch ex As Exception
                    RuntimeJournal.Record("Trade input cleanup", "Could not release an owned Trade key.")
                End Try
            End Try
        End SyncLock
    End Function

    Private Shared Function TapForegroundTradeKey(input As ForegroundWindowsInput, hwnd As IntPtr, key As Keys, holdMs As Integer,
                                                 cancellation As CancellationToken, delay As Action(Of Integer, CancellationToken),
                                                 Optional afterDown As Action = Nothing, Optional afterFocusLoss As Action = Nothing) As Boolean
        cancellation.ThrowIfCancellationRequested()
        Dim scan = CInt(NativeMethods.MapVirtualKey(CUInt(key), 0UI))
        Dim pressed As Boolean
        Dim focused = input.PostKeyDownWithReceipt(hwnd, CUShort(key), CUShort(scan),
            Sub()
                pressed = True
                afterDown?.Invoke()
            End Sub)
        If Not pressed Then Return False
        Dim released As Boolean
        Try
            If Not focused Then
                afterFocusLoss?.Invoke()
                Return False
            End If
            PauseForegroundTrade(holdMs, cancellation, delay)
        Finally
            released = ReleaseForegroundTradeKey(input, hwnd, key)
        End Try
        Return released
    End Function

    Private Shared Function ReleaseForegroundTradeKey(input As ForegroundWindowsInput, hwnd As IntPtr, key As Keys) As Boolean
        Dim scan = CInt(NativeMethods.MapVirtualKey(CUInt(key), 0UI))
        Dim up = 1 Or (scan << 16) Or (1 << 30) Or (1 << 31)
        Return input.Post(hwnd, &H101UI, New IntPtr(CInt(key)), New IntPtr(up))
    End Function

    Private Shared Sub PauseForegroundTrade(milliseconds As Integer, cancellation As CancellationToken, delay As Action(Of Integer, CancellationToken))
        If delay Is Nothing Then
            cancellation.WaitHandle.WaitOne(milliseconds)
        Else
            delay(milliseconds, cancellation)
        End If
        cancellation.ThrowIfCancellationRequested()
    End Sub
End Class
