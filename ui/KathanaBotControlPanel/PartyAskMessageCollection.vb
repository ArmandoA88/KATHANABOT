Friend NotInheritable Class PartyAskMessageCollection
    Public Const DefaultMessage As String = "/all add"
    Public Const DefaultRegularMessage As String = "add"
    Private ReadOnly _random As Random
    Private _source As String = Nothing
    Private _phrases As New List(Of String)
    Private ReadOnly _remaining As New List(Of String)
    Private _pending As String = Nothing
    Private _lastSent As String = ""

    Public Sub New(Optional random As Random = Nothing)
        _random = If(random, Random.Shared)
    End Sub

    Public Shared Function Parse(rawText As String) As List(Of String)
        Dim phrases As New List(Of String)
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each entry In If(rawText, "").Split(","c)
            Dim phrase = entry.Replace(vbCr, " ").Replace(vbLf, " ").Trim()
            While phrase.StartsWith("/all", StringComparison.OrdinalIgnoreCase) AndAlso
                  (phrase.Length = 4 OrElse Char.IsWhiteSpace(phrase(4)))
                phrase = phrase.Substring(4).TrimStart()
            End While
            If phrase.Length = 0 Then Continue For
            phrase = "/all " & phrase
            If seen.Add(phrase) Then phrases.Add(phrase)
        Next
        If phrases.Count = 0 Then phrases.Add(DefaultMessage)
        Return phrases
    End Function

    Public Shared Function NormalizeRegular(rawText As String) As String
        Dim message = If(rawText, "").Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        While message.StartsWith("/all", StringComparison.OrdinalIgnoreCase) AndAlso
              (message.Length = 4 OrElse Char.IsWhiteSpace(message(4)))
            message = message.Substring(4).TrimStart()
        End While
        Return If(message.Length = 0, DefaultRegularMessage, message)
    End Function

    ' Keep a pending choice until chat confirms submission. Failed sends must not
    ' consume a phrase or change the message on every retry.
    Public Function Peek(rawText As String) As String
        Dim source = If(rawText, "")
        If _source Is Nothing OrElse Not String.Equals(_source, source, StringComparison.Ordinal) Then
            Dim phrases = Parse(source)
            If Not _phrases.SequenceEqual(phrases, StringComparer.Ordinal) Then
                _phrases = phrases
                _remaining.Clear()
                _pending = Nothing
            End If
            _source = source
        End If

        If _pending IsNot Nothing Then Return _pending
        If _remaining.Count = 0 Then
            _remaining.AddRange(_phrases)
            For index = _remaining.Count - 1 To 1 Step -1
                Dim swapIndex = _random.Next(index + 1)
                Dim temporary = _remaining(index)
                _remaining(index) = _remaining(swapIndex)
                _remaining(swapIndex) = temporary
            Next
            If _remaining.Count > 1 AndAlso String.Equals(_remaining(0), _lastSent, StringComparison.OrdinalIgnoreCase) Then
                Dim swapIndex = _random.Next(1, _remaining.Count)
                Dim temporary = _remaining(0)
                _remaining(0) = _remaining(swapIndex)
                _remaining(swapIndex) = temporary
            End If
        End If
        _pending = _remaining(0)
        Return _pending
    End Function

    Public Sub MarkSent()
        If _pending Is Nothing Then Return
        _lastSent = _pending
        _remaining.RemoveAt(0)
        _pending = Nothing
    End Sub

    Public Sub Reset()
        _source = Nothing
        _phrases.Clear()
        _remaining.Clear()
        _pending = Nothing
        _lastSent = ""
    End Sub
End Class
