Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class ChatOverlayLine
    Public Property SourceText As String = ""
    Public Property TranslatedText As String = ""
    Public Property CreatedAtUtc As DateTime = DateTime.UtcNow
    Public Property SourceBounds As Rectangle
    Public Property CaptureBounds As Rectangle
    Public Property MessageLeft As Integer
    Public Property SpeakerPrefix As String = ""
    Public Property FontPixelSize As Single = 12
    Public Property GlyphTop As Integer
    Public Property TextColor As Color = Color.White
    Public Property BackgroundColor As Color = Color.FromArgb(15, 12, 9)

    Public Function Copy() As ChatOverlayLine
        Return DirectCast(MemberwiseClone(), ChatOverlayLine)
    End Function

    Private Shared ReadOnly FontMetrics As New System.Collections.Concurrent.ConcurrentDictionary(Of String, Rectangle)()

    Private Shared Function MeasureGlyphs(text As String, pixels As Single) As Rectangle
        Dim key = pixels.ToString(Globalization.CultureInfo.InvariantCulture) & "|" & text
        Dim cached As Rectangle
        If FontMetrics.TryGetValue(key, cached) Then Return cached
        Using bitmap As New Bitmap(768, 80), g = Graphics.FromImage(bitmap),
              font As New Font("Tahoma", pixels, FontStyle.Regular, GraphicsUnit.Pixel)
            g.Clear(Color.Black)
            TextRenderer.DrawText(g, text, font, New Rectangle(0, 0, bitmap.Width, bitmap.Height), Color.White,
                                  TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix)
            Dim top = bitmap.Height, bottom = -1
            For y = 0 To bitmap.Height - 1
                For x = 0 To bitmap.Width - 1
                    If bitmap.GetPixel(x, y).R > 100 Then
                        top = Math.Min(top, y)
                        bottom = y
                        Exit For
                    End If
                Next
            Next
            cached = If(bottom >= top, New Rectangle(0, top, 1, bottom - top + 1), New Rectangle(0, 0, 1, CInt(pixels)))
        End Using
        If FontMetrics.Count > 512 Then FontMetrics.Clear()
        FontMetrics(key) = cached
        Return cached
    End Function

    Public Shared Function FromOcr(regions As IEnumerable(Of OcrReader.OcrTextRegion), source As Bitmap,
                                   capture As Rectangle, scale As Integer) As List(Of ChatOverlayLine)
        Dim rows As New List(Of List(Of OcrReader.OcrTextRegion))()
        For Each region In regions.Where(Function(r) r.Bounds.Height > 0 AndAlso Not String.IsNullOrWhiteSpace(r.Text)).OrderBy(Function(r) r.Bounds.Top).ThenBy(Function(r) r.Bounds.Left)
            Dim row = rows.LastOrDefault()
            ' OCR sometimes separates the name and message into horizontally adjacent regions.
            If row Is Nothing OrElse Math.Min(row(0).Bounds.Bottom, region.Bounds.Bottom) - Math.Max(row(0).Bounds.Top, region.Bounds.Top) < Math.Min(row(0).Bounds.Height, region.Bounds.Height) * 0.5 Then
                row = New List(Of OcrReader.OcrTextRegion)()
                rows.Add(row)
            End If
            row.Add(region)
        Next
        Dim output As New List(Of ChatOverlayLine)()
        For Each row In rows
            Dim ordered = row.OrderBy(Function(r) r.Bounds.Left).ToList()
            Dim text = String.Join(" ", ordered.Select(Function(r) r.Text))
            Dim bounds = ordered.Select(Function(r) r.Bounds).Aggregate(Function(a, b) Rectangle.Union(a, b))
            bounds = Rectangle.FromLTRB(CInt(Math.Floor(bounds.Left / CDbl(scale))), CInt(Math.Floor(bounds.Top / CDbl(scale))),
                                        CInt(Math.Ceiling(bounds.Right / CDbl(scale))), CInt(Math.Ceiling(bounds.Bottom / CDbl(scale))))
            bounds = Rectangle.Intersect(bounds, New Rectangle(System.Drawing.Point.Empty, source.Size))
            If bounds.IsEmpty Then Continue For
            Dim entry As New ChatOverlayLine With {.SourceText = text, .TranslatedText = text, .SourceBounds = bounds,
                .CaptureBounds = capture, .MessageLeft = bounds.Left, .FontPixelSize = Math.Max(6, bounds.Height * 1.25F)}
            Dim referenceInk = MeasureGlyphs(text, 16)
            entry.FontPixelSize = Math.Clamp(CSng(Math.Round(16.0 * bounds.Height / Math.Max(1, referenceInk.Height) * 2) / 2), 6.0F, 40.0F)
            entry.GlyphTop = MeasureGlyphs(text, entry.FontPixelSize).Top
            Dim colon = text.IndexOf(":"c)
            If colon > 0 Then
                entry.SpeakerPrefix = text.Substring(0, colon + 1)
                Dim words = ordered.SelectMany(Function(r) r.Words).OrderBy(Function(w) w.Bounds.Left).ToList()
                Dim colonWord = words.FindIndex(Function(w) w.Text.Contains(":"))
                If colonWord >= 0 Then
                    Dim word = words(colonWord)
                    Dim split = word.Text.IndexOf(":"c) + 1
                    If split = word.Text.Length AndAlso colonWord + 1 < words.Count Then
                        entry.MessageLeft = CInt(Math.Floor(words(colonWord + 1).Bounds.Left / CDbl(scale)))
                    Else
                        Using font As New Font("Tahoma", entry.FontPixelSize, FontStyle.Regular, GraphicsUnit.Pixel)
                            Dim flags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
                            Dim prefixWidth = TextRenderer.MeasureText(word.Text.Substring(0, split), font, Size.Empty, flags).Width
                            Dim wordWidth = Math.Max(1, TextRenderer.MeasureText(word.Text, font, Size.Empty, flags).Width)
                            entry.MessageLeft = CInt(Math.Ceiling((word.Bounds.Left + word.Bounds.Width * prefixWidth / CDbl(wordWidth)) / scale))
                        End Using
                    End If
                Else
                    ' Without word geometry, keep the original row rather than cover its speaker.
                    Continue For
                End If
            End If
            Dim sample = Rectangle.Intersect(New Rectangle(entry.MessageLeft, bounds.Top, Math.Max(1, bounds.Right - entry.MessageLeft), bounds.Height), New Rectangle(System.Drawing.Point.Empty, source.Size))
            Dim colors As New Dictionary(Of Integer, Integer)()
            Dim brightest = Color.White
            Dim maxBrightness As Integer = -1
            For y = sample.Top To sample.Bottom - 1
                For x = sample.Left To sample.Right - 1
                    Dim color = source.GetPixel(x, y)
                    Dim key = Color.FromArgb(color.R And &HF0, color.G And &HF0, color.B And &HF0).ToArgb()
                    colors(key) = If(colors.ContainsKey(key), colors(key), 0) + 1
                    Dim brightness = CInt(color.R) + color.G + color.B
                    If brightness > maxBrightness Then
                        brightest = color
                        maxBrightness = brightness
                    End If
                Next
            Next
            If colors.Count > 0 Then entry.BackgroundColor = Color.FromArgb(colors.OrderByDescending(Function(c) c.Value).First().Key)
            entry.TextColor = brightest
            output.Add(entry)
        Next
        Return output
    End Function
