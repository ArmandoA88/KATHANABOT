Imports System.Drawing
Imports System.Windows.Forms

' Standard MessageBox icons can trigger Windows sounds even without explicit audio playback.
' Keep messages and confirmation results, but render them using an ordinary silent form.
Public NotInheritable Class SilentMessageBox
    Private Sub New()
    End Sub

    Public Shared Function Show(owner As IWin32Window, text As String, caption As String,
                                Optional buttons As MessageBoxButtons = MessageBoxButtons.OK,
                                Optional icon As MessageBoxIcon = MessageBoxIcon.None) As DialogResult
        Using dialog As New Form With {
            .Text = caption, .StartPosition = FormStartPosition.CenterParent,
            .FormBorderStyle = FormBorderStyle.FixedDialog, .MinimizeBox = False,
            .MaximizeBox = False, .ShowInTaskbar = False, .AutoScaleMode = AutoScaleMode.Dpi,
            .ClientSize = New Size(540, 230)
        }
            Dim content As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(16), .ColumnCount = 2, .RowCount = 2}
            content.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 48))
            content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            content.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            content.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
            Dim symbol As Icon = Nothing
            Select Case icon
                Case MessageBoxIcon.Warning : symbol = SystemIcons.Warning
                Case MessageBoxIcon.Error : symbol = SystemIcons.Error
                Case MessageBoxIcon.Information : symbol = SystemIcons.Information
                Case MessageBoxIcon.Question : symbol = SystemIcons.Question
            End Select
            Using picture As Bitmap = If(symbol Is Nothing, Nothing, symbol.ToBitmap())
                content.Controls.Add(New PictureBox With {.Image = picture, .SizeMode = PictureBoxSizeMode.CenterImage, .Size = New Size(40, 40)}, 0, 0)
                Dim messagePanel As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True}
                messagePanel.Controls.Add(New Label With {.Text = text, .AutoSize = True, .MaximumSize = New Size(440, 0), .UseMnemonic = False})
                content.Controls.Add(messagePanel, 1, 0)
                Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft}
                content.Controls.Add(actions, 0, 1)
                content.SetColumnSpan(actions, 2)
                Dim results As DialogResult()
                Select Case buttons
                    Case MessageBoxButtons.OK : results = {DialogResult.OK}
                    Case MessageBoxButtons.OKCancel : results = {DialogResult.OK, DialogResult.Cancel}
                    Case MessageBoxButtons.YesNo : results = {DialogResult.Yes, DialogResult.No}
                    Case Else : Throw New ArgumentOutOfRangeException(NameOf(buttons))
                End Select
                For index As Integer = results.Length - 1 To 0 Step -1
                    Dim result = results(index)
                    Dim button As New Button With {.Text = result.ToString(), .DialogResult = result, .AutoSize = True, .MinimumSize = New Size(85, 30)}
                    actions.Controls.Add(button)
                    If index = 0 Then dialog.AcceptButton = button
                    If index = results.Length - 1 Then dialog.CancelButton = button
                Next
                dialog.Controls.Add(content)
                Return dialog.ShowDialog(owner)
            End Using
        End Using
    End Function
End Class
