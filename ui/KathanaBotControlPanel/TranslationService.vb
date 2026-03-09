Imports System.Collections.Concurrent
Imports System.Linq
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading.Tasks

Public Class TranslationService
    Private Shared ReadOnly Client As New HttpClient() With {
        .Timeout = TimeSpan.FromSeconds(8)
    }

    Private ReadOnly _cache As New ConcurrentDictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    Public Async Function TranslateTextAsync(sourceText As String, targetLanguage As String) As Task(Of String)
        Dim text As String = If(sourceText, "").Trim()
        If text = "" Then
            Return ""
        End If

        Dim language As String = NormalizeLanguageCode(targetLanguage)
        Dim cacheKey As String = language & "|" & text
        Dim cached As String = Nothing
        If _cache.TryGetValue(cacheKey, cached) Then
            Return cached
        End If

        Dim url As String = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&dt=t&tl=" &
            Uri.EscapeDataString(language) &
            "&q=" & Uri.EscapeDataString(text)

        Try
            Dim payload As String = Await Client.GetStringAsync(url)
            Dim translated As String = ParseTranslatedText(payload)
            If String.IsNullOrWhiteSpace(translated) Then
                translated = text
            End If

            _cache(cacheKey) = translated
            Return translated
        Catch
            Return text
        End Try
    End Function

    Private Shared Function NormalizeLanguageCode(raw As String) As String
        Dim cleaned As String = If(raw, "").Trim().ToLowerInvariant()
        If cleaned = "" Then
            Return "en"
        End If

        cleaned = New String(cleaned.Where(Function(ch) Char.IsLetter(ch) OrElse ch = "-"c).ToArray())
        If cleaned = "" Then
            Return "en"
        End If
        Return cleaned
    End Function

    Private Shared Function ParseTranslatedText(payload As String) As String
        If String.IsNullOrWhiteSpace(payload) Then
            Return ""
        End If

        Using doc As JsonDocument = JsonDocument.Parse(payload)
            If doc.RootElement.ValueKind <> JsonValueKind.Array OrElse doc.RootElement.GetArrayLength() = 0 Then
                Return ""
            End If

            Dim parts As New List(Of String)()
            For Each segment As JsonElement In doc.RootElement(0).EnumerateArray()
                If segment.ValueKind <> JsonValueKind.Array OrElse segment.GetArrayLength() = 0 Then
                    Continue For
                End If

                Dim translatedPiece As String = segment(0).GetString()
                If Not String.IsNullOrWhiteSpace(translatedPiece) Then
                    parts.Add(translatedPiece.Trim())
                End If
            Next

            Return String.Join("", parts).Trim()
        End Using
    End Function
End Class
