Partial Public Class Form1
    Private _surfaceRefreshQueued As Boolean
    Private Sub QueueMainSurfaceRefresh()
        If Not IsHandleCreated OrElse IsDisposed OrElse _surfaceRefreshQueued Then Return
        _surfaceRefreshQueued = True
        BeginInvoke(New Action(Sub()
                                   _surfaceRefreshQueued = False
                                   If IsDisposed OrElse _mainTabs Is Nothing Then Return
                                   PerformLayout()
                                   pnlWindowFrame.PerformLayout()
                                   _mainTabs.PerformLayout()
                                   Dim page = _mainTabs.SelectedTab
                                   If page IsNot Nothing Then
                                       page.Bounds = _mainTabs.DisplayRectangle
                                       page.PerformLayout()
                                   End If
                                   SyncSidebarRailOverlayBounds()
                                   UpdateTabIndicatorTarget()
                                   RefreshDashboardFromEngine()
                                   pnlWindowFrame.Refresh()
                               End Sub))
    End Sub

    Private _autoAssistOnlyEnabled As Boolean
    Private _autoAssistButton As Button
    Private Function BuildAutoAssistButton() As Control
        _autoAssistButton = CreateResponsiveCenterButton("AUTO-ASSIST ONLY: OFF", Color.FromArgb(155, 45, 45))
        _autoAssistButton.Tag = "direct-kp-scope"
        _autoAssistButton.FlatStyle = FlatStyle.Flat
        AddHandler _autoAssistButton.Click, AddressOf ToggleAutoAssistOnly
        Return _autoAssistButton
    End Function

    Private Sub ToggleAutoAssistOnly(sender As Object, e As EventArgs)
        If Not _autoAssistOnlyEnabled Then
            If Not ConfirmAutoAssist("AUTO-ASSIST ONLY - warning", "This disables ALL automated E presses for the selected game, including normal/forced retargeting, E skill rows and Direct KP's E. You must select a target manually or use another assist key. The bot may remain without a target; monster filters and stuck-target recovery cannot switch targets using E. Other keys keep their existing rules; Direct KP's R continues.", "Continue") Then Return
            If Not ConfirmAutoAssist("Are you sure?", "Enable AUTO-ASSIST ONLY and block automated E presses? Other keys remain enabled under their normal conditions. Turn this toggle off to restore E. This setting resets when the app closes.", "Yes, enable") Then Return
        End If
        _autoAssistOnlyEnabled = Not _autoAssistOnlyEnabled
        _autoAssistButton.Text = If(_autoAssistOnlyEnabled, "AUTO-ASSIST ONLY: ON", "AUTO-ASSIST ONLY: OFF")
        _autoAssistButton.BackColor = If(_autoAssistOnlyEnabled, Color.FromArgb(20, 155, 75), Color.FromArgb(155, 45, 45))
        PushLiveConfig()
        AppendLog(If(_autoAssistOnlyEnabled, "AUTO-ASSIST ONLY enabled: automated E blocked; all other keys keep their normal rules.", "AUTO-ASSIST ONLY disabled: automated E restored."))
    End Sub

    Private Function ConfirmAutoAssist(title As String, message As String, acceptText As String) As Boolean
        Using dialog As New Form With {.Text = title, .StartPosition = FormStartPosition.CenterParent, .ClientSize = New Size(600, 270), .BackColor = ThemeSurface, .ForeColor = Color.Gainsboro, .Font = New Font("Segoe UI", 10), .FormBorderStyle = FormBorderStyle.FixedDialog, .MaximizeBox = False, .MinimizeBox = False, .ShowInTaskbar = False}
            Dim body As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(18), .ColumnCount = 1, .RowCount = 2}
            body.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            body.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
            body.Controls.Add(New Label With {.Text = message, .Dock = DockStyle.Fill}, 0, 0)
            Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft}
            Dim cancel As New Button With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .AutoSize = True, .FlatStyle = FlatStyle.Flat, .BackColor = ThemeCard}
            Dim accept As New Button With {.Text = acceptText, .DialogResult = DialogResult.OK, .AutoSize = True, .FlatStyle = FlatStyle.Flat, .BackColor = Color.FromArgb(20, 110, 70)}
            actions.Controls.AddRange({cancel, accept})
            body.Controls.Add(actions, 0, 1)
            dialog.Controls.Add(body)
            dialog.CancelButton = cancel
            dialog.AcceptButton = cancel
            Return dialog.ShowDialog(Me) = DialogResult.OK
        End Using
    End Function
End Class
