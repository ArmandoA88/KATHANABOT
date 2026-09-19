Partial Public Class Form1
    Private Shared Function LogAppearance(message As String) As (Kind As String, Ink As Color)
        Dim text = If(message, "").ToLowerInvariant()
        If text.Contains("error") OrElse text.Contains("exception") OrElse text.Contains("failed") OrElse text.Contains("crash") Then Return ("ERROR", Color.FromArgb(255, 125, 125))
        If text.Contains("warning") OrElse text.Contains("retry") OrElse text.Contains("did not find") OrElse text.Contains("timeout") OrElse text.Contains("disconnected") OrElse text.Contains("not responding") OrElse text.Contains("dropped") OrElse text.Contains("glitch") Then Return ("WARNING", Color.FromArgb(255, 193, 92))
        If text.Contains("waiting") OrElse text.Contains("no target") OrElse text.Contains("no attack reason") OrElse text.Contains("paused") OrElse text.Contains("blocked") OrElse text.Contains("cooldown") Then Return ("WAIT", Color.FromArgb(255, 225, 125))
        If text.Contains("kill confirmed") OrElse text.Contains("completed") OrElse text.Contains("success") Then Return ("SUCCESS", Color.FromArgb(120, 235, 155))
        Select Case ClassifyLogMessage(message)
            Case "combat" : Return ("COMBAT", Color.FromArgb(100, 215, 255))
            Case "loot" : Return ("LOOT", Color.FromArgb(210, 170, 255))
            Case "ocr" : Return ("OCR", Color.FromArgb(145, 180, 255))
            Case "navigation" : Return ("NAV", Color.FromArgb(105, 230, 205))
            Case Else : Return ("INFO", Color.FromArgb(225, 230, 240))
        End Select
    End Function

    Private Sub AppendColoredLog(line As String)
        Dim appearance = LogAppearance(line)
        rtbLog.Select(rtbLog.TextLength, 0)
        rtbLog.SelectionColor = appearance.Ink
        rtbLog.AppendText($"[{appearance.Kind}] {line}" & Environment.NewLine)
    End Sub
End Class
