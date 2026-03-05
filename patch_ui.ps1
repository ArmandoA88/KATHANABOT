$formPath = "c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\Form1.vb"
$enginePath = "c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\BotEngine.vb"
$configPath = "c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\BotConfig.vb"

$formContent = Get-Content -Raw -Path $formPath -Encoding UTF8
$engineContent = Get-Content -Raw -Path $enginePath -Encoding UTF8
$configContent = Get-Content -Raw -Path $configPath -Encoding UTF8

# --- 1. Modify BotConfig.vb ---
$configInsert = @"
    Public Property PartyAskText As String = ""
    Public Property LootScannerEnabled As Boolean = True
    Public Property ItemNtfyTopic As String = ""
"@
$configContent = $configContent -replace 'Public Property PartyAskText As String = ""', $configInsert

# --- 2. Modify Form1.vb ---
# Add properties and variables
$formVars = @"
    Private _partyAskEnabled As Boolean = False
    Private _lootScannerEnabled As Boolean = True
"@
$formContent = $formContent -replace 'Private _partyAskEnabled As Boolean = False', $formVars

$formControls = @"
    Private btnPartyAsk As Button
    Private btnLootScanner As Button
    Private txtItemNtfyTopic As TextBox
"@
$formContent = $formContent -replace 'Private btnPartyAsk As Button', $formControls

# Update UI Tick for Diagnostic
$formTick = @"
            `$"AutoAskPartyText: {GetPartyAskCommandText()}{Environment.NewLine}`" &
            `$"LootScannerEnabled: {_lootScannerEnabled}{Environment.NewLine}`" &
"@
$formContent = $formContent -replace '`\$"AutoAskPartyText: {GetPartyAskCommandText\(\)}{Environment\.NewLine}`" &', $formTick

