Imports System.Windows.Forms

Module Program
    <STAThread>
    Sub Main()
        Application.EnableVisualStyles()
        CheckDialog(MessageBoxButtons.OK, DialogResult.OK, MessageBoxIcon.Error)
        CheckDialog(MessageBoxButtons.OKCancel, DialogResult.Cancel, MessageBoxIcon.Question)
        CheckDialog(MessageBoxButtons.YesNo, DialogResult.Yes, MessageBoxIcon.Warning)
        CheckDialog(MessageBoxButtons.YesNo, DialogResult.No, MessageBoxIcon.Warning)
        Console.WriteLine("PASS: silent error/warning dialogs retain text, icons and OK/Cancel/Yes/No results.")
    End Sub

    Private Sub CheckDialog(buttons As MessageBoxButtons, expected As DialogResult, icon As MessageBoxIcon)
        Using owner As New Form(), timer As New Timer With {.Interval = 100}
            Dim failure As Exception = Nothing
            AddHandler timer.Tick,
                Sub()
                    timer.Stop()
                    Dim dialog = Application.OpenForms.Cast(Of Form)().FirstOrDefault(Function(f) f.Text = "Silent dialog test")
                    If dialog Is Nothing Then
                        failure = New Exception("Dialog was not shown.")
                        Application.ExitThread()
                        Return
                    End If
                    Try
                        Dim controls = Descendants(dialog).ToList()
                        If Not controls.OfType(Of Label)().Any(Function(label) label.Text = "Warning & details") Then Throw New Exception("Message lost.")
                        If Not controls.OfType(Of PictureBox)().Any(Function(p) p.Image IsNot Nothing) Then Throw New Exception("Icon lost.")
                        controls.OfType(Of Button)().Single(Function(b) b.DialogResult = expected).PerformClick()
                    Catch ex As Exception
                        failure = ex
                        dialog.Close()
                    End Try
                End Sub
            timer.Start()
            Dim actual = SilentMessageBox.Show(owner, "Warning & details", "Silent dialog test", buttons, icon)
            If failure IsNot Nothing Then Throw failure
            If actual <> expected Then Throw New Exception($"Expected {expected}, got {actual}.")
        End Using
    End Sub

    Private Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant In Descendants(child)
                Yield descendant
            Next
        Next
    End Function
End Module
