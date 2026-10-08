Imports System.IO
Imports System.Text.Json

' A saved session is one JSON file holding the posts, detected items, selected items and whisper queue so they can be
' loaded again later. It never contains the Discord reader token or automatic-import settings.
Public NotInheritable Class TradeSessionFile
    Public Property Format As String = TradeSessionStore.FormatName
    Public Property Version As Integer = TradeSessionStore.CurrentVersion
    Public Property SavedAtUtc As DateTimeOffset
    Public Property Settings As New TradeSettings()
End Class

Public NotInheritable Class TradeSessionStore
    Public Const FormatName As String = "KathanaTradeSession"
    Public Const CurrentVersion As Integer = 1
    Public Const FileExtension As String = ".kathana-trade.json"
    Public Const MaximumFileBytes As Long = 256L * 1024 * 1024
    Private Const MaximumListings As Integer = 500000
    Private Const MaximumRecipients As Integer = 50000
    Private Const MaximumSelectedItems As Integer = 500000

    Private Sub New()
    End Sub

    Public Shared Function DefaultFolder() As String
        Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KathanaBotControlPanel", "trade_sessions")
    End Function

    Public Shared Function Serialize(settings As TradeSettings, savedAtUtc As DateTimeOffset) As String
        If settings Is Nothing Then Throw New ArgumentNullException(NameOf(settings))
        ' Copy through JSON so the caller's live objects are never edited, then drop everything that is a credential.
        Dim copy = JsonSerializer.Deserialize(Of TradeSettings)(JsonSerializer.Serialize(settings))
        copy.EncryptedDiscordBotToken = ""
        copy.DiscordAutoImport = False
        Return JsonSerializer.Serialize(New TradeSessionFile With {.SavedAtUtc = savedAtUtc, .Settings = copy})
    End Function

    Public Shared Function Parse(json As String) As TradeSettings
        If String.IsNullOrWhiteSpace(json) Then Throw New InvalidDataException("The session file is empty.")
        Dim file As TradeSessionFile
        Try
            ' The header is checked on the raw JSON: the file type's own defaults would make a header-less file look valid.
            Using document = JsonDocument.Parse(json)
                Dim root = document.RootElement
                Dim format As JsonElement, version As JsonElement, content As JsonElement, versionNumber As Integer
                If root.ValueKind <> JsonValueKind.Object OrElse Not root.TryGetProperty("Format", format) OrElse format.ValueKind <> JsonValueKind.String OrElse
                    format.GetString() <> FormatName Then Throw New InvalidDataException("This is not a Kathana Trade session file.")
                If Not root.TryGetProperty("Version", version) OrElse version.ValueKind <> JsonValueKind.Number OrElse Not version.TryGetInt32(versionNumber) Then Throw New InvalidDataException("The session file has no valid format version.")
                If versionNumber < 1 OrElse versionNumber > CurrentVersion Then
                    Throw New InvalidDataException($"This session was saved by a newer KathanaBot (session format {versionNumber}). Update KathanaBot to load it.")
                End If
                If Not root.TryGetProperty("Settings", content) OrElse content.ValueKind <> JsonValueKind.Object Then Throw New InvalidDataException("The session file has no saved Trade data.")
            End Using
            file = JsonSerializer.Deserialize(Of TradeSessionFile)(json)
        Catch ex As JsonException
            Throw New InvalidDataException("This is not a valid Trade session file.", ex)
        End Try
        Dim settings = file?.Settings
        If settings Is Nothing Then Throw New InvalidDataException("The session file has no saved Trade data.")
        If If(settings.DiscordText, "").Length > DiscordTradeService.MaximumImportedCharacters Then Throw New InvalidDataException("The saved posts are larger than the import limit.")
        If If(settings.ExtractedListings, New List(Of TradeListing)()).Count > MaximumListings OrElse
            If(settings.Recipients, New List(Of TradeRecipient)()).Count > MaximumRecipients OrElse
            If(settings.SelectedItemKeys, New List(Of String)()).Count > MaximumSelectedItems Then
            Throw New InvalidDataException("The session file contains more data than a Trade session can hold.")
        End If
        Return Clean(settings)
    End Function

    ' JSON nulls replace property defaults, so normalize everything the Trade tab reads without checking.
    Private Shared Function Clean(settings As TradeSettings) As TradeSettings
        settings.DiscordText = If(settings.DiscordText, "")
        settings.ItemSearch = If(settings.ItemSearch, "")
        settings.WantedItems = If(settings.WantedItems, "")
        settings.MessageTemplate = If(settings.MessageTemplate, "")
        settings.EncryptedDiscordBotToken = ""
        settings.DiscordAutoImport = False
        settings.ExtractedListings = If(settings.ExtractedListings, New List(Of TradeListing)()).
            Where(Function(entry) entry IsNot Nothing).
            Select(Function(entry) New TradeListing With {.CharacterName = If(entry.CharacterName, "").Trim(), .Intent = If(entry.Intent, ""),
                .ItemText = If(entry.ItemText, ""), .ItemKey = If(entry.ItemKey, ""), .Evidence = If(entry.Evidence, "")}).
            Where(Function(entry) entry.CharacterName.Length > 0 AndAlso entry.ItemKey.Trim().Length > 0 AndAlso (entry.Intent = "buy" OrElse entry.Intent = "sell")).ToList()
        settings.SelectedItemKeys = If(settings.SelectedItemKeys, New List(Of String)()).Where(Function(key) Not String.IsNullOrWhiteSpace(key)).ToList()
        settings.Recipients = If(settings.Recipients, New List(Of TradeRecipient)()).
            Where(Function(row) row IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(row.CharacterName)).
            Select(Function(row) New TradeRecipient With {.Selected = row.Selected, .CharacterName = row.CharacterName.Trim(),
                .Items = If(row.Items, ""), .Message = If(row.Message, "")}).ToList()
        settings.PriceOffers = If(settings.PriceOffers, New List(Of TradePriceOffer)()).Where(Function(offer) offer IsNot Nothing).ToList()
        Return settings
    End Function

    Public Shared Sub Save(path As String, settings As TradeSettings)
        If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentException("Choose a file for the session.", NameOf(path))
        Dim full = System.IO.Path.GetFullPath(path)
        Dim folder = System.IO.Path.GetDirectoryName(full)
        If Not String.IsNullOrEmpty(folder) Then Directory.CreateDirectory(folder)
        Dim temporary = full & ".tmp"
        ' Write beside the target and swap in, so an interrupted save never leaves a half-written session.
        File.WriteAllText(temporary, Serialize(settings, DateTimeOffset.UtcNow), New System.Text.UTF8Encoding(False))
        File.Move(temporary, full, True)
    End Sub

    Public Shared Function Load(path As String) As TradeSettings
        Dim info As New FileInfo(path)
        If Not info.Exists Then Throw New FileNotFoundException("The session file was not found.", path)
        If info.Length > MaximumFileBytes Then Throw New InvalidDataException("The session file is too large to load.")
        Return Parse(File.ReadAllText(path, System.Text.Encoding.UTF8))
    End Function

    Public Shared Function Summarize(settings As TradeSettings) As String
        Dim listings = If(settings.ExtractedListings, New List(Of TradeListing)())
        Dim characters = listings.Select(Function(entry) entry.CharacterName).Distinct(StringComparer.Ordinal).Count()
        Return $"{listings.Count:N0} detected listing(s) from {characters:N0} character(s), {If(settings.SelectedItemKeys, New List(Of String)()).Count:N0} selected item(s), " &
            $"{If(settings.Recipients, New List(Of TradeRecipient)()).Count:N0} whisper(s) in the queue"
    End Function
End Class
