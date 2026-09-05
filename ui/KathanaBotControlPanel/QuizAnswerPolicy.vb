Friend NotInheritable Class QuizAnswerPolicy
    Private Sub New()
    End Sub

    Public Shared Function HasWebEvidence(answer As QuizSolveResult) As Boolean
        Return answer IsNot Nothing AndAlso answer.SearchPerformed AndAlso answer.SourceVerified AndAlso
            answer.AnswerBasis = "web" AndAlso Not String.IsNullOrWhiteSpace(answer.Evidence)
    End Function

    Public Shared Function CanClick(answer As QuizSolveResult, buttonCount As Integer, ByRef reason As String) As Boolean
        reason = ""
        If answer Is Nothing OrElse Not answer.CanAnswer OrElse String.IsNullOrWhiteSpace(answer.QuestionText) OrElse String.IsNullOrWhiteSpace(answer.AnswerText) Then
            reason = "No supported answer found."
            Return False
        End If
        If answer.ButtonNumber < 1 OrElse answer.ButtonNumber > buttonCount Then
            reason = "The answer does not map to a detected button; no random click was sent."
            Return False
        End If
        If Not Double.IsFinite(answer.Confidence) OrElse answer.Confidence < 0 OrElse answer.Confidence > 1 Then
            reason = "Invalid answer confidence."
            Return False
        End If
        Select Case answer.Category
            Case "gm_personal"
                If Not HasWebEvidence(answer) Then
                    answer.IsGuess = True
                    answer.AnswerBasis = "guess"
                End If
                Return True
            Case "game"
                If answer.IsGuess OrElse Not HasWebEvidence(answer) OrElse answer.Confidence < 0.75 Then
                    reason = "Kathana/Tantra answer could not be supported by a web source."
                    Return False
                End If
            Case "general"
                Dim supported = (HasWebEvidence(answer) AndAlso answer.Confidence >= 0.75) OrElse
                    (answer.AnswerBasis = "knowledge" AndAlso answer.Confidence >= 0.85)
                If answer.IsGuess OrElse Not supported Then
                    reason = "General-knowledge answer is uncertain."
                    Return False
                End If
            Case Else
                reason = "Unknown question category."
                Return False
        End Select
        Return True
    End Function

    Public Shared Function MethodLabel(answer As QuizSolveResult) As String
        If answer.IsGuess Then Return "GM guess"
        If HasWebEvidence(answer) Then Return "Web-sourced"
        Return If(answer.Category = "general" AndAlso answer.AnswerBasis = "knowledge", "General knowledge", "Unverified")
    End Function
End Class
