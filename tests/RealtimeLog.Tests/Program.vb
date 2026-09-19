Imports System.Reflection
Imports System.Drawing
Module Program
    <STAThread>
    Sub Main()
        Dim flags = BindingFlags.Instance Or BindingFlags.NonPublic
        Dim owner = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Using log As New LogProbe With {.ReadOnly = True}
            Dim handle = log.Handle
            GetType(Form1).GetField("rtbLog", flags).SetValue(owner, log)
            Dim append = GetType(Form1).GetMethod("AppendColoredLog", flags)
            Dim trim = GetType(Form1).GetMethod("TrimRealtimeLogIfNeeded", flags)
            Dim lastTrim = GetType(Form1).GetField("_lastLogTrimUtc", flags)
            For cycle = 1 To 4
                ' Simulate hours of output quickly, crossing the actual production threshold.
                log.AppendText(String.Concat(Enumerable.Repeat("[COMBAT] 4/5/7 " & New String("x"c, 100) & vbCrLf, 1500)))
                append.Invoke(owner, {"[Full] Key action: 4 (attack)"})
                Dim latest = "[COMBAT] [Full] Key action: 4 (attack)"
                log.Select(log.TextLength - latest.Length - 1, latest.Length)
                Dim expectedColor = log.SelectionColor
                Dim before = log.TextLength
                lastTrim.SetValue(owner, DateTime.MinValue)
                trim.Invoke(owner, {False})
                Check(log.TextLength <= 120000 AndAlso log.TextLength < before, "Log prefix was not removed.")
                Check(log.Text.EndsWith(latest & vbLf), "Latest line lost.")
                Check(log.Text.StartsWith("[COMBAT]"), "Trim split a retained line.")
                log.Select(log.TextLength - latest.Length - 1, latest.Length)
                Check(log.SelectionColor = expectedColor, "Retained category color lost.")
                Check(log.ReadOnly, "Log became editable.")
                Check(Not log.CanUndo, "Trim retained an undo history.")
                Check(log.ReadOnlyClearAttempts = 0, "Native clear was sent while read-only (Windows ding path).")
            Next
            Dim retained = log.Text
            trim.Invoke(owner, {False})
            Check(log.Text = retained, "Below-threshold log changed.")
            log.Clear()
            trim.Invoke(owner, {True})
            Check(log.TextLength = 0 AndAlso log.ReadOnly, "Empty forced trim failed.")
            Console.WriteLine("PASS: repeated production log trims stay bounded, retain whole lines/colors and newest output, restore read-only state, and never send WM_CLEAR to a read-only control.")
        End Using
    End Sub
    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New Exception(message)
    End Sub
    Private Class LogProbe
        Inherits RichTextBox
        Public ReadOnlyClearAttempts As Integer
        Protected Overrides Sub WndProc(ByRef message As Message)
            If message.Msg = &H303 AndAlso Me.ReadOnly Then ReadOnlyClearAttempts += 1
            MyBase.WndProc(message)
        End Sub
    End Class
End Module
