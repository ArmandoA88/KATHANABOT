Partial Public Class BotEngine
    Private Shared ReadOnly _autoAssistWindows As New HashSet(Of IntPtr)
    Private _autoAssistWindow As IntPtr

    Private Sub UpdateAutoAssistOutput(hwnd As IntPtr, cfg As BotConfig)
        SyncLock _keyOutputPauseSync
            If _autoAssistWindow <> IntPtr.Zero Then _autoAssistWindows.Remove(_autoAssistWindow)
            _autoAssistWindow = If(cfg IsNot Nothing AndAlso cfg.AutoAssistOnlyEnabled, hwnd, IntPtr.Zero)
            If _autoAssistWindow <> IntPtr.Zero Then _autoAssistWindows.Add(_autoAssistWindow)
        End SyncLock
    End Sub
End Class
