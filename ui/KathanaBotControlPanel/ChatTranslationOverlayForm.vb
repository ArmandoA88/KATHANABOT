Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class ChatOverlayLine
    Public Property SourceText As String = ""
    Public Property TranslatedText As String = ""
    Public Property CreatedAtUtc As DateTime = DateTime.UtcNow
End Class

Public Class ChatTranslationOverlayForm
    Inherits Form

    Private Const WS_EX_TOOLWINDOW As Integer = &H80
    Private Const WS_EX_LAYERED As Integer = &H80000
    Private Const WS_EX_TRANSPARENT As Integer = &H20

    Private ReadOnly _configProvider As Func(Of BotConfig)
    Private ReadOnly _timer As New Timer()
    Private _entries As List(Of ChatOverlayLine) = New List(Of ChatOverlayLine)()
    Private _enabled As Boolean

    Public Sub New(configProvider As Func(Of BotConfig))
        _configProvider = configProvider
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        TopMost = True
        StartPosition = FormStartPosition.Manual
        BackColor = Color.Magenta
        TransparencyKey = Color.Magenta
        DoubleBuffered = True

        _timer.Interval = 150
        AddHandler _timer.Tick, AddressOf TickUpdate
        _timer.Start()
    End Sub

    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp As CreateParams = MyBase.CreateParams
            cp.ExStyle = cp.ExStyle Or WS_EX_TOOLWINDOW Or WS_EX_LAYERED Or WS_EX_TRANSPARENT
            Return cp
        End Get
    End Property

    Public Sub UpdateContent(entries As List(Of ChatOverlayLine), enabled As Boolean)
        _enabled = enabled
        _entries = If(entries, New List(Of ChatOverlayLine)()).
            Select(Function(entry) New ChatOverlayLine With {
                .SourceText = entry.SourceText,
                .TranslatedText = entry.TranslatedText,
                .CreatedAtUtc = entry.CreatedAtUtc
            }).
            ToList()

        If Not _enabled OrElse _entries.Count = 0 Then
            Hide()
            Return
        End If

        If Not Visible Then
            Show()
        End If
        Invalidate()
    End Sub

    Private Sub TickUpdate(sender As Object, e As EventArgs)
        If Not _enabled OrElse _entries.Count = 0 OrElse _configProvider Is Nothing Then
            Hide()
            Return
        End If

        Dim cfg As BotConfig = _configProvider.Invoke()
        If cfg Is Nothing Then
            Hide()
            Return
        End If

        Dim clientRect As Rectangle
        If Not BotEngine.TryGetClientScreenRect(cfg.WindowTitle, clientRect) Then
            Hide()
            Return
        End If

        Dim chatRect As RectRegion = If(cfg.ChatRect, New RectRegion(0, 0, 1, 1))
        Dim bounded As New Rectangle(
            clientRect.Left + Math.Max(0, chatRect.X),
            clientRect.Top + Math.Max(0, chatRect.Y),
            Math.Max(1, Math.Min(chatRect.W, clientRect.Width)),
            Math.Max(1, Math.Min(chatRect.H, clientRect.Height)))

        If Bounds <> bounded Then
            Bounds = bounded
        End If

        If Not Visible Then
            Show()
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        If Not _enabled OrElse _entries.Count = 0 Then
            Return
        End If

        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        Dim padding As Integer = 8
        Dim y As Integer = padding
        Dim textWidth As Integer = Math.Max(60, ClientSize.Width - (padding * 2))

        Using translatedFont As New Font("Segoe UI Semibold", 11.0F, FontStyle.Bold),
              sourceFont As New Font("Segoe UI", 8.5F, FontStyle.Regular),
              translatedBrush As New SolidBrush(Color.FromArgb(255, 244, 235, 170)),
              sourceBrush As New SolidBrush(Color.FromArgb(230, 210, 210, 210)),
              panelBrush As New SolidBrush(Color.FromArgb(150, 5, 5, 5))

            For Each entry As ChatOverlayLine In _entries
                Dim translatedSize As Size = TextRenderer.MeasureText(e.Graphics, entry.TranslatedText, translatedFont, New Size(textWidth, 0), TextFormatFlags.WordBreak)
                Dim sourceSize As Size = TextRenderer.MeasureText(e.Graphics, entry.SourceText, sourceFont, New Size(textWidth, 0), TextFormatFlags.WordBreak)
                Dim blockHeight As Integer = translatedSize.Height + sourceSize.Height + 12
                Dim blockRect As New Rectangle(padding, y, textWidth, blockHeight)
                e.Graphics.FillRectangle(panelBrush, blockRect)

                Dim translatedRect As New Rectangle(blockRect.X + 6, blockRect.Y + 4, blockRect.Width - 12, translatedSize.Height + 4)
                Dim sourceRect As New Rectangle(blockRect.X + 6, translatedRect.Bottom, blockRect.Width - 12, sourceSize.Height + 2)
                TextRenderer.DrawText(e.Graphics, entry.TranslatedText, translatedFont, translatedRect, translatedBrush.Color, TextFormatFlags.WordBreak)
                TextRenderer.DrawText(e.Graphics, entry.SourceText, sourceFont, sourceRect, sourceBrush.Color, TextFormatFlags.WordBreak)

                y += blockHeight + 6
                If y >= ClientSize.Height Then
                    Exit For
                End If
            Next
        End Using
    End Sub
End Class
