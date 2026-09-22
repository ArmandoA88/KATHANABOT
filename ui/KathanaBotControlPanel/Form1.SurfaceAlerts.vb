Partial Public Class Form1
    Private ReadOnly _surfaceAlertTimer As New System.Windows.Forms.Timer With {.Interval = 600}
    Private _surfaceAlertPhase As Boolean
    Private _lastSurfaceAlert As Integer = -1
    Private _lastAlertTab As TabPage

    Private Sub InitializeSurfaceAlerts()
        AddHandler _surfaceAlertTimer.Tick, Sub() RefreshSurfaceAlerts()
        AddHandler Me.FormClosed, Sub()
                                      _surfaceAlertTimer.Stop()
                                      _surfaceAlertTimer.Dispose()
                                  End Sub
        _surfaceAlertTimer.Start()
        RefreshSurfaceAlerts()
    End Sub

    Private Sub RefreshSurfaceAlerts()
        If Not _themeSnapshotCaptured OrElse IsDisposed Then Return
        Dim edition = GetRunningEdition()
        Dim status = GetEngineForEdition(edition.GetValueOrDefault(BotEdition.Full)).GetStatus()
        _surfaceAlertPhase = Not _surfaceAlertPhase
        Dim state = GetSurfaceAlertState(status, edition.HasValue, _dashboardTab.Visible, _surfaceAlertPhase)
        If state = _lastSurfaceAlert AndAlso _lastAlertTab Is _mainTabs.SelectedTab Then Return
        _lastSurfaceAlert = state
        _lastAlertTab = _mainTabs.SelectedTab
        CaptureThemeSnapshot(Me)
        If state = 0 Then
            RestoreThemeSnapshot(Me)
        Else
            ApplyTintRecursive(Me, Color.FromArgb(220, 25, 35), If(state = 2, 0.66, If(state = 3, 0.18, 0.32)))
        End If
        UpdateDashboardRunButton()
        UpdateTaskbarStatusIndicator()
        Invalidate(True)
    End Sub

    Private Shared Function GetSurfaceAlertState(status As BotStatus, running As Boolean, homeVisible As Boolean, phase As Boolean) As Integer
        Dim dead = status IsNot Nothing AndAlso status.WindowFound AndAlso
            String.IsNullOrWhiteSpace(status.ErrorMessage) AndAlso status.HpPercent >= 0 AndAlso
            status.HpPercent <= DeadZeroThreshold
        Return If(dead AndAlso homeVisible, If(phase, 2, 3), If(running, 0, 1))
    End Function
End Class
