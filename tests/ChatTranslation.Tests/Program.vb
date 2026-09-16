Imports System.Reflection

Module Program
    Private Const Flags As TextFormatFlags = TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix

    <STAThread>
    Sub Main()
        Dim texts = {"CastorTroid: xd", "kSyy: i dont know where it is", "ESPECTRO: xD", "RESUMAMI: me xd", "ESPECTRO: piedras mierda", "ESPECTRO: piedras mierda"}
        Dim regions As New List(Of OcrReader.OcrTextRegion)()
        Using source As New Bitmap(420, 110), g = Graphics.FromImage(source), font As New Font("Tahoma", 12, FontStyle.Regular, GraphicsUnit.Pixel)
            g.Clear(Color.FromArgb(16, 16, 16))
            For i = 0 To texts.Length - 1
                Dim top = 2 + i * 18
                TextRenderer.DrawText(g, texts(i), font, New Rectangle(6, top, 410, 18), Color.White, Flags)
                Dim ink = InkBounds(source, New Rectangle(0, top, source.Width, 18))
                Dim words As New List(Of OcrReader.OcrTextRegion)()
                Dim colon = texts(i).IndexOf(":"c)
                Dim name = texts(i).Substring(0, colon + 1)
                Dim message = texts(i).Substring(colon + 2)
                Dim messageX = 6 + TextRenderer.MeasureText(name & " ", font, Size.Empty, Flags).Width
                words.Add(New OcrReader.OcrTextRegion With {.Text = name, .Bounds = Twice(New Rectangle(ink.Left, ink.Top, messageX - ink.Left - 2, ink.Height))})
                words.Add(New OcrReader.OcrTextRegion With {.Text = message, .Bounds = Twice(New Rectangle(messageX, ink.Top, Math.Max(1, ink.Right - messageX), ink.Height))})
                If i = 2 Then
                    ' OCR can return separate name/message regions for the same physical row.
                    regions.Add(New OcrReader.OcrTextRegion With {.Text = message, .Bounds = words(1).Bounds, .Words = New List(Of OcrReader.OcrTextRegion) From {words(1)}})
                    regions.Add(New OcrReader.OcrTextRegion With {.Text = name, .Bounds = words(0).Bounds, .Words = New List(Of OcrReader.OcrTextRegion) From {words(0)}})
                Else
                    regions.Add(New OcrReader.OcrTextRegion With {.Text = texts(i), .Bounds = Twice(ink), .Words = words})
                End If
            Next
            If Environment.GetCommandLineArgs().Contains("--ocr-check") Then
                Using enlarged As New Bitmap(source.Width * 2, source.Height * 2), enlargedGraphics = Graphics.FromImage(enlarged)
                    enlargedGraphics.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor
                    enlargedGraphics.DrawImage(source, New Rectangle(0, 0, enlarged.Width, enlarged.Height))
                    Dim detected = OcrReader.ReadScreenTextRegions(enlarged)
                    Check(detected.Count > 0, "Installed OCR returned no rows: " & OcrReader.LastError())
                    Check(detected.All(Function(region) region.Words.Count > 0), "OCR word bounds missing")
                    Dim detectedLines = ChatOverlayLine.FromOcr(detected, source, New Rectangle(13, 604, source.Width, source.Height), 2)
                    Console.WriteLine("OCR integration: " & String.Join(" | ", detectedLines.Select(Function(line) line.SourceText)))
                    Check(detectedLines.Count = texts.Length, "Installed OCR did not retain the six visual rows")
                End Using
            End If
            regions.Reverse()
            Dim capture As New Rectangle(13, 604, source.Width, source.Height)
            Dim lines = ChatOverlayLine.FromOcr(regions, source, capture, 2)
            Check(lines.Count = texts.Length, "Repeated lines or fragments were lost")
            Check(lines.Select(Function(line) line.SourceText).SequenceEqual(texts), "Visual ordering or speaker/message joining failed")
            Check(lines.All(Function(line) line.CaptureBounds = capture), "Capture geometry was lost")
            Check(lines.All(Function(line) Math.Abs(line.FontPixelSize - 12) <= 2), "Font height no longer matches original glyphs")
            Dim translated = {"CastorTroid: xd", "kSyy: no se donde esta", "ESPECTRO: xD", "RESUMAMI: yo xd", "ESPECTRO: malditas piedras", "ESPECTRO: malditas piedras"}
            For i = 0 To lines.Count - 1
                lines(i).TranslatedText = translated(i)
            Next
            Using result As New Bitmap(source), output = Graphics.FromImage(result)
                ChatTranslationOverlayForm.DrawTranslatedLines(output, lines, New Rectangle(Point.Empty, result.Size))
                ' Speaker pixels must remain exactly identical, not OCR re-renderings of the names.
                For Each line In lines
                    For y = line.SourceBounds.Top To line.SourceBounds.Bottom - 1
                        For x = 0 To line.MessageLeft - 1
                            Check(source.GetPixel(x, y) = result.GetPixel(x, y), "Speaker pixels changed")
                        Next
                    Next
                Next
                Dim directory = IO.Path.Combine(AppContext.BaseDirectory, "render-check")
                IO.Directory.CreateDirectory(directory)
                source.Save(IO.Path.Combine(directory, "original.png"))
                result.Save(IO.Path.Combine(directory, "translated.png"))
                ' Long translations must neither shrink the configured font nor paint the next row.
                Dim originalFont = lines(1).FontPixelSize
                lines(1).TranslatedText = lines(1).SpeakerPrefix & " " & New String("W"c, 200)
                ChatTranslationOverlayForm.DrawTranslatedLines(output, {lines(1)}, New Rectangle(Point.Empty, result.Size))
                Check(lines(1).FontPixelSize = originalFont, "Long text shrank the font")
                For y = lines(2).SourceBounds.Top To lines(2).SourceBounds.Bottom - 1
                    For x = 0 To source.Width - 1
                        Check(source.GetPixel(x, y) = result.GetPixel(x, y), "Long translation overlapped another row")
                    Next
                Next
            End Using
            Dim clone = lines(0).Copy()
            clone.SourceBounds = Rectangle.Empty
            Check(Not lines(0).SourceBounds.IsEmpty, "Snapshots share mutable geometry")
        End Using
        ' Late network replies must retain their row index and cannot overwrite a newer screen.
        Dim binding = BindingFlags.NonPublic Or BindingFlags.Instance
        Dim form = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Dim entries As New List(Of ChatOverlayLine) From {
            New ChatOverlayLine With {.SourceText = "First: hola", .TranslatedText = "First: hola"},
            New ChatOverlayLine With {.SourceText = "Second: adios", .TranslatedText = "Second: adios"}}
        GetType(Form1).GetField("_chatOverlayEntries", binding).SetValue(form, entries)
        GetType(Form1).GetField("_chatScreenGeneration", binding).SetValue(form, 2)
        Dim apply = GetType(Form1).GetMethod("ApplyTranslatedChatEntry", binding)
        apply.Invoke(form, {2, 1, "Second: adios", "Second: goodbye"})
        apply.Invoke(form, {1, 0, "First: hola", "WRONG SCREEN"})
        Check(entries(0).TranslatedText = "First: hola" AndAlso entries(1).TranslatedText = "Second: goodbye", "Late translation reordered or overwrote rows")
        GetType(Form1).GetField("_lastChatOcrText", binding).SetValue(form, "First: hola" & vbLf & "Second: adios")
        GetType(Form1).GetField("_lastChatTargetLanguage", binding).SetValue(form, "en")
        Using enabled As New CheckBox With {.Checked = True}, overlay As New CheckBox With {.Checked = False}
            GetType(Form1).GetField("chkChatTranslationEnabled", binding).SetValue(form, enabled)
            GetType(Form1).GetField("chkChatTranslationOverlay", binding).SetValue(form, overlay)
            Dim handle = GetType(Form1).GetMethod("HandleChatTranslation", binding)
            For scan = 1 To 20
                handle.Invoke(form, {New BotStatus With {.ChatOcrText = "", .ChatOcrLines = New List(Of ChatOverlayLine)()}})
            Next
            Check(entries.Count = 2 AndAlso entries(1).TranslatedText = "Second: goodbye", "Unreadable scans cleared the overlay")
            Check(CInt(GetType(Form1).GetField("_chatScreenGeneration", binding).GetValue(form)) = 2, "Unreadable scans cancelled pending translations")
            Dim applyMatching = GetType(Form1).GetMethod("ApplyMatchingChatTranslation", binding)
            applyMatching.Invoke(form, {"es", "First: hola", "WRONG LANGUAGE"})
            Check(entries(0).TranslatedText = "First: hola", "Old language result overwrote the current language")
            entries.Reverse()
            applyMatching.Invoke(form, {"en", "First: hola", "First: hello"})
            Check(entries(1).TranslatedText = "First: hello", "Pending translation failed after OCR gap or row movement")
            Dim preserve = GetType(Form1).GetMethod("PreserveChatTranslations", BindingFlags.NonPublic Or BindingFlags.Static)
            Dim fresh As New List(Of ChatOverlayLine) From {
                New ChatOverlayLine With {.SourceText = "First: hola", .SourceBounds = New Rectangle(3, 30, 80, 12)},
                New ChatOverlayLine With {.SourceText = "New: text", .TranslatedText = "New: text"}}
            Dim retained = DirectCast(preserve.Invoke(Nothing, {fresh, entries}), List(Of ChatOverlayLine))
            Check(retained(0).TranslatedText = "First: hello" AndAlso retained(0).SourceBounds.Y = 30, "Fresh layout discarded an existing translation")
            Check(retained(1).TranslatedText = "New: text", "Translation was assigned to an unrelated message")
            enabled.Checked = False
            handle.Invoke(form, {New BotStatus()})
            Check(entries.Count = 0, "Disabling translation did not clear retained overlay")
        End Using
        Console.WriteLine("PASS: repeated unreadable scans retain overlay and pending work; late results follow exact source/language; changed layouts retain translations; disabling clears them.")
        Dim parse = GetType(Form1).GetMethod("ParseChatOcrLines", BindingFlags.NonPublic Or BindingFlags.Static)
        Dim parsed = DirectCast(parse.Invoke(Nothing, {"Name: quoted Other: message" & vbLf & "Name: same" & vbLf & "Name: same"}), List(Of String))
        Check(parsed.Count = 3 AndAlso parsed(0) = "Name: quoted Other: message", "Colon heuristic split a real row")
        Dim translationParse = GetType(TranslationService).GetMethod("ParseTranslatedText", BindingFlags.NonPublic Or BindingFlags.Static)
        Dim payload = "[[[""hello "",""hola""],[""world"",""mundo""]]]"
        Check(CStr(translationParse.Invoke(Nothing, {payload})) = "hello world", "Translation segments lost their spaces")
        Console.WriteLine("PASS: spatial ordering, duplicate rows, same-row OCR fragments, original speaker pixels, font height, long-line clipping, independent snapshots, colon preservation, translation segment spacing.")
    End Sub

    Private Function Twice(rect As Rectangle) As Rectangle
        Return New Rectangle(rect.X * 2, rect.Y * 2, rect.Width * 2, rect.Height * 2)
    End Function

    Private Function InkBounds(bitmap As Bitmap, area As Rectangle) As Rectangle
        Dim left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1
        For y = area.Top To area.Bottom - 1
            For x = area.Left To area.Right - 1
                If bitmap.GetPixel(x, y).R > 100 Then
                    left = Math.Min(left, x) : right = Math.Max(right, x)
                    top = Math.Min(top, y) : bottom = Math.Max(bottom, y)
                End If
            Next
        Next
        Return Rectangle.FromLTRB(left, top, right + 1, bottom + 1)
    End Function

    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New Exception(message)
    End Sub
End Module
