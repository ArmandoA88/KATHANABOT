Public NotInheritable Class BackgroundModePolicy
    Private Shared ReadOnly DisabledFeatures As New Dictionary(Of String, String) From {
        {"AutoLootForceForeground", "Force Game Foreground"}, {"LootScannerEnabled", "Loot scanner / held Alt"},
        {"LootPickupEnabled", "Centered loot pickup (requires the loot scanner)"},
        {"EvadeDadatiEnabled", "Dadati movement"}, {"LevelingAgentEnabled", "Leveling movement"},
        {"NavigationEnabled", "Navigation"}, {"HoldPlaceEnabled", "Max Range movement"},
        {"ArrowUnbundleEnabled", "Arrow unbundling clicks"}, {"AutoPartyInviteEnabled", "Auto Party invite macro"},
        {"AutoPartyMessageEnabled", "Auto Party messages"}, {"PartyAskEnabled", "Ask Party messages"},
        {"AskForResurrectEnabled", "Ask Resurrection messages"}, {"LootRejectClickEnabled", "Loot rejection clicks"},
        {"ResurrectAutoAcceptEnabled", "Auto Resurrect clicks"}, {"PartyInviteAutoAcceptEnabled", "Auto Accept Party"},
        {"PartyRessAutoAcceptEnabled", "Auto Accept Resurrection"}, {"FullSupportTankEnabled", "Tank click targeting"},
        {"FullSupportIndividualEnabled", "Individual click healing"}, {"FullSupportAssistEnabled", "Full Support assist"},
        {"FullSupportPartyHealEnabled", "Full Support party-heal automation"}, {"FullSupportSelfSurvivalEnabled", "Full Support self-target automation"},
        {"FullSupportPartyResurrectEnabled", "Party resurrection targeting"}, {"BuffWatchSelfClickEnabled", "Buff self-click"}}

    Public Shared Function Apply(cfg As BotConfig) As List(Of String)
        Dim disabled As New List(Of String)
        For Each entry In DisabledFeatures
            Dim prop = GetType(BotConfig).GetProperty(entry.Key)
            If CBool(prop.GetValue(cfg)) Then
                prop.SetValue(cfg, False)
                disabled.Add(entry.Value)
            End If
        Next
        If cfg.AutoLootArrowHolds IsNot Nothing Then
            If cfg.AutoLootArrowHolds.LeftEnabled OrElse cfg.AutoLootArrowHolds.RightEnabled Then disabled.Add("Auto-loot arrow holds")
            cfg.AutoLootArrowHolds.LeftEnabled = False
            cfg.AutoLootArrowHolds.RightEnabled = False
        End If
        For Each action In If(cfg.Actions, New List(Of ActionRule))
            If action.Enabled AndAlso RequiresForeground(action.KeyName) Then
                action.Enabled = False
                disabled.Add("Skill " & action.KeyName)
            End If
        Next
        For Each slot In If(cfg.BuffWatchSlots, New List(Of BuffWatchSlot))
            If slot.Enabled AndAlso (slot.SelfClickBeforeCast OrElse RequiresForeground(slot.KeyName)) Then
                slot.Enabled = False
                disabled.Add("Buff " & slot.Name)
            End If
        Next
        Return disabled
    End Function

    Public Shared Function ApplyKeyboardOnly(cfg As BotConfig) As List(Of String)
        ' Apply only to fresh runtime configs; saved UI/profile preferences stay intact.
        Dim disabled As New List(Of String)
        For Each name In {"AutoLootForceForeground", "LootScannerEnabled", "LootPickupEnabled", "ArrowUnbundleEnabled",
                          "AutoPartyInviteEnabled", "AutoPartyMessageEnabled", "PartyAskEnabled", "AskForResurrectEnabled",
                          "LootRejectClickEnabled", "ResurrectAutoAcceptEnabled", "PartyInviteAutoAcceptEnabled",
                          "PartyRessAutoAcceptEnabled", "FullSupportTankEnabled", "FullSupportIndividualEnabled",
                          "FullSupportPartyResurrectEnabled"}
            Dim prop = GetType(BotConfig).GetProperty(name)
            If CBool(prop.GetValue(cfg)) Then
                prop.SetValue(cfg, False)
                disabled.Add(name)
            End If
        Next
        ' Movement, chords, F pickup and keyboard support/self-target actions remain enabled.
        Return disabled
    End Function

    ' Readable name for a feature that ApplyKeyboardOnly disabled (it reports BotConfig property names).
    Public Shared Function DescribeFeature(propertyName As String) As String
        Dim label As String = Nothing
        Return If(DisabledFeatures.TryGetValue(propertyName, label), label, propertyName)
    End Function

    Public Shared Function RequiresForeground(key As String) As Boolean
        Dim value = If(key, "").Trim().ToUpperInvariant()
        Return value.Contains("+") OrElse {"W", "A", "S", "D", "ALT", "LALT", "RALT", "MENU", "LMENU", "RMENU", "CTRL", "CONTROL", "SHIFT"}.Contains(value)
    End Function
End Class
