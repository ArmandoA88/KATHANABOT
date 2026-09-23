Partial Public Class Form1
    Private nudLootAfterKillHold As NumericUpDown

    Private Function BuildLootAfterKillControls() As Control
        Dim row As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 3, .Margin = New Padding(0)}
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 66))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 54))
        nudLootAfterKillHold = New NumericUpDown With {.Minimum = 0.05D, .Maximum = 5D, .DecimalPlaces = 2, .Increment = 0.1D, .Value = 1D, .Dock = DockStyle.Fill, .Anchor = AnchorStyles.Left Or AnchorStyles.Right}
        AddHandler nudLootAfterKillHold.ValueChanged, Sub()
                                                          PushLiveConfig()
                                                          SavePersistedListState(False)
                                                      End Sub
        row.Controls.Add(btnLootAfterKill, 0, 0)
        row.Controls.Add(nudLootAfterKillHold, 1, 0)
        row.Controls.Add(New Label With {.Text = "hold (s)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 2, 0)
        Return row
    End Function

    Private Function BuildSkillAddControls() As Control
        Dim bar As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .WrapContents = True}
        Dim add As New Button With {.Text = "+ Add skill", .AutoSize = True}
        AddHandler add.Click, Sub()
                                  Using dialog As New Form With {.Text = "Add skill", .ClientSize = New Size(340, 120), .StartPosition = FormStartPosition.CenterParent, .FormBorderStyle = FormBorderStyle.FixedDialog, .MinimizeBox = False, .MaximizeBox = False}
                                      Dim key As New ComboBox With {.Location = New System.Drawing.Point(15, 15), .Width = 305, .DropDownStyle = ComboBoxStyle.DropDownList}
                                      For Each modifier In {"CTRL", "ALT"}
                                          For digit = 0 To 9
                                              key.Items.Add(modifier & "+" & digit)
                                          Next
                                      Next
                                      key.SelectedIndex = 1
                                      Dim ok As New Button With {.Text = "Add", .Location = New System.Drawing.Point(145, 65), .DialogResult = DialogResult.OK}
                                      Dim cancel As New Button With {.Text = "Cancel", .Location = New System.Drawing.Point(235, 65), .DialogResult = DialogResult.Cancel}
                                      dialog.Controls.AddRange({key, ok, cancel})
                                      dialog.AcceptButton = ok
                                      dialog.CancelButton = cancel
                                      If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                                      Dim row = AddEditableSkillRow(key.Text)
                                      dgvCombat.FirstDisplayedScrollingRowIndex = row.Index
                                      SavePersistedListState(False)
                                  End Using
                              End Sub
        bar.Controls.Add(add)
        bar.Controls.Add(New Label With {.Text = "Green = enabled   •   Purple key = editable", .AutoSize = True, .Margin = New Padding(8, 7, 0, 0)})
        Return bar
    End Function

    Private Function AddEditableSkillRow(key As String) As DataGridViewRow
        Dim wasApplying = _applyingSettings
        _applyingSettings = True
        Try
            Dim index = dgvCombat.Rows.Add(False, key, "1", "attack", 100, 1)
            Dim row = dgvCombat.Rows(index)
            row.Cells("Key").ReadOnly = False
            dgvCombat.InvalidateRow(index)
            Return row
        Finally
            _applyingSettings = wasApplying
        End Try
    End Function

    Private Sub FormatCombatSkill(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 Then Return
        Dim row = dgvCombat.Rows(e.RowIndex)
        Dim enabled = row.Cells("Enabled").Value IsNot Nothing AndAlso Convert.ToBoolean(row.Cells("Enabled").Value)
        Dim editable = dgvCombat.Columns(e.ColumnIndex).Name = "Key" AndAlso Not row.Cells("Key").ReadOnly
        e.CellStyle.BackColor = If(editable, Color.FromArgb(87, 57, 125), If(enabled, Color.FromArgb(25, 96, 55), ThemeSurface))
        e.CellStyle.ForeColor = Color.White
        e.CellStyle.SelectionBackColor = If(editable, Color.FromArgb(120, 78, 168), If(enabled, Color.FromArgb(36, 132, 75), Color.FromArgb(60, 70, 90)))
        e.CellStyle.SelectionForeColor = Color.White
    End Sub
End Class
