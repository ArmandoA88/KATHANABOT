Imports System.IO
Imports System.Text.Json

Friend Module TradeSessionTests
    Private checks As Integer

    Private Sub Check(condition As Boolean, reason As String)
        checks += 1
        If Not condition Then Throw New InvalidOperationException("Trade session test: " & reason)
    End Sub

    Public Function SampleSettings() As TradeSettings
        Return New TradeSettings With {
            .DiscordText = "PuLgA" & vbCrLf & "S> Ror Asura 7M" & vbCrLf & vbCrLf & "Akane" & vbCrLf & "B> Datu Pazuta+11",
            .ItemSearch = "ror", .Buying = False, .DelaySeconds = 7, .MessageTemplate = "Hi! {items}?",
            .DiscordChannelUrl = "https://discord.com/channels/1/2", .DiscordImportMessageCount = 250,
            .EncryptedDiscordBotToken = "SECRET-ENCRYPTED-TOKEN", .DiscordAutoImport = True,
            .ExtractedListings = New List(Of TradeListing) From {
                New TradeListing With {.CharacterName = "PuLgA", .Intent = "sell", .ItemText = "Ror Asura", .ItemKey = "ROR ASURA", .Evidence = "S> Ror Asura 7M"},
                New TradeListing With {.CharacterName = "Akane", .Intent = "buy", .ItemText = "Datu Pazuta+11", .ItemKey = "DATU PAZUTA+11", .Evidence = "B> Datu Pazuta+11"}},
            .SelectedItemKeys = New List(Of String) From {"DATU PAZUTA+11"},
            .Recipients = New List(Of TradeRecipient) From {
                New TradeRecipient With {.Selected = False, .CharacterName = "Akane", .Items = "Datu Pazuta+11", .Message = "already sent"},
                New TradeRecipient With {.Selected = True, .CharacterName = "PuLgA", .Items = "Ror Asura", .Message = "edited whisper"}}}
    End Function

    Public Sub RunTests()
        checks = 0
        TestRoundTripAndCredentials()
        TestValidation()
        TestAtomicSaveAndLoad()
        TestExtensionPackage()
        Console.WriteLine($"PASS: {checks} Trade session and embedded extension assertions: full round trip, no token saved, validation/normalization, atomic files and a self-contained, safely extracted browser extension.")
    End Sub

    Private Sub TestRoundTripAndCredentials()
        Dim original = SampleSettings()
        Dim json = TradeSessionStore.Serialize(original, DateTimeOffset.Parse("2026-10-08T12:00:00Z"))
        Check(Not json.Contains("SECRET-ENCRYPTED-TOKEN", StringComparison.Ordinal), "the Discord token was written to the session file")
        Check(original.EncryptedDiscordBotToken = "SECRET-ENCRYPTED-TOKEN" AndAlso original.DiscordAutoImport, "saving edited the live settings")
        Dim loaded = TradeSessionStore.Parse(json)
        Check(loaded.EncryptedDiscordBotToken = "" AndAlso Not loaded.DiscordAutoImport, "a loaded session carried credentials or automatic import")
        Check(loaded.DiscordText = original.DiscordText AndAlso loaded.ItemSearch = "ror" AndAlso Not loaded.Buying AndAlso loaded.DelaySeconds = 7 AndAlso loaded.MessageTemplate = "Hi! {items}?", "posts or options were lost")
        Check(loaded.ExtractedListings.Count = 2 AndAlso loaded.ExtractedListings(0).ItemKey = "ROR ASURA" AndAlso loaded.ExtractedListings(1).Intent = "buy" AndAlso loaded.ExtractedListings(1).Evidence = "B> Datu Pazuta+11", "detected items were lost")
        Check(loaded.SelectedItemKeys.SequenceEqual({"DATU PAZUTA+11"}), "selected items were lost")
        Check(loaded.Recipients.Count = 2 AndAlso Not loaded.Recipients(0).Selected AndAlso loaded.Recipients(1).Selected AndAlso loaded.Recipients(1).Message = "edited whisper", "the whisper queue, its checked state or its edits were lost")
        Check(TradeSessionStore.Summarize(loaded).Contains("2 detected listing(s) from 2 character(s)", StringComparison.Ordinal) AndAlso TradeSessionStore.Summarize(loaded).Contains("1 selected item(s)", StringComparison.Ordinal) AndAlso
              TradeSessionStore.Summarize(loaded).Contains("2 whisper(s)", StringComparison.Ordinal), "summary text changed: " & TradeSessionStore.Summarize(loaded))
        Using document = JsonDocument.Parse(json)
            Check(document.RootElement.GetProperty("Format").GetString() = TradeSessionStore.FormatName AndAlso document.RootElement.GetProperty("Version").GetInt32() = 1, "session header is missing")
        End Using
    End Sub

    Private Sub Rejected(json As String, reason As String)
        Dim failed = False
        Try
            TradeSessionStore.Parse(json)
        Catch ex As InvalidDataException
            failed = True
        End Try
        Check(failed, reason)
    End Sub

    Private Sub TestValidation()
        Rejected("", "an empty file was accepted")
        Rejected("not json", "garbage was accepted")
        Rejected("[]", "a JSON array was accepted")
        Rejected("{}", "an object without the session header was accepted")
        Rejected("{""Format"":""Other"",""Version"":1,""Settings"":{}}", "another file format was accepted")
        Rejected("{""Format"":""KathanaTradeSession"",""Version"":99,""Settings"":{}}", "a newer session format was accepted")
        Rejected("{""Format"":""KathanaTradeSession"",""Version"":1}", "a session without data was accepted")
        ' A plain profile-style settings blob is not a session.
        Rejected(JsonSerializer.Serialize(SampleSettings()), "raw settings JSON was accepted as a session")
        ' JSON nulls and bad rows are normalized so the Trade tab never meets a null or an unusable listing.
        Dim messy = "{""Format"":""KathanaTradeSession"",""Version"":1,""Settings"":{""DiscordText"":null,""MessageTemplate"":null,""ItemSearch"":null," &
            """ExtractedListings"":[null,{""CharacterName"":null,""Intent"":""sell"",""ItemKey"":""X""},{""CharacterName"":""Ok"",""Intent"":""trade"",""ItemKey"":""X""}," &
            "{""CharacterName"":"" Ok "",""Intent"":""sell"",""ItemText"":null,""ItemKey"":""KEY"",""Evidence"":null}],""SelectedItemKeys"":[null,"""",""KEY""]," &
            """Recipients"":[null,{""CharacterName"":""  "",""Message"":""x""},{""CharacterName"":"" Bob "",""Items"":null,""Message"":null,""Selected"":true}],""PriceOffers"":[null]}}"
        Dim cleaned = TradeSessionStore.Parse(messy)
        Check(cleaned.DiscordText = "" AndAlso cleaned.MessageTemplate = "" AndAlso cleaned.ItemSearch = "", "null text fields were not normalized")
        Check(cleaned.ExtractedListings.Count = 1 AndAlso cleaned.ExtractedListings(0).CharacterName = "Ok" AndAlso cleaned.ExtractedListings(0).ItemText = "" AndAlso cleaned.ExtractedListings(0).Evidence = "", "unusable listings were kept or nulls were left in a listing")
        Check(cleaned.SelectedItemKeys.SequenceEqual({"KEY"}), "blank selected keys were kept")
        Check(cleaned.Recipients.Count = 1 AndAlso cleaned.Recipients(0).CharacterName = "Bob" AndAlso cleaned.Recipients(0).Items = "" AndAlso cleaned.Recipients(0).Message = "" AndAlso cleaned.Recipients(0).Selected, "unusable whisper rows were kept or nulls were left in a row")
        Check(cleaned.PriceOffers.Count = 0, "a null price offer was kept")
    End Sub

    Private Sub TestAtomicSaveAndLoad()
        Dim folder = Path.Combine(Path.GetTempPath(), "kathana-session-test-" & Guid.NewGuid().ToString("N"))
        Try
            Dim path1 = Path.Combine(folder, "nested", "one" & TradeSessionStore.FileExtension)
            TradeSessionStore.Save(path1, SampleSettings())
            Check(File.Exists(path1) AndAlso Not File.Exists(path1 & ".tmp"), "save left a temporary file or no file")
            Check(TradeSessionStore.Load(path1).Recipients.Count = 2, "a saved file did not load")
            ' Saving over an existing session replaces it completely.
            Dim second = SampleSettings()
            second.Recipients.Clear()
            TradeSessionStore.Save(path1, second)
            Check(TradeSessionStore.Load(path1).Recipients.Count = 0 AndAlso Not File.Exists(path1 & ".tmp"), "overwriting a session kept old data")
            Dim missing = False
            Try
                TradeSessionStore.Load(Path.Combine(folder, "missing.json"))
            Catch ex As FileNotFoundException
                missing = True
            End Try
            Check(missing, "a missing file did not report FileNotFound")
            File.WriteAllText(Path.Combine(folder, "bad.json"), "{broken")
            Dim bad = False
            Try
                TradeSessionStore.Load(Path.Combine(folder, "bad.json"))
            Catch ex As InvalidDataException
                bad = True
            End Try
            Check(bad, "a corrupt file did not report InvalidData")
            Check(TradeSessionStore.DefaultFolder().EndsWith(Path.Combine("KathanaBotControlPanel", "trade_sessions"), StringComparison.OrdinalIgnoreCase), "default folder moved")
        Finally
            Try
                Directory.Delete(folder, True)
            Catch
            End Try
        End Try
    End Sub

    ' The extension travels inside the EXE. Compare it with the repository copy so the two can never drift.
    Private Function FindExtensionSource() As String
        Dim directory = New DirectoryInfo(AppContext.BaseDirectory)
        While directory IsNot Nothing
            Dim candidate = Path.Combine(directory.FullName, "tools", "discord-browser-capture")
            If File.Exists(Path.Combine(candidate, "manifest.json")) Then Return candidate
            directory = directory.Parent
        End While
        Return Nothing
    End Function

    Private Sub TestExtensionPackage()
        Dim names = DiscordCaptureExtensionPackage.FileNames()
        For Each required In {"manifest.json", "worker.mjs", "core.mjs", "scroll.mjs", "search.mjs", "popup.html", "popup.mjs", "popup.css", "README.md"}
            Check(names.Contains(required), "the extension file is not embedded: " & required)
        Next
        Check(Not names.Any(Function(name) name.Contains("test", StringComparison.OrdinalIgnoreCase) OrElse name.Contains("\"c) OrElse name.Contains("/"c)), "tests or paths were embedded in the extension package")
        Using manifest = JsonDocument.Parse(DiscordCaptureExtensionPackage.ReadFile("manifest.json"))
            Check(manifest.RootElement.GetProperty("name").GetString() = "Kathana Discord Capture" AndAlso manifest.RootElement.GetProperty("manifest_version").GetInt32() = 3, "embedded manifest is not the capture extension")
        End Using
        Check(DiscordCaptureExtensionPackage.Version().Split("."c).Length = 3, "extension version is not a dotted version")

        Dim folder = Path.Combine(Path.GetTempPath(), "kathana-extension-test-" & Guid.NewGuid().ToString("N"))
        Try
            Check(DiscordCaptureExtensionPackage.Extract(folder) = names.Count, "extract did not write every file")
            Dim source = FindExtensionSource()
            For Each name In names
                Check(File.Exists(Path.Combine(folder, name)) AndAlso Not File.Exists(Path.Combine(folder, name & ".tmp")), "extracted file missing or temporary file left: " & name)
                If source IsNot Nothing Then
                    Check(File.ReadAllBytes(Path.Combine(source, name)).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, name))), "embedded copy differs from tools/discord-browser-capture: " & name)
                End If
            Next
            ' Re-installing after an update refreshes files and removes stale package files, but leaves unrelated files alone.
            File.WriteAllText(Path.Combine(folder, "worker.mjs"), "// old version")
            File.WriteAllText(Path.Combine(folder, "removed-in-new-version.mjs"), "// stale")
            File.WriteAllText(Path.Combine(folder, "notes-from-user.txt"), "keep me")
            Check(DiscordCaptureExtensionPackage.Extract(folder) = names.Count, "second extract failed")
            Check(File.ReadAllText(Path.Combine(folder, "worker.mjs")) <> "// old version", "extract did not refresh an older extension file")
            Check(Not File.Exists(Path.Combine(folder, "removed-in-new-version.mjs")), "a stale extension file was kept")
            Check(File.Exists(Path.Combine(folder, "notes-from-user.txt")), "an unrelated file was deleted")
        Finally
            Try
                Directory.Delete(folder, True)
            Catch
            End Try
        End Try
        ' A folder that is not ours is never cleaned.
        Dim foreign = Path.Combine(Path.GetTempPath(), "kathana-extension-foreign-" & Guid.NewGuid().ToString("N"))
        Try
            Directory.CreateDirectory(foreign)
            File.WriteAllText(Path.Combine(foreign, "other.mjs"), "// someone else's")
            File.WriteAllText(Path.Combine(foreign, "manifest.json"), "{""name"":""Another extension""}")
            DiscordCaptureExtensionPackage.Extract(foreign)
            Check(File.Exists(Path.Combine(foreign, "other.mjs")), "files in a folder holding another extension were deleted")
        Finally
            Try
                Directory.Delete(foreign, True)
            Catch
            End Try
        End Try
        Dim rejected = 0
        For Each bad In {"", "   "}
            Try
                DiscordCaptureExtensionPackage.Extract(bad)
            Catch ex As ArgumentException
                rejected += 1
            End Try
        Next
        Try
            DiscordCaptureExtensionPackage.Extract(Path.GetPathRoot(Path.GetTempPath()))
        Catch ex As ArgumentException
            rejected += 1
        End Try
        Check(rejected = 3, "an empty path or a drive root was accepted as the extension folder")
    End Sub
End Module
