Partial Public Class Form1
    Private _applyingSettings As Boolean

    Private Sub CloseCalibrationEditors()
        If _overlayForm IsNot Nothing Then _overlayForm.Close()
        If _liteOverlayForm IsNot Nothing Then _liteOverlayForm.Close()
        _isDraggingSnapshotRegion = False
        _snapshotDragMode = SnapshotDragMode.None
    End Sub

    Private Sub FormatCalibrationRegionName(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If dgvRegions.Columns(e.ColumnIndex).Name <> "Region" OrElse e.Value Is Nothing Then Return
        Select Case e.Value.ToString()
            Case "mob_hp_rect"
                e.Value = "Mob HP - Red Bar"
                e.FormattingApplied = True
            Case "mob_life_rect"
                e.Value = "Mob HP - Numbers (OCR)"
                e.FormattingApplied = True
        End Select
    End Sub

    Private Function BuildProfileSettingsButtons() As Control
        Dim row As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0)}
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 55))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 45))
        row.Controls.Add(btnProfiles, 0, 0)
        Dim reset = CreateResponsiveCenterButton("Reset All Settings", Color.FromArgb(155, 45, 45))
        AddHandler reset.Click, AddressOf ResetAllSettingsClicked
        row.Controls.Add(reset, 1, 0)
        Return row
    End Function

    Private Sub ResetAllSettingsClicked(sender As Object, e As EventArgs)
        If _tradeRunning OrElse _tradeAnalyzing Then
            SilentMessageBox.Show(Me, "Stop Trade before resetting settings.", "Reset All Settings")
            Return
        End If
        If Not ConfirmAutoAssist("Reset all settings?", "Do you really want to reset all settings?" & Environment.NewLine & Environment.NewLine &
            "The bot will stop. All features and overlays will be disabled, calibrations and lists cleared, and cooldowns reset to their lowest supported values. Saved character profiles will be kept. This will detach the current profile.", "Yes, reset all") Then Return
        Try
            Dim defaults = BuildEmptySettingsJson()
            StopEdition(BotEdition.Full, False, "reset settings")
            StopEdition(BotEdition.Lite, False, "reset settings")
            StopResu("reset settings")
            _quizScanTimer.Stop()
            _quizCancellation?.Cancel()
            CloseCalibrationEditors()
            If _resuOverlay IsNot Nothing Then _resuOverlay.Close()
            _persistDebounceTimer.Stop()
            _pendingPersistState = Nothing
            Threading.Interlocked.Increment(_persistWriteRevision)
            _autoAssistOnlyEnabled = False
            _directKpEnabled = False
            LoadPersistedListState(defaults)
            _applyingSettings = True
            Try
                For Each surface As Control In {_liteTab, _combatTab, _visionTab, _autoPotTab, _autoLootTab, _fullSupportTab, _levelingTab, _holdPlaceTab, _buffWatchTab, _diagnosticsTab, _quizTab, _resuTab, _tradeTab, _updateTab}
                    ClearSetupControls(surface)
                Next
                For Each row As DataGridViewRow In dgvRegions.Rows
                    If row.IsNewRow Then Continue For
                    row.Cells("Enabled").Value = False
                    row.Cells("X").Value = "0"
                    row.Cells("Y").Value = "0"
                    row.Cells("W").Value = "1"
                    row.Cells("H").Value = "1"
                Next
                For Each row As DataGridViewRow In dgvCombat.Rows
                    If row.IsNewRow Then Continue For
                    row.Cells("Enabled").Value = False
                    row.Cells("CooldownSec").Value = "1"
                    row.Cells("Priority").Value = "1"
                    row.Cells("TriggerPercent").Value = "1"
                Next
            Finally
                _applyingSettings = False
            End Try
            _activeProfileName = ""
            UpdateProfilesButtonAppearance()
            UpdateDirectKpButton()
            If _autoAssistButton IsNot Nothing Then
                _autoAssistButton.Text = "AUTO-ASSIST ONLY: OFF"
                _autoAssistButton.BackColor = Color.FromArgb(155, 45, 45)
            End If
            PushLiveConfig()
            SavePersistedListState(True, True)
            QueueMainSurfaceRefresh()
            AppendLog("All features disabled and current setup cleared. Saved profiles retained.")
        Catch ex As Exception
            SilentMessageBox.Show(Me, ex.Message, "Reset All Settings")
        End Try
    End Sub

    Private Shared Function BuildEmptySettingsJson() As String
        Dim state As New PersistedAppState()
        state.Full.SavedConfig = New BotConfig()
        Dim node = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(state))
        ClearSettingsNode(node)
        ' Keep application identity and schema selectors usable; none enable automation.
        node("WindowTitle") = DefaultGameWindowTitle
        node("UpdateRepositoryUrl") = DefaultUpdateRepositoryUrl
        node("DashboardMode") = "Compact"
        node("Full")("MonsterFilterMode") = "blacklist"
        node("Full")("LootDefaultsVersion") = DefaultLootItems.Version
        Return node.ToJsonString()
    End Function

    Private Shared Sub ClearSettingsNode(node As System.Text.Json.Nodes.JsonNode)
        Dim obj = TryCast(node, System.Text.Json.Nodes.JsonObject)
        If obj Is Nothing Then Return
        For Each key In obj.Select(Function(pair) pair.Key).ToArray()
            Dim child = obj(key)
            If TypeOf child Is System.Text.Json.Nodes.JsonObject Then
                ClearSettingsNode(child)
            ElseIf TypeOf child Is System.Text.Json.Nodes.JsonArray Then
                obj(key) = New System.Text.Json.Nodes.JsonArray()
            ElseIf child IsNot Nothing Then
                Select Case child.GetValueKind()
                    Case System.Text.Json.JsonValueKind.True, System.Text.Json.JsonValueKind.False
                        obj(key) = False
                    Case System.Text.Json.JsonValueKind.String
                        obj(key) = ""
                    Case System.Text.Json.JsonValueKind.Number
                        obj(key) = If(key = "X" OrElse key = "Y" OrElse key.EndsWith("X") OrElse key.EndsWith("Y"), 0, 1)
                End Select
            End If
        Next
    End Sub

    Private Sub ClearSetupControls(parent As Control)
        If parent Is Nothing Then Return
        For Each control As Control In parent.Controls
            If TypeOf control Is CheckBox Then
                DirectCast(control, CheckBox).Checked = False
            ElseIf TypeOf control Is NumericUpDown Then
                Dim number = DirectCast(control, NumericUpDown)
                number.Value = Math.Max(number.Minimum, Math.Min(number.Maximum, If(number.Minimum <= 0, 0D, 1D)))
                Continue For
            ElseIf TypeOf control Is TextBox Then
                Dim box = DirectCast(control, TextBox)
                If Not box.ReadOnly AndAlso box IsNot txtUpdateRepositoryUrl Then box.Clear()
            ElseIf TypeOf control Is RichTextBox Then
                Dim box = DirectCast(control, RichTextBox)
                If Not box.ReadOnly Then box.Clear()
            End If
            ClearSetupControls(control)
        Next
    End Sub
End Class
