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

    Public Shared Function RequiresForeground(key As String) As Boolean
        Dim value = If(key, "").Trim().ToUpperInvariant()
        Return value.Contains("+") OrElse {"W", "A", "S", "D", "ALT", "LALT", "RALT", "MENU", "LMENU", "RMENU", "CTRL", "CONTROL", "SHIFT"}.Contains(value)
    End Function
End Class
