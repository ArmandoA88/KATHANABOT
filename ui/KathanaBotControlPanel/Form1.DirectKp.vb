Partial Public Class Form1
    Private _directKpEnabled As Boolean
    Private _directKpNoticeAccepted As Boolean
    Private _directKpButton As Button
    Private _directKpInterval As NumericUpDown
    Private _directKpNotice As Form

    Private Const DirectKpWarning As String = "WARNING: LITE Direct KP bypasses bot targeting/retargeting and presses E then R without target recognition. Monster filters and target exclusions cannot protect you; kill counts, loot-after-kill, Max Range/navigation and target-based skills may not work correctly. Configured attacks, healing and other enabled features continue. Main Stop stops LITE Direct KP."

    Private Function BuildDirectKpControls() As Control
        Dim panel As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 3, .RowCount = 2, .Margin = New Padding(0), .BackColor = ThemeSurface, .ForeColor = Color.White, .Tag = "direct-kp-scope"}
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 65))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 35))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 28))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 28))
        _directKpButton = CreateResponsiveCenterButton("LITE Direct KP: OFF", Color.FromArgb(155, 45, 45))
        _directKpButton.FlatStyle = FlatStyle.Flat
        _directKpButton.FlatAppearance.BorderColor = Color.FromArgb(212, 170, 88)
        _directKpButton.FlatAppearance.BorderSize = 1
        AddHandler _directKpButton.Click, AddressOf ToggleDirectKp
        panel.Controls.Add(_directKpButton, 0, 0)
        _directKpInterval = New NumericUpDown With {.Minimum = 200, .Maximum = 60000, .Value = 1000, .Increment = 100, .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .BackColor = ThemeCard, .ForeColor = Color.White}
        AddHandler _directKpInterval.ValueChanged, Sub() PushLiveConfig()
        panel.Controls.Add(_directKpInterval, 1, 0)
        panel.Controls.Add(New Label With {.Text = "ms", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 2, 0)
        Dim warning As New Label With {.Text = "Warning: filters and target-based features may not work correctly.", .Dock = DockStyle.Fill, .ForeColor = Color.Gold, .AutoEllipsis = True, .Margin = New Padding(3, 0, 3, 0)}
        panel.Controls.Add(warning, 0, 1)
        panel.SetColumnSpan(warning, 3)
        Dim tips As New ToolTip With {.AutoPopDelay = 20000}
        tips.SetToolTip(warning, DirectKpWarning)
        tips.SetToolTip(_directKpInterval, "E-to-E cycle in milliseconds. E first, R 100 ms later; minimum 100 ms before the next E. Other input or recovery may extend timing.")
        AddHandler panel.Disposed, Sub() tips.Dispose()
        Return panel
    End Function

    Private Sub ToggleDirectKp(sender As Object, e As EventArgs)
        If _directKpEnabled Then
            DisableDirectKp()
            Return
        End If
        If Not _directKpNoticeAccepted Then
            Using notice As New DirectKpNoticeForm(DirectKpWarning)
                _directKpNotice = notice
                Dim accepted = notice.ShowDialog(Me) = DialogResult.OK
                _directKpNotice = Nothing
                If Not accepted Then Return
                _directKpNoticeAccepted = True
            End Using
        End If
        _directKpEnabled = True
        UpdateDirectKpButton()
        PushLiveConfig()
        SavePersistedListState(False)
        AppendLog("LITE Direct KP enabled: E/R replaces target selection. Monster filters and target-based features may not work correctly.")
    End Sub

    Private Sub DisableDirectKp()
        If _directKpNotice IsNot Nothing Then
            _directKpNotice.DialogResult = DialogResult.Cancel
            _directKpNotice.Close()
        End If
        _directKpEnabled = False
        UpdateDirectKpButton()
        PushLiveConfig()
        SavePersistedListState(False)
        _fullEngine.StopDirectKpWorker()
    End Sub

    Private Sub UpdateDirectKpButton()
        If _directKpButton Is Nothing Then Return
        _directKpButton.Text = If(_directKpEnabled, "LITE Direct KP: ON", "LITE Direct KP: OFF")
        _directKpButton.BackColor = If(_directKpEnabled, Color.FromArgb(20, 155, 75), Color.FromArgb(155, 45, 45))
        UpdateLightCombatEditor()
    End Sub
End Class
