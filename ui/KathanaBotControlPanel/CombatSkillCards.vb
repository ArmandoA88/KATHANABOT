' Alternate editor for the SAME Full combat rows, so switching modes never copies or loses skills.
Public NotInheritable Class CombatSkillCards
    Inherits FlowLayoutPanel
    Private ReadOnly _grid As DataGridView
    Private ReadOnly _cards As New Dictionary(Of DataGridViewRow, SkillCard)

    Public Sub New(grid As DataGridView)
        _grid = grid
        AutoScroll = True
        WrapContents = True
        BackColor = Color.FromArgb(245, 243, 236)
        Padding = New Padding(5)
        AddHandler grid.CellValueChanged, AddressOf GridValueChanged
        AddHandler grid.RowsAdded, AddressOf GridRowsAdded
        AddHandler grid.RowsRemoved, AddressOf GridRowsRemoved
        RefreshCards()
    End Sub

    Public Sub RefreshCards()
        If IsDisposed Then Return
        Dim rows = _grid.Rows.Cast(Of DataGridViewRow)().Where(Function(row) Not row.IsNewRow).ToList()
        If rows.Count <> _cards.Count OrElse rows.Any(Function(row) Not _cards.ContainsKey(row)) Then
            SuspendLayout()
            For Each card In _cards.Values
                card.Dispose()
            Next
            Controls.Clear()
            _cards.Clear()
            For Each row In rows
                Dim card As New SkillCard(row, DirectCast(_grid.Columns("Role"), DataGridViewComboBoxColumn).Items.Cast(Of Object)().ToArray())
                _cards.Add(row, card)
                Controls.Add(card)
            Next
            ResumeLayout()
        End If
        For Each card In _cards.Values
            card.RefreshValues()
        Next
    End Sub
    Private Sub GridValueChanged(sender As Object, e As DataGridViewCellEventArgs)
        RefreshCards()
    End Sub
    Private Sub GridRowsAdded(sender As Object, e As DataGridViewRowsAddedEventArgs)
        RefreshCards()
    End Sub
    Private Sub GridRowsRemoved(sender As Object, e As DataGridViewRowsRemovedEventArgs)
        RefreshCards()
    End Sub
    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            RemoveHandler _grid.CellValueChanged, AddressOf GridValueChanged
            RemoveHandler _grid.RowsAdded, AddressOf GridRowsAdded
            RemoveHandler _grid.RowsRemoved, AddressOf GridRowsRemoved
        End If
        MyBase.Dispose(disposing)
    End Sub

    Private NotInheritable Class SkillCard
        Inherits Panel
        Private ReadOnly _row As DataGridViewRow
        Private ReadOnly _enabled As CheckBox
        Private ReadOnly _role As ComboBox
        Private ReadOnly _editors As New Dictionary(Of String, TextBox)
        Private _refreshing As Boolean
        Public Sub New(row As DataGridViewRow, roles As Object())
            _row = row
            Size = New Size(148, 166)
            Margin = New Padding(4)
            BackColor = Color.White
            ForeColor = Color.FromArgb(45, 45, 45)
            Font = New Font("Segoe UI", 8)
            _enabled = New CheckBox With {.Location = New Drawing.Point(5, 5), .Size = New Size(138, 26), .BackColor = Color.FromArgb(141, 112, 71), .ForeColor = Color.White, .Font = New Font("Segoe UI", 9, FontStyle.Bold)}
            Controls.Add(_enabled)
            _role = New ComboBox With {.Location = New Drawing.Point(5, 36), .Width = 138, .DropDownStyle = ComboBoxStyle.DropDownList}
            _role.Items.AddRange(roles)
            Controls.Add(_role)
            AddEditor("CooldownSec", "CD sec", 66)
            AddEditor("Priority", "Priority", 96)
            AddEditor("TriggerPercent", "At %", 126)
            AddHandler _enabled.CheckedChanged, Sub() WriteValue("Enabled", _enabled.Checked)
            AddHandler _role.SelectedIndexChanged, Sub() If _role.SelectedItem IsNot Nothing Then WriteValue("Role", _role.SelectedItem)
            RefreshValues()
        End Sub
        Private Sub AddEditor(column As String, caption As String, top As Integer)
            Controls.Add(New Label With {.Text = caption, .Location = New Drawing.Point(5, top + 3), .Size = New Size(58, 20)})
            Dim editor As New TextBox With {.Name = column, .Location = New Drawing.Point(64, top), .Width = 79}
            _editors.Add(column, editor)
            Controls.Add(editor)
            AddHandler editor.TextChanged,
                Sub()
                    If _refreshing Then Return
                    Dim value As Decimal
                    Dim valid = Decimal.TryParse(editor.Text, value) AndAlso value >= 0
                    If column <> "CooldownSec" Then valid = valid AndAlso value = Decimal.Truncate(value) AndAlso value <= Integer.MaxValue
                    If column = "TriggerPercent" Then valid = valid AndAlso value >= 1 AndAlso value <= 100
                    editor.BackColor = If(valid, Color.White, Color.MistyRose)
                    If valid Then WriteValue(column, editor.Text)
                End Sub
            AddHandler editor.Validated, Sub() RefreshValues()
        End Sub
        Private Sub WriteValue(column As String, value As Object)
            If Not _refreshing AndAlso Not Equals(_row.Cells(column).Value, value) Then _row.Cells(column).Value = value
        End Sub
        Public Sub RefreshValues()
            _refreshing = True
            Try
                _enabled.Text = "Slot " & Convert.ToString(_row.Cells("Key").Value)
                _enabled.Checked = _row.Cells("Enabled").Value IsNot Nothing AndAlso Convert.ToBoolean(_row.Cells("Enabled").Value)
                BackColor = If(_enabled.Checked, Color.FromArgb(202, 238, 214), Color.White)
                _enabled.BackColor = If(Not _row.Cells("Key").ReadOnly, Color.FromArgb(87, 57, 125), If(_enabled.Checked, Color.FromArgb(25, 96, 55), Color.FromArgb(90, 90, 90)))
                _role.SelectedItem = _row.Cells("Role").Value
                For Each pair In _editors
                    Dim text = Convert.ToString(_row.Cells(pair.Key).Value)
                    If pair.Value.Text <> text Then pair.Value.Text = text
                Next
            Finally
                _refreshing = False
            End Try
        End Sub
    End Class
End Class
