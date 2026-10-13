Partial Public Class Form1
    Private _partyAskTextLabel As Label
    Private _partyAskRegularText As String = PartyAskMessageCollection.DefaultRegularMessage
    Private _partyAskCollectionText As String = PartyAskMessageCollection.DefaultMessage
    Private _partyAskShowingCollection As Boolean
    Private _partyAskUpdatingEditor As Boolean

    Private Sub CapturePartyAskEditorText()
        If _partyAskUpdatingEditor OrElse txtPartyAskText Is Nothing Then Return
        If _partyAskShowingCollection Then
            _partyAskCollectionText = NormalizePartyAskCollectionText(txtPartyAskText.Text)
        Else
            _partyAskRegularText = PartyAskMessageCollection.NormalizeRegular(txtPartyAskText.Text)
        End If
    End Sub

    Private Shared Function NormalizePartyAskCollectionText(rawText As String) As String
        Dim cleaned = If(rawText, "").Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        Return If(cleaned.Length = 0, PartyAskMessageCollection.DefaultMessage, cleaned)
    End Function

    Private Function GetRegularPartyAskText() As String
        CapturePartyAskEditorText()
        Return PartyAskMessageCollection.NormalizeRegular(_partyAskRegularText)
    End Function

    Private Function GetPartyAskCollectionText() As String
        CapturePartyAskEditorText()
        Return NormalizePartyAskCollectionText(_partyAskCollectionText)
    End Function

    Private Sub ApplyPartyAskConfig(cfg As BotConfig)
        cfg.PartyAskText = GetRegularPartyAskText()
        cfg.PartyAskCollectionText = GetPartyAskCollectionText()
        cfg.PartyAskCollectionEnabled = _quizUnlocked
    End Sub

    Private Sub LoadPartyAskTexts(regularText As String, collectionText As String)
        If collectionText Is Nothing Then
            ' Older builds stored the collection in the regular field. Keep it for
            ' hidden mode and use its first plain phrase for the normal form.
            _partyAskCollectionText = NormalizePartyAskCollectionText(regularText)
            _partyAskRegularText = PartyAskMessageCollection.NormalizeRegular(If(regularText, "").Split(","c)(0))
        Else
            _partyAskRegularText = PartyAskMessageCollection.NormalizeRegular(regularText)
            _partyAskCollectionText = NormalizePartyAskCollectionText(collectionText)
        End If
        UpdatePartyAskEditorMode(captureCurrent:=False)
    End Sub

    Private Sub UpdatePartyAskEditorMode(Optional captureCurrent As Boolean = True)
        If captureCurrent Then CapturePartyAskEditorText()
        Dim wasApplying = _applyingSettings
        _applyingSettings = True
        _partyAskUpdatingEditor = True
        Try
            _partyAskShowingCollection = _quizUnlocked
            Dim description = If(_quizUnlocked,
                "Separate phrases with commas. Every phrase automatically starts with /all; an existing prefix is not duplicated. Every distinct phrase is sent once in random order before reshuffling, without consecutive repeats.",
                "Enter one regular party request message. Commas are ordinary punctuation. The message repeats at the chosen interval.")
            If _partyAskTextLabel IsNot Nothing Then
                _partyAskTextLabel.Text = If(_quizUnlocked, "Auto Ask Party Text (comma-separated)", "Auto Ask Party Text")
            End If
            If txtPartyAskText IsNot Nothing Then
                txtPartyAskText.MaxLength = If(_quizUnlocked, 0, 32767)
                txtPartyAskText.AccessibleDescription = description
                txtPartyAskText.Text = If(_quizUnlocked, NormalizePartyAskCollectionText(_partyAskCollectionText), PartyAskMessageCollection.NormalizeRegular(_partyAskRegularText))
                _scanTimerToolTip?.SetToolTip(txtPartyAskText, description)
            End If
        Finally
            _partyAskUpdatingEditor = False
            _applyingSettings = wasApplying
        End Try
        UpdatePartyAskButton()
    End Sub
End Class