End Class

Public Class ChatTranslationOverlayForm
    Inherits Form

    Private ReadOnly _configProvider As Func(Of BotConfig)
    Private ReadOnly _timer As New Timer()
    Private _entries As New List(Of ChatOverlayLine)()
    Private _enabled As Boolean

    Public Sub New(configProvider As Func(Of BotConfig))
        _configProvider = configProvider
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        TopMost = True
        StartPosition = FormStartPosition.Manual
        AutoScaleMode = AutoScaleMode.None
        BackColor = Color.Magenta
        TransparencyKey = Color.Magenta
        DoubleBuffered = True
        _timer.Interval = 150
        AddHandler _timer.Tick, AddressOf TickUpdate
        _timer.Start()
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        ' Prevent the translated overlay from being OCR'd again by screen-capture fallback.
        NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE)
    End Sub

    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp = MyBase.CreateParams
            cp.ExStyle = cp.ExStyle Or &H80 Or &H80000 Or &H20
            Return cp
        End Get
    End Property

    Public Sub UpdateContent(entries As List(Of ChatOverlayLine), enabled As Boolean)
        _enabled = enabled
        _entries = If(entries, New List(Of ChatOverlayLine)()).Select(Function(entry) entry.Copy()).ToList()
        TickUpdate(Me, EventArgs.Empty)
        Invalidate()
    End Sub

    Private Sub TickUpdate(sender As Object, e As EventArgs)
        If Not _enabled OrElse _entries.Count = 0 OrElse _configProvider Is Nothing Then
            Hide()
            Return
        End If
        Dim cfg = _configProvider.Invoke()
        Dim clientRect As Rectangle
        If cfg Is Nothing OrElse Not BotEngine.TryGetClientScreenRect(cfg, clientRect) Then
            Hide()
            Return
        End If
        ' Use the actual resolved capture rectangle, including calibration/resolution scaling.
        Dim chatRect = _entries(0).CaptureBounds
        If chatRect.Width <= 0 OrElse chatRect.Height <= 0 Then
            Hide()
            Return
        End If
        Dim bounded = Rectangle.Intersect(New Rectangle(clientRect.Location + New Size(chatRect.Location), chatRect.Size), clientRect)
        If Bounds <> bounded Then Bounds = bounded
        If Not Visible Then Show()
        Invalidate()
    End Sub

    Public Shared Sub DrawTranslatedLines(g As Graphics, entries As IEnumerable(Of ChatOverlayLine), area As Rectangle)
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
        For Each entry In entries
            If entry.SourceBounds.IsEmpty OrElse entry.TranslatedText = entry.SourceText Then Continue For
            Dim text = entry.TranslatedText
            If entry.SpeakerPrefix.Length > 0 Then
                If Not text.StartsWith(entry.SpeakerPrefix, StringComparison.Ordinal) Then Continue For
                text = text.Substring(entry.SpeakerPrefix.Length).TrimStart()
            End If
            text = text.Replace(vbCr, " ").Replace(vbLf, " ")
            ' Keep the original name pixels, baseline, row spacing and font height. Never reflow rows
            ' or reduce the font when a translation is longer; ellipsize within that same row.
            Dim row = Rectangle.Intersect(New Rectangle(entry.MessageLeft, entry.SourceBounds.Top - 1,
                Math.Max(1, area.Right - entry.MessageLeft), entry.SourceBounds.Height + 2), area)
            If row.Width <= 0 OrElse row.Height <= 0 Then Continue For
            Using font As New Font("Tahoma", entry.FontPixelSize, FontStyle.Regular, GraphicsUnit.Pixel),
                  brush As New SolidBrush(entry.BackgroundColor)
                Dim state = g.Save()
                g.SetClip(row)
                Dim translatedWidth = TextRenderer.MeasureText(g, text, font, Size.Empty,
                    TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix).Width
                Dim replacementWidth = Math.Min(row.Width, Math.Max(entry.SourceBounds.Right - row.Left + 1, translatedWidth))
                g.FillRectangle(brush, New Rectangle(row.Left, row.Top, replacementWidth, row.Height))
                Dim textRect As New Rectangle(row.Left, entry.SourceBounds.Top - entry.GlyphTop, row.Width, Math.Max(row.Height, CInt(entry.FontPixelSize * 3)))
                Dim textFlags = TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix Or
                    TextFormatFlags.EndEllipsis Or TextFormatFlags.PreserveGraphicsClipping
                ' Only translated messages change color; speaker names retain their original pixels.
                TextRenderer.DrawText(g, text, font, textRect, Color.FromArgb(255, 165, 0), textFlags)
                g.Restore(state)
            End Using
        Next
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        If _enabled Then DrawTranslatedLines(e.Graphics, _entries, ClientRectangle)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then _timer.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class
