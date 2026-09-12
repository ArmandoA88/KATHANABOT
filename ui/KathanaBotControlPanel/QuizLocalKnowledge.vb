Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Reflection
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Diagnostics
Imports System.Threading.Tasks
Imports System.Threading

Friend NotInheritable Class QuizLocalKnowledge
    Private Const ResourceName As String = "KathanaBotControlPanel.QuizKnowledge.dat"
    Private Shared ReadOnly StopWords As New HashSet(Of String)(StringComparer.Ordinal) From {
        "about", "after", "answer", "are", "can", "does", "following", "from", "have", "into", "kathana",
        "level", "many", "monster", "name", "online", "quest", "skill", "tantra", "that", "the", "their",
        "there", "these", "this", "what", "when", "where", "which", "who", "with"
    }
    Private Shared ReadOnly Index As New Lazy(Of KnowledgeIndex)(AddressOf LoadIndex, True)
    Private Shared ReadOnly AliasTokens As New Dictionary(Of String, String)(StringComparer.Ordinal) From {
        {"located", "location"}, {"coordinates", "location"}, {"coordinate", "location"}, {"found", "location"},
        {"requires", "requirement"}, {"required", "requirement"}, {"requirements", "requirement"},
        {"drop", "drops"}, {"dropped", "drops"}, {"dropping", "drops"},
        {"jobs", "job"}, {"classes", "class"}, {"refines", "refining"}, {"refine", "refining"}
    }

    Private NotInheritable Class KnowledgeRecord
        Public Property Text As String = ""
        Public Property Normalized As String = ""
        Public Property Source As String = "https://kathana.gitbook.io/wiki"
        Public Property Terms As HashSet(Of String)
    End Class

    Private NotInheritable Class KnowledgeIndex
        Public Property Records As List(Of KnowledgeRecord)
        Public Property ByToken As Dictionary(Of String, List(Of Integer))
        Public Property Vocabulary As String()
    End Class

    Friend NotInheritable Class SolveTiming
        Public Property OcrMs As Long
        Public Property SearchMs As Long
    End Class

    Friend NotInheritable Class SolveAttempt
        Public Property Answer As QuizSolveResult
        Public Property Timing As New SolveTiming()
    End Class

    Private Sub New()
    End Sub

    Public Shared Function WarmUpAsync() As Task
        OcrReader.PrewarmAsync()
        Return Task.Run(Sub() WarmUpIndex())
    End Function

    Public Shared Function SolveAsync(quizImage As Bitmap,
                                      answerAreaWithinQuiz As Rectangle,
                                      buttons As IReadOnlyList(Of Rectangle),
                                      cancellationToken As CancellationToken) As Task(Of SolveAttempt)
        If quizImage Is Nothing Then Return Task.FromResult(New SolveAttempt())
        Dim ownedImage = DirectCast(quizImage.Clone(), Bitmap)
        Dim ownedButtons = buttons.ToArray()
        Return Task.Run(
            Function()
                Using ownedImage
                    cancellationToken.ThrowIfCancellationRequested()
                    Dim attempt As New SolveAttempt()
                    Dim answer As QuizSolveResult = Nothing
                    If TrySolve(ownedImage, answerAreaWithinQuiz, ownedButtons, answer, attempt.Timing) Then attempt.Answer = answer
                    cancellationToken.ThrowIfCancellationRequested()
                    Return attempt
                End Using
            End Function,
            cancellationToken)
    End Function

    Private Shared Sub WarmUpIndex()
        Dim ignored = Index.Value.Records.Count
    End Sub

    Public Shared Function TrySolve(quizImage As Bitmap,
                                    answerAreaWithinQuiz As Rectangle,
                                    buttons As IReadOnlyList(Of Rectangle),
                                    ByRef result As QuizSolveResult,
                                    Optional timing As SolveTiming = Nothing) As Boolean
        result = Nothing
        If quizImage Is Nothing OrElse buttons Is Nothing OrElse buttons.Count < 2 Then Return False

        Dim watch = Stopwatch.StartNew()
        Dim questionHeight = Math.Max(1, Math.Min(quizImage.Height, answerAreaWithinQuiz.Top))
        Dim regions = OcrReader.ReadScreenTextRegions(quizImage)
        Dim questionText = JoinRegions(regions, New Rectangle(0, 0, quizImage.Width, questionHeight))
        If questionText.Length < 4 Then
            Using questionImage = QuizImageTools.Crop(quizImage, New Rectangle(0, 0, quizImage.Width, questionHeight))
                questionText = OcrReader.ReadScreenText(questionImage).Trim()
            End Using
        End If
        If questionText.Length < 4 Then Return False

        Dim choices As New List(Of String)()
        For Each button In buttons
            Dim area = New Rectangle(answerAreaWithinQuiz.X + button.X, answerAreaWithinQuiz.Y + button.Y, button.Width, button.Height)
            area.Intersect(New Rectangle(0, 0, quizImage.Width, quizImage.Height))
            If area.Width <= 0 OrElse area.Height <= 0 Then Return False
            Dim choiceText = JoinRegions(regions, area)
            If choiceText.Length < 1 Then
                Using choiceImage = QuizImageTools.Crop(quizImage, area)
                    choiceText = OcrReader.ReadScreenText(choiceImage).Trim()
                End Using
            End If
            choices.Add(choiceText)
        Next
        If timing IsNot Nothing Then timing.OcrMs = watch.ElapsedMilliseconds

        Dim chosen As Integer = -1
        Dim evidence As String = ""
        Dim source As String = ""
        Dim confidence As Double = 0
        watch.Restart()
        If Not TryMatch(questionText, choices, chosen, evidence, source, confidence) Then
            If timing IsNot Nothing Then timing.SearchMs = watch.ElapsedMilliseconds
            Return False
        End If
        If timing IsNot Nothing Then timing.SearchMs = watch.ElapsedMilliseconds
        result = New QuizSolveResult With {
            .QuestionText = questionText,
            .AnswerText = choices(chosen),
            .ButtonNumber = chosen + 1,
            .GridColumn = 0,
            .GridRow = 0,
            .Confidence = confidence,
            .IsGuess = False,
            .Category = "game",
            .AnswerBasis = "local",
            .CanAnswer = True,
            .Evidence = evidence,
            .SourceUrl = source,
            .SearchPerformed = False,
            .SourceVerified = True
        }
        Return True
    End Function

    Private Shared Function JoinRegions(regions As IEnumerable(Of OcrReader.OcrTextRegion), area As Rectangle) As String
        Return String.Join(" ", regions.Where(Function(region) area.Contains(region.Bounds.Left + region.Bounds.Width \ 2, region.Bounds.Top + region.Bounds.Height \ 2)).
                                      OrderBy(Function(region) region.Bounds.Top).
                                      ThenBy(Function(region) region.Bounds.Left).
                                      Select(Function(region) region.Text)).Trim()
    End Function

    Friend Shared Function TryMatch(question As String,
                                    choices As IReadOnlyList(Of String),
                                    ByRef chosenIndex As Integer,
                                    ByRef evidence As String,
                                    ByRef source As String,
                                    ByRef confidence As Double) As Boolean
        chosenIndex = -1 : evidence = "" : source = "" : confidence = 0
        If String.IsNullOrWhiteSpace(question) OrElse choices Is Nothing OrElse choices.Count < 2 Then Return False
        Dim indexValue = Index.Value
        Dim questionTokens = CorrectTokens(Tokens(question).Where(Function(token) Not StopWords.Contains(token)), indexValue).Distinct().ToArray()
        If questionTokens.Length = 0 Then Return False

        Dim candidateIds As New HashSet(Of Integer)()
        For Each token In questionTokens
            Dim ids As List(Of Integer) = Nothing
            If indexValue.ByToken.TryGetValue(token, ids) Then candidateIds.UnionWith(ids)
        Next
        If candidateIds.Count = 0 Then Return False

        Dim scores(choices.Count - 1) As Double
        Dim bestRecords(choices.Count - 1) As KnowledgeRecord
        For choiceIndex = 0 To choices.Count - 1
            Dim choiceTokens = CorrectTokens(Tokens(choices(choiceIndex)).Where(Function(token) token.Length > 1), indexValue).Distinct().ToArray()
            If choiceTokens.Length = 0 Then Continue For
            For Each recordId In candidateIds
                Dim record = indexValue.Records(recordId)
                Dim choiceHits = choiceTokens.Count(Function(token) record.Terms.Contains(token))
                Dim choiceCoverage = choiceHits / CDbl(choiceTokens.Length)
                If choiceCoverage < 0.66R Then Continue For
                Dim questionHits = questionTokens.Count(Function(token) record.Terms.Contains(token))
                If questionHits = 0 Then Continue For
                Dim numericHits = questionTokens.Count(Function(token) token.All(AddressOf Char.IsDigit) AndAlso record.Terms.Contains(token))
                Dim score = choiceCoverage * 12.0R + questionHits * 3.0R + numericHits * 4.0R
                If score > scores(choiceIndex) Then scores(choiceIndex) = score : bestRecords(choiceIndex) = record
            Next
        Next

        Dim ranked = scores.Select(Function(value, index) New With {.Index = index, .Score = value}).OrderByDescending(Function(item) item.Score).ToArray()
        If ranked(0).Score < 15.0R OrElse ranked(0).Score - ranked(1).Score < 3.0R OrElse bestRecords(ranked(0).Index) Is Nothing Then Return False
        chosenIndex = ranked(0).Index
        Dim winner = bestRecords(chosenIndex)
        source = winner.Source
        confidence = Math.Min(0.97R, 0.82R + Math.Min(0.15R, (ranked(0).Score - ranked(1).Score) / 40.0R))
        Dim excerpt = Regex.Replace(winner.Text, "\s+", " ").Trim()
        If excerpt.Length > 180 Then excerpt = excerpt.Substring(0, 177) & "..."
        evidence = "Bundled Kathana index: " & excerpt
        Return True
    End Function

    Private Shared Function LoadIndex() As KnowledgeIndex
        Dim records As New List(Of KnowledgeRecord)()
        Using stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            If stream Is Nothing Then Return New KnowledgeIndex With {.Records = records, .ByToken = New Dictionary(Of String, List(Of Integer))(StringComparer.Ordinal), .Vocabulary = Array.Empty(Of String)()}
            Dim encrypted As Byte()
            Using buffer As New MemoryStream()
                stream.CopyTo(buffer)
                encrypted = buffer.ToArray()
            End Using
            If encrypted.Length <= 32 Then Return New KnowledgeIndex With {.Records = records, .ByToken = New Dictionary(Of String, List(Of Integer))(StringComparer.Ordinal), .Vocabulary = Array.Empty(Of String)()}
            Dim iv(15) As Byte
            Array.Copy(encrypted, 0, iv, 0, iv.Length)
            Dim key = SHA256.HashData(Encoding.UTF8.GetBytes("KathanaQuizIndex-v1-embedded-2026"))
            Using algorithm As Aes = Aes.Create()
                algorithm.Key = key
                algorithm.IV = iv
                algorithm.Mode = CipherMode.CBC
                algorithm.Padding = PaddingMode.PKCS7
                Using encryptedContent As New MemoryStream(encrypted, iv.Length, encrypted.Length - iv.Length, False),
                      crypto As New CryptoStream(encryptedContent, algorithm.CreateDecryptor(), CryptoStreamMode.Read),
                      gzip As New GZipStream(crypto, CompressionMode.Decompress),
                      reader As New StreamReader(gzip, Encoding.UTF8, True)
                Dim currentSource = "https://kathana.gitbook.io/wiki"
                While Not reader.EndOfStream
                    Dim line = reader.ReadLine().Trim()
                    If line.StartsWith("SOURCE ", StringComparison.OrdinalIgnoreCase) Then
                        currentSource = line.Substring(7).Trim()
                    ElseIf line.Contains(" SOURCE ", StringComparison.OrdinalIgnoreCase) Then
                        Dim marker = line.LastIndexOf(" SOURCE ", StringComparison.OrdinalIgnoreCase)
                        currentSource = line.Substring(marker + 8).Trim()
                    ElseIf line.Length >= 4 Then
                        Dim normalized = Normalize(line)
                        records.Add(New KnowledgeRecord With {.Text = line, .Normalized = " " & normalized & " ", .Source = currentSource, .Terms = New HashSet(Of String)(Tokens(normalized), StringComparer.Ordinal)})
                    End If
                End While
                End Using
            End Using
        End Using
        Dim byToken As New Dictionary(Of String, List(Of Integer))(StringComparer.Ordinal)
        For recordId = 0 To records.Count - 1
            For Each token In records(recordId).Terms
                Dim ids As List(Of Integer) = Nothing
                If Not byToken.TryGetValue(token, ids) Then ids = New List(Of Integer)() : byToken(token) = ids
                ids.Add(recordId)
            Next
        Next
        Return New KnowledgeIndex With {.Records = records, .ByToken = byToken, .Vocabulary = byToken.Keys.Where(Function(token) token.Length >= 3).ToArray()}
    End Function

    Private Shared Function Tokens(value As String) As IEnumerable(Of String)
        Return Normalize(value).Split(" "c, StringSplitOptions.RemoveEmptyEntries).Select(AddressOf CanonicalToken)
    End Function

    Private Shared Function CanonicalToken(token As String) As String
        Dim replacement As String = Nothing
        Return If(AliasTokens.TryGetValue(token, replacement), replacement, token)
    End Function

    Private Shared Function CorrectTokens(tokens As IEnumerable(Of String), indexValue As KnowledgeIndex) As IEnumerable(Of String)
        Dim output As New List(Of String)()
        For Each token In tokens
            If indexValue.ByToken.ContainsKey(token) OrElse token.Length < 4 Then
                output.Add(token)
                Continue For
            End If
            Dim matches = indexValue.Vocabulary.Where(Function(candidate) Math.Abs(candidate.Length - token.Length) <= 1 AndAlso EditDistanceAtMostOne(token, candidate)).Take(2).ToArray()
            output.Add(If(matches.Length = 1, matches(0), token))
        Next
        Return output
    End Function

    Private Shared Function EditDistanceAtMostOne(a As String, b As String) As Boolean
        If a = b Then Return True
        If Math.Abs(a.Length - b.Length) > 1 Then Return False
        If a.Length > b.Length Then Dim swap = a : a = b : b = swap
        Dim i = 0, j = 0, edits = 0
        While i < a.Length AndAlso j < b.Length
            If a(i) = b(j) Then i += 1 : j += 1 : Continue While
            edits += 1
            If edits > 1 Then Return False
            If a.Length = b.Length Then i += 1
            j += 1
        End While
        Return True
    End Function

    Private Shared Function Normalize(value As String) As String
        Dim decomposed = If(value, "").Normalize(NormalizationForm.FormD)
        Dim builder As New StringBuilder(decomposed.Length)
        For Each character In decomposed
            If CharUnicodeInfo.GetUnicodeCategory(character) = UnicodeCategory.NonSpacingMark Then Continue For
            builder.Append(If(Char.IsLetterOrDigit(character), Char.ToLowerInvariant(character), " "c))
        Next
        Return Regex.Replace(builder.ToString(), "\s+", " ").Trim()
    End Function
End Class