# Insert txtItemNtfyTopic into AutoPot Tab (below general NtfyTopic)
$autoPotOld = @"
        layout.Controls.Add(New Label() With {.Text = "ntfy Channel", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 3)
        txtNtfyTopic = New TextBox() With {.Dock = DockStyle.Fill, .Text = DefaultNtfyTopicName}
        layout.Controls.Add(txtNtfyTopic, 1, 3)
"@
$autoPotNew = @"
        layout.Controls.Add(New Label() With {.Text = "ntfy Channel (Global)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 3)
        txtNtfyTopic = New TextBox() With {.Dock = DockStyle.Fill, .Text = DefaultNtfyTopicName}
        layout.Controls.Add(txtNtfyTopic, 1, 3)

        layout.Controls.Add(New Label() With {.Text = "ntfy Channel (Items)", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 4)
        txtItemNtfyTopic = New TextBox() With {.Dock = DockStyle.Fill, .Text = ""}
        layout.Controls.Add(txtItemNtfyTopic, 1, 4)
"@
$formContent = $formContent -replace [regex]::Escape($autoPotOld), $autoPotNew
$formContent = $formContent -replace 'layout.Controls.Add\(buttonRow, 1, 4\)', 'layout.Controls.Add(buttonRow, 1, 5)'
$formContent = $formContent -replace 'layout.SetRowSpan\(note, 5\)', 'layout.SetRowSpan(note, 6)'
$formContent = $formContent -replace 'layout.RowCount = 5', 'layout.RowCount = 6'

# Insert btnLootScanner into CenterControlPanel (Below btnPartyAsk)
$centerOld = @"
        btnHelp = New Button() With {
            .Text = "Help (EN/ES/FIL)",
            .Top = 662,
"@
$centerNew = @"
        btnLootScanner = New Button() With {
            .Text = If(_lootScannerEnabled, "Loot Scanner (Alt): ON", "Loot Scanner (Alt): OFF"),
            .Top = 662,
            .Left = 8,
            .Width = 210,
            .Height = 38,
            .BackColor = If(_lootScannerEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45)),
            .ForeColor = Color.White
        }
        btnHelp = New Button() With {
            .Text = "Help (EN/ES/FIL)",
            .Top = 712,
"@
$formContent = $formContent -replace [regex]::Escape($centerOld), $centerNew

$clickEvents = @"
        AddHandler btnPartyAsk.Click, AddressOf TogglePartyAskClicked
        AddHandler btnLootScanner.Click, AddressOf ToggleLootScannerClicked
"@
$formContent = $formContent -replace 'AddHandler btnPartyAsk\.Click, AddressOf TogglePartyAskClicked', $clickEvents

$addControls = @"
        panel.Controls.Add(btnPartyAsk)
        panel.Controls.Add(btnLootScanner)
"@
$formContent = $formContent -replace 'panel\.Controls\.Add\(btnPartyAsk\)', $addControls

# Add Toggle Event Handler
$toggleLogic = @"
    Private Sub TogglePartyAskClicked(sender As Object, e As EventArgs)
        _partyAskEnabled = Not _partyAskEnabled
        btnPartyAsk.Text = If(_partyAskEnabled, "Auto Ask Party (add): ON", "Auto Ask Party (add): OFF")
        btnPartyAsk.BackColor = If(_partyAskEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        PushLiveConfig()
        SavePersistedListState(False)
    End Sub

    Private Sub ToggleLootScannerClicked(sender As Object, e As EventArgs)
        _lootScannerEnabled = Not _lootScannerEnabled
        btnLootScanner.Text = If(_lootScannerEnabled, "Loot Scanner (Alt): ON", "Loot Scanner (Alt): OFF")
        btnLootScanner.BackColor = If(_lootScannerEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
        PushLiveConfig()
        SavePersistedListState(False)
    End Sub
"@
$formContent = $formContent -replace '(?s)    Private Sub TogglePartyAskClicked.*?End Sub', $toggleLogic

# Update BuildConfig
$buildConfig = @"
        cfg.PartyAskText = GetPartyAskCommandText()
        cfg.LootScannerEnabled = _lootScannerEnabled
        cfg.ItemNtfyTopic = If(txtItemNtfyTopic IsNot Nothing, txtItemNtfyTopic.Text.Trim(), "")
"@
$formContent = $formContent -replace 'cfg\.PartyAskText = GetPartyAskCommandText\(\)', $buildConfig

# Load/Save Persistence
$savePersist = @"
                .AskForPartyText = GetPartyAskCommandText(),
                .LootScannerEnabled = _lootScannerEnabled,
                .ItemNtfyTopic = If(txtItemNtfyTopic IsNot Nothing, txtItemNtfyTopic.Text.Trim(), ""),
"@
$formContent = $formContent -replace '\.AskForPartyText = GetPartyAskCommandText\(\),', $savePersist

$loadPersist = @"
            If txtPartyAskText IsNot Nothing Then
                txtPartyAskText.Text = If(String.IsNullOrWhiteSpace(state.AskForPartyText), DefaultPartyAskCommand, state.AskForPartyText.Trim())
            End If
            UpdatePartyAskButton()

            _lootScannerEnabled = state.LootScannerEnabled
            If btnLootScanner IsNot Nothing Then
                btnLootScanner.Text = If(_lootScannerEnabled, "Loot Scanner (Alt): ON", "Loot Scanner (Alt): OFF")
                btnLootScanner.BackColor = If(_lootScannerEnabled, Color.FromArgb(35, 130, 80), Color.FromArgb(110, 45, 45))
            End If
            If txtItemNtfyTopic IsNot Nothing Then
                txtItemNtfyTopic.Text = If(state.ItemNtfyTopic, "").Trim()
            End If
"@
$formContent = $formContent -replace '(?s)            If txtPartyAskText IsNot Nothing Then.*?UpdatePartyAskButton\(\)', $loadPersist

# Modify PersistedListState in Form1.vb to add these properties
$stateInsert = @"
    Public Property AskForPartyText As String
    Public Property LootScannerEnabled As Boolean = True
    Public Property ItemNtfyTopic As String
"@
$formContent = $formContent -replace 'Public Property AskForPartyText As String', $stateInsert

# --- 3. Modify BotEngine.vb ---
# Update Loot Scanning logic to check cfg.LootScannerEnabled
$engineOld = @"
            Dim activeHwnd As IntPtr = NativeMethods.GetForegroundWindow()
            If activeHwnd = hwnd AndAlso (now - _lastRightAltAt).TotalMilliseconds >= 10000 Then
"@
$engineNew = @"
            Dim activeHwnd As IntPtr = NativeMethods.GetForegroundWindow()
            If cfg.LootScannerEnabled AndAlso activeHwnd = hwnd AndAlso (now - _lastRightAltAt).TotalMilliseconds >= 10000 Then
"@
$engineContent = $engineContent -replace [regex]::Escape($engineOld), $engineNew

# Wire Ntfy into the loot scanner
$ntfyOld = @"
                                        If normItem <> "" AndAlso normOcr.Contains(normItem) Then
                                            System.Media.SystemSounds.Exclamation.Play()
                                            Console.Beep(800, 1000)
                                            Console.Beep(800, 1000)
                                            RaiseEvent LogLine("LOOT ALARM: Found " & item)
                                            Exit For
                                        End If
"@
$ntfyNew = @"
                                        If normItem <> "" AndAlso normOcr.Contains(normItem) Then
                                            System.Media.SystemSounds.Exclamation.Play()
                                            Console.Beep(800, 1000)
                                            Console.Beep(800, 1000)
                                            RaiseEvent LogLine("LOOT ALARM: Found " & item)
                                            
                                            Dim topic As String = cfg.ItemNtfyTopic
                                            If Not String.IsNullOrWhiteSpace(topic) Then
                                                Task.Run(Async Function()
                                                    Try
                                                        Using client As New System.Net.Http.HttpClient()
                                                            Dim request As New System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "https://ntfy.sh/" & Uri.EscapeDataString(topic))
                                                            request.Content = New System.Net.Http.StringContent("Found important item: " & item)
                                                            request.Headers.Add("Title", "KathanaBot Loot Finder")
                                                            Await client.SendAsync(request)
                                                        End Using
                                                    Catch ex As Exception
                                                        RaiseEvent LogLine("Item Ntfy send failed: " & ex.Message)
                                                    End Try
                                                End Function)
                                            End If
                                            
                                            Exit For
                                        End If
"@
$engineContent = $engineContent -replace [regex]::Escape($ntfyOld), $ntfyNew

Set-Content -Path $formPath -Value $formContent -Encoding UTF8 -NoNewline
Set-Content -Path $enginePath -Value $engineContent -Encoding UTF8 -NoNewline
Set-Content -Path $configPath -Value $configContent -Encoding UTF8 -NoNewline

Write-Host "Patch complete."
