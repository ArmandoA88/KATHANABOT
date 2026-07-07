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

    Private Const OverlayMaxFontSize As Single = 10.0F
    Private Const OverlayMinFontSize As Single = 5.5F
    Private Const OverlayFontStep As Single = 0.5F
    Private Const OverlayBlockHorizontalPadding As Integer = 12
    Private Const OverlayBlockGap As Integer = 2
    Private Const OverlayRowHeight As Integer = 18
    Private Shared ReadOnly OverlayTextFlags As TextFormatFlags = TextFormatFlags.SingleLine Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.TextBoxControl

    Private NotInheritable Class OverlayRenderLine
        Public Property Text As String = ""
        Public Property FontSize As Single = OverlayMaxFontSize
    End Class

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
        If Not BotEngine.TryGetClientScreenRect(cfg, clientRect) Then
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
        Dim padding As Integer = 4
        Dim blockWidth As Integer = Math.Max(60, ClientSize.Width - (padding * 2))
        Dim renderLines As List(Of OverlayRenderLine) = BuildRenderLayout(e.Graphics, padding, blockWidth)
        If renderLines.Count = 0 Then
            Return
        End If

        Dim y As Integer = padding

        Using translatedBrush As New SolidBrush(Color.FromArgb(255, 244, 235, 170)),
              panelBrush As New SolidBrush(Color.FromArgb(150, 5, 5, 5))

            For Each renderLine As OverlayRenderLine In renderLines
                Using translatedFont As New Font("Segoe UI Semibold", renderLine.FontSize, FontStyle.Bold)
                    Dim blockRect As New Rectangle(padding, y, blockWidth, OverlayRowHeight)
                    e.Graphics.FillRectangle(panelBrush, blockRect)

                    Dim translatedRect As New Rectangle(
                        blockRect.X + (OverlayBlockHorizontalPadding \ 2),
                        blockRect.Y,
                        Math.Max(1, blockRect.Width - OverlayBlockHorizontalPadding),
                        blockRect.Height)
                    TextRenderer.DrawText(e.Graphics, renderLine.Text, translatedFont, translatedRect, translatedBrush.Color, OverlayTextFlags)
                End Using

                y += OverlayRowHeight + OverlayBlockGap
                If y >= ClientSize.Height Then
                    Exit For
                End If
            Next
        End Using
    End Sub

    Private Function BuildRenderLayout(g As Graphics, padding As Integer, blockWidth As Integer) As List(Of OverlayRenderLine)
        Dim availableHeight As Integer = Math.Max(1, ClientSize.Height - (padding * 2))
        Dim measureWidth As Integer = Math.Max(24, blockWidth - OverlayBlockHorizontalPadding)
        Dim sourceEntries As List(Of ChatOverlayLine) = _entries.
            Where(Function(entry) Not String.IsNullOrWhiteSpace(entry.TranslatedText)).
            ToList()

        While sourceEntries.Count > 0 AndAlso GetRowsHeight(sourceEntries.Count) > availableHeight
            sourceEntries.RemoveAt(0)
        End While

        If sourceEntries.Count = 0 Then
            Return New List(Of OverlayRenderLine)()
        End If

        Dim result As New List(Of OverlayRenderLine)()
        For Each entry As ChatOverlayLine In sourceEntries
            Dim text As String = If(entry.TranslatedText, "").Trim()
            result.Add(New OverlayRenderLine With {
                .Text = text,
                .FontSize = ChooseSingleLineFontSize(g, text, measureWidth)
            })
        Next
        Return result
    End Function

    Private Function ChooseSingleLineFontSize(g As Graphics, text As String, measureWidth As Integer) As Single
        Dim fontSize As Single = OverlayMaxFontSize
        While fontSize >= OverlayMinFontSize
            Using translatedFont As New Font("Segoe UI Semibold", fontSize, FontStyle.Bold)
                Dim measured As Size = TextRenderer.MeasureText(g, If(text, ""), translatedFont, New Size(Integer.MaxValue, Integer.MaxValue), OverlayTextFlags)
                If measured.Width <= measureWidth Then
                    Return fontSize
                End If
            End Using
            fontSize -= OverlayFontStep
        End While

        Return OverlayMinFontSize
    End Function

    Private Shared Function GetRowsHeight(rowCount As Integer) As Integer
        If rowCount <= 0 Then
            Return 0
        End If

        Return (rowCount * OverlayRowHeight) + ((rowCount - 1) * OverlayBlockGap)
    End Function
End Class
