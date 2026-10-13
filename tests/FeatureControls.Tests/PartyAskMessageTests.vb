Imports System.Reflection
Imports System.Text
Imports System.Text.Json

Friend Module PartyAskMessageTests
    Private checks As Integer
    Private ReadOnly InstanceFlags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic

    Public Sub Run()
        checks = 0
        TestParsingAndLargeCollections()
        TestAllChannelPrefix()
        TestShuffledCycles()
        TestRetriesAndLiveEdits()
        TestPersistenceAndButton()
        TestHiddenModeEditorAndMigration()
        TestLockedEngineSubmission()
        TestEngineSubmission()
        Console.WriteLine($"PASS: {checks} party-message assertions: comma parsing, all-channel prefixes, large collections, complete shuffled cycles, no adjacent repeats, failed-send retries, live edits, single-message compatibility and settings/profile persistence (recorded input only).")
    End Sub

    Private Sub Check(condition As Boolean, reason As String)
        checks += 1
        If Not condition Then Throw New InvalidOperationException("Party-message test: " & reason)
    End Sub

    Private Sub TestParsingAndLargeCollections()
        Dim parsed = PartyAskMessageCollection.Parse(" , Add please ,add PLEASE,, Any room? , " & vbCrLf & "Invite me! , ")
        Check(parsed.SequenceEqual({"/all Add please", "/all Any room?", "/all Invite me!"}), "blank entries must be ignored and duplicates removed without changing the first spelling")
        For Each empty In {Nothing, "", " , , "}
            Check(PartyAskMessageCollection.Parse(empty).SequenceEqual({"/all add"}), "empty collections must use the all-channel add default")
        Next
        Check(PartyAskMessageCollection.Parse("¿Hay espacio?, パーティーに入れますか？").Count = 2, "Unicode phrases must be preserved")
        Dim large = String.Join(",", Enumerable.Range(1, 5000).Select(Function(index) "Party phrase " & index))
        Check(large.Length > 32767 AndAlso PartyAskMessageCollection.Parse(large).Count = 5000, "collections must not impose a phrase-count or old textbox-length limit")
        Dim selector As New PartyAskMessageCollection(New Random(31))
        Dim sent As New HashSet(Of String)
        For index = 1 To 5000
            sent.Add(selector.Peek(large))
            selector.MarkSent()
        Next
        Check(sent.Count = 5000, "every phrase in a large collection must be used before reshuffling")
    End Sub

    Private Sub TestAllChannelPrefix()
        Dim parsed = PartyAskMessageCollection.Parse("one, /all two, /ALL three, /all    ONE, /all /all four, /all, , /alliance")
        Check(parsed.SequenceEqual({"/all one", "/all two", "/all three", "/all four", "/all /alliance"}), "every phrase must have one canonical prefix, with exact-token matching and mixed-prefix duplicates removed")
        Check(PartyAskMessageCollection.Parse("/all" & vbTab & "Any room?").Single() = "/all Any room?", "whitespace after an existing command must be normalized")
        Check(PartyAskMessageCollection.Parse("/all, /ALL  ").Single() = "/all add", "bare channel commands must not be sent as empty messages")
        Check(New BotConfig().PartyAskText = "add" AndAlso Not New BotConfig().PartyAskCollectionEnabled, "new configs must default to regular requests with the collection locked")
        Using stream = GetType(Form1).Assembly.GetManifestResourceStream("KathanaBotControlPanel.DefaultUserSettings.json")
            Using defaults = JsonDocument.Parse(stream)
                Dim full = defaults.RootElement.GetProperty("Full")
                Check(full.GetProperty("AskForPartyText").GetString() = "add" AndAlso full.GetProperty("AskForPartyCollectionText").GetString() = "/all add", "embedded Full settings must keep separate regular and hidden defaults")
            End Using
        End Using
    End Sub

    Private Sub TestShuffledCycles()
        Dim phrases = {"one", "two", "three", "four", "five", "six", "seven", "eight"}
        Dim raw = String.Join(",", phrases)
        Dim selector As New PartyAskMessageCollection(New Random(73))
        Dim previous As String = Nothing
        Dim orders As New HashSet(Of String)
        For cycle = 1 To 60
            Dim sent As New List(Of String)
            For index = 1 To phrases.Length
                Dim current = selector.Peek(raw)
                Check(current <> previous, "adjacent messages must differ, including at cycle boundaries")
                sent.Add(current)
                selector.MarkSent()
                previous = current
            Next
            Check(New HashSet(Of String)(sent).SetEquals(phrases.Select(Function(phrase) "/all " & phrase)) AndAlso sent.Count = phrases.Length, "each cycle must contain every distinct all-channel phrase exactly once")
            orders.Add(String.Join(",", sent))
        Next
        Check(orders.Count > 1, "successive cycles must be reshuffled rather than replaying a fixed order")

        Dim pair As New PartyAskMessageCollection(New Random(4))
        previous = Nothing
        For index = 1 To 30
            Dim current = pair.Peek("one,ONE,two,two")
            Check(current <> previous, "duplicate entries must not defeat the no-repeat rule for two distinct phrases")
            previous = current
            pair.MarkSent()
        Next
        Dim oneMessage As New PartyAskMessageCollection(New Random(9))
        For index = 1 To 4
            Check(oneMessage.Peek("party pls") = "/all party pls", "a single legacy message must receive the all-channel prefix")
            oneMessage.MarkSent()
        Next
    End Sub

    Private Sub TestRetriesAndLiveEdits()
        Dim selector As New PartyAskMessageCollection(New Random(7))
        Dim pending = selector.Peek("one,two,three")
        For retry = 1 To 5
            Check(selector.Peek("one,two,three") = pending, "unsubmitted choices must remain pending for retries")
        Next
        Check(selector.Peek(" one , two , three ") = pending, "formatting edits must not discard a pending choice")
        Check(selector.Peek("/all one, /all two, /ALL three") = pending, "adding existing prefixes must not restart a pending cycle")
        selector.MarkSent()
        Check(selector.Peek("one,two,three") <> pending, "successful submission must advance to a different phrase")
        Check(selector.Peek("new phrase,another phrase") <> pending, "live edits must discard removed pending choices")
        selector.MarkSent()
        Dim last = selector.Peek("new phrase,another phrase")
        selector.MarkSent()
        Check(selector.Peek(last & ",extra phrase") = "/all extra phrase", "editing a collection must preserve the last-sent guard")
        selector.Reset()
        Check(selector.Peek("single") = "/all single", "starting a new run must discard the old collection")
        selector.MarkSent()
        selector.MarkSent()
        Check(selector.Peek("single") = "/all single", "extra acknowledgements must not consume future choices")
    End Sub

    Private Sub TestPersistenceAndButton()
        Dim raw = "Any party with room?, Looking for a group, Invite if you have a spot"
        Dim json = JsonSerializer.Serialize(New BotConfig With {.PartyAskText = "regular request", .PartyAskCollectionText = raw, .PartyAskIntervalMs = 180000, .PartyAskCollectionEnabled = True})
        Dim cfg = JsonSerializer.Deserialize(Of BotConfig)(json)
        Check(cfg.PartyAskText = "regular request" AndAlso cfg.PartyAskCollectionText = raw AndAlso cfg.PartyAskIntervalMs = 180000, "config roundtrips must preserve both texts and the interval")
        Check(Not json.Contains("PartyAskCollectionEnabled") AndAlso Not cfg.PartyAskCollectionEnabled, "saving an unlocked config must not save its runtime permission")
        Check(Not JsonSerializer.Deserialize(Of BotConfig)("{""PartyAskCollectionEnabled"":true}").PartyAskCollectionEnabled, "a saved JSON flag must not bypass the code unlock")
        Dim stateType = GetType(Form1).GetNestedType("PersistedListState", BindingFlags.NonPublic)
        Dim state = Activator.CreateInstance(stateType, nonPublic:=True)
        stateType.GetProperty("AskForPartyText").SetValue(state, "regular request")
        stateType.GetProperty("AskForPartyCollectionText").SetValue(state, raw)
        Dim restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(state, stateType), stateType)
        Check(CStr(stateType.GetProperty("AskForPartyText").GetValue(restored)) = "regular request" AndAlso CStr(stateType.GetProperty("AskForPartyCollectionText").GetValue(restored)) = raw, "saved settings and character profiles must retain both regular and hidden texts")
        Dim normalize = GetType(Form1).GetMethod("NormalizePartyAskUiText", BindingFlags.Static Or BindingFlags.NonPublic)
        Check(CStr(normalize.Invoke(Nothing, {raw})) = raw, "UI normalization must preserve delimiters")
        Dim update = GetType(Form1).GetMethod("UpdatePartyAskButtonCore", BindingFlags.Static Or BindingFlags.NonPublic)
        Using button As New Button()
            update.Invoke(Nothing, {button, True, raw & ",Any party with room?", True})
            Check(button.Text = "Auto Ask Party (3 phrases): ON", "button must show the distinct phrase count")
            update.Invoke(Nothing, {button, False, "add", True})
            Check(button.Text = "Auto Ask Party (/all add): OFF", "single-message button labels must show the actual all-channel request")
            update.Invoke(Nothing, {button, False, "add", False})
            Check(button.Text = "Auto Ask Party (add): OFF", "normal mode must show the regular message")
        End Using
    End Sub

    Private Sub TestHiddenModeEditorAndMigration()
        Dim owner = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        GetType(Form1).GetField("_applyingSettings", InstanceFlags).SetValue(owner, True)
        Dim load = GetType(Form1).GetMethod("LoadPartyAskTexts", InstanceFlags)
        Dim update = GetType(Form1).GetMethod("UpdatePartyAskEditorMode", InstanceFlags)
        Dim apply = GetType(Form1).GetMethod("ApplyPartyAskConfig", InstanceFlags)
        Dim unlock = GetType(Form1).GetField("_quizUnlocked", InstanceFlags)
        Using editor As New TextBox(), label As New Label(), button As New Button()
            GetType(Form1).GetField("txtPartyAskText", InstanceFlags).SetValue(owner, editor)
            GetType(Form1).GetField("_partyAskTextLabel", InstanceFlags).SetValue(owner, label)
            GetType(Form1).GetField("btnPartyAsk", InstanceFlags).SetValue(owner, button)
            load.Invoke(owner, {"regular request", "one,two,three"})
            Check(editor.Text = "regular request" AndAlso label.Text = "Auto Ask Party Text" AndAlso Not editor.AccessibleDescription.Contains("/all"), "locked UI must use the regular form and hide collection instructions")
            For Each language In {"English", "Spanish", "Filipino"}
                Dim help = CStr(GetType(Form1).GetMethod("BuildScopedHelpText" & language, InstanceFlags).Invoke(owner, {"combat"}))
                Check(Not help.Contains("/all"), "locked help must not describe the hidden party prefix: " & language)
            Next
            Dim cfg As New BotConfig()
            editor.Text = "regular edited, please"
            apply.Invoke(owner, {cfg})
            Check(Not cfg.PartyAskCollectionEnabled AndAlso cfg.PartyAskText = "regular edited, please" AndAlso cfg.PartyAskCollectionText = "one,two,three", "locked edits must preserve the hidden collection and literal commas")
            unlock.SetValue(owner, True)
            update.Invoke(owner, {True})
            Check(editor.Text = "one,two,three" AndAlso label.Text.Contains("comma-separated") AndAlso editor.AccessibleDescription.Contains("/all") AndAlso editor.MaxLength = 0, "the shared unlock flag must reveal the collection editor")
            For Each language In {"English", "Spanish", "Filipino"}
                Dim help = CStr(GetType(Form1).GetMethod("BuildScopedHelpText" & language, InstanceFlags).Invoke(owner, {"combat"}))
                Check(help.Contains("/all"), "unlocked help must describe the hidden party prefix: " & language)
            Next
            editor.Text = "new one,new two"
            apply.Invoke(owner, {cfg})
            Check(cfg.PartyAskCollectionEnabled AndAlso cfg.PartyAskText = "regular edited, please" AndAlso cfg.PartyAskCollectionText = "new one,new two", "hidden edits must leave the regular message intact and enable the runtime collection")
            unlock.SetValue(owner, False)
            update.Invoke(owner, {True})
            Check(editor.Text = "regular edited, please" AndAlso label.Text = "Auto Ask Party Text", "relocking must restore the regular form")
            apply.Invoke(owner, {cfg})
            Check(Not cfg.PartyAskCollectionEnabled AndAlso cfg.PartyAskCollectionText = "new one,new two", "relocking must revoke collection permission without losing its text")
            load.Invoke(owner, {"/all old one, /all old two", Nothing})
            apply.Invoke(owner, {cfg})
            Check(editor.Text = "old one" AndAlso cfg.PartyAskText = "old one" AndAlso cfg.PartyAskCollectionText = "/all old one, /all old two" AndAlso Not cfg.PartyAskCollectionEnabled, "legacy collections must migrate to hidden storage and expose only their first plain phrase normally")
            unlock.SetValue(owner, True)
            update.Invoke(owner, {True})
            Check(editor.Text = "/all old one, /all old two", "unlocking must recover the complete legacy collection")
        End Using
    End Sub

    Private Sub TestLockedEngineSubmission()
        Dim originalInput = WindowsInput.Current
        Dim recorder As New ChatRecorder()
        WindowsInput.Current = recorder
        Try
            Dim engine As New BotEngine()
            Dim cfg As New BotConfig With {.PartyAskEnabled = True, .PartyAskText = "/all /ALL hello, party please", .PartyAskCollectionText = "hidden one,hidden two", .PartyAskIntervalMs = 5000, .ChatMessageUseCtrlV = False}
            Dim send = GetType(BotEngine).GetMethod("TryHandlePartyAsk", InstanceFlags)
            Dim now As New DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc)
            For index = 0 To 1
                recorder.Reset()
                Check(CBool(send.Invoke(engine, {cfg, New IntPtr(123), now.AddSeconds(index * 6)})) AndAlso recorder.Text.ToString() = "hello, party please", "locked input must repeatedly send one plain message without prefixing or splitting commas")
            Next
        Finally
            WindowsInput.Current = originalInput
        End Try
    End Sub

    Private Sub TestEngineSubmission()
        Dim originalInput = WindowsInput.Current
        Dim recorder As New ChatRecorder()
        WindowsInput.Current = recorder
        Try
            Dim engine As New BotEngine()
            Dim cfg As New BotConfig With {.PartyAskEnabled = True, .PartyAskCollectionEnabled = True, .PartyAskCollectionText = "one,two,three", .PartyAskIntervalMs = 5000, .ChatMessageUseCtrlV = False}
            Dim send = GetType(BotEngine).GetMethod("TryHandlePartyAsk", InstanceFlags)
            Dim selector = DirectCast(GetType(BotEngine).GetField("_partyAskMessages", InstanceFlags).GetValue(engine), PartyAskMessageCollection)
            Dim now As New DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc)
            Dim pending = selector.Peek(cfg.PartyAskCollectionText)
            recorder.AllowActivate = False
            Check(Not CBool(send.Invoke(engine, {cfg, New IntPtr(123), now})), "failed chat activation must not claim submission")
            Check(selector.Peek(cfg.PartyAskCollectionText) = pending, "activation failure must not advance the collection")
            recorder.AllowActivate = True
            recorder.RejectSubmit = True
            Check(CBool(send.Invoke(engine, {cfg, New IntPtr(123), now})), "existing partial-send handling must remain intact")
            Check(recorder.Text.ToString() = pending AndAlso selector.Peek(cfg.PartyAskCollectionText) = pending, "failed final Enter must keep the typed phrase pending")
            now = now.AddSeconds(6)
            recorder.Reset()
            Check(CBool(send.Invoke(engine, {cfg, New IntPtr(123), now})), "successful chat submission must use the selected phrase")
            Check(recorder.Text.ToString() = pending AndAlso Not recorder.Text.ToString().Contains(","), "engine must send one phrase, not the collection")
            Check(recorder.Text.ToString().StartsWith("/all ", StringComparison.Ordinal), "engine must submit requests to the all channel")
            Check(selector.Peek(cfg.PartyAskCollectionText) <> pending, "only successful final Enter may advance the cycle")
            recorder.Reset()
            Check(Not CBool(send.Invoke(engine, {cfg, New IntPtr(123), now.AddSeconds(1)})) AndAlso recorder.DownCount = 0, "existing interval must still prevent early sends")
            GetType(BotEngine).GetField("_partyAskSuppressedInParty", InstanceFlags).SetValue(engine, True)
            Check(Not CBool(send.Invoke(engine, {cfg, New IntPtr(123), now.AddSeconds(6)})) AndAlso recorder.DownCount = 0, "existing in-party suppression must still prevent sends")
            GetType(BotEngine).GetField("_partyAskSuppressedInParty", InstanceFlags).SetValue(engine, False)
            cfg.PartyAskCollectionText = "/all single"
            Check(CBool(send.Invoke(engine, {cfg, New IntPtr(123), now.AddSeconds(12)})) AndAlso recorder.Text.ToString() = "/all single", "an already-prefixed request must be submitted without a duplicate prefix")
        Finally
            WindowsInput.Current = originalInput
        End Try
    End Sub

    Private NotInheritable Class ChatRecorder
        Implements IWindowsInput
        Public ReadOnly Text As New StringBuilder()
        Public AllowActivate As Boolean = True
        Public RejectSubmit As Boolean
        Public DownCount As Integer

        Public Sub Reset()
            Text.Clear()
            DownCount = 0
            RejectSubmit = False
        End Sub
        Public Function Post(hwnd As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean Implements IWindowsInput.Post
            If message = &H102UI Then Text.Append(ChrW(CInt(wParam.ToInt64())))
            If message = &H100UI Then
                DownCount += 1
                If RejectSubmit AndAlso DownCount = 2 Then Return False
            End If
            Return True
        End Function
        Public Function Activate(hwnd As IntPtr) As Boolean Implements IWindowsInput.Activate
            Return AllowActivate
        End Function
        Public Function MoveCursor(x As Integer, y As Integer) As Boolean Implements IWindowsInput.MoveCursor
            Throw New InvalidOperationException("Party-message fixture must not move the mouse.")
        End Function
        Public Sub Keyboard(key As Byte, scan As Byte, flags As UInteger, extra As UIntPtr) Implements IWindowsInput.Keyboard
            Throw New InvalidOperationException("Party-message fixture must use recorded input only.")
        End Sub
        Public Sub Mouse(flags As UInteger, x As UInteger, y As UInteger, data As UInteger, extra As UIntPtr) Implements IWindowsInput.Mouse
            Throw New InvalidOperationException("Party-message fixture must not send mouse input.")
        End Sub
    End Class
End Module
