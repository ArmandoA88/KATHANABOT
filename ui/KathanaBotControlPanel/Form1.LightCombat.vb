Partial Public Class Form1
    Private _combatSkillsGroup As GroupBox
    Private _combatSkillHost As Panel
    Private _lightCombatCards As CombatSkillCards
    Private _lightEditorShown As Boolean

    Private Sub UpdateLightCombatEditor()
        If _combatSkillHost Is Nothing OrElse dgvCombat Is Nothing Then Return
        If _lightEditorShown = _directKpEnabled Then Return
        CommitPendingGridEdits()
        _lightEditorShown = _directKpEnabled
        If _directKpEnabled Then
            If _lightCombatCards Is Nothing Then
                _lightCombatCards = New CombatSkillCards(dgvCombat) With {.Dock = DockStyle.Fill, .Tag = "lite-scope"}
                _combatSkillHost.Controls.Add(_lightCombatCards)
            End If
            _lightCombatCards.RefreshCards()
            dgvCombat.Visible = False
            _lightCombatCards.Visible = True
            _lightCombatCards.BringToFront()
            _combatSkillsGroup.Text = "LITE Direct KP — Combat Skills"
            _combatSkillsGroup.ForeColor = Color.FromArgb(212, 170, 88)
        Else
            If _lightCombatCards IsNot Nothing Then _lightCombatCards.Visible = False
            dgvCombat.Visible = True
            dgvCombat.BringToFront()
            _combatSkillsGroup.Text = "Full — Combat Skills"
            _combatSkillsGroup.ForeColor = ThemeAccent
        End If
    End Sub
End Class
