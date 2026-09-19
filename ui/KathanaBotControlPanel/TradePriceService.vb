Imports System.Globalization
Imports System.Text.RegularExpressions

Public Class TradePriceOffer
    Public Property CharacterName As String = ""
    Public Property Item As String = ""
    Public Property Price As Decimal
    Public Property Evidence As String = ""
    Public Property Seen As DateTime
End Class

Public NotInheritable Class TradePriceService
    Private Shared ReadOnly PricePattern As New Regex("(?<![\w.])(?<value>\d+(?:[.,]\d{3})*(?:[.,]\d+)?)(?:\s*(?<unit>kk|k|m|b|million|rupiahs?|rp))?(?![\w.])", RegexOptions.IgnoreCase)

    Public Shared Function Parse(text As String, recipients As IEnumerable(Of TradeRecipient)) As List(Of TradePriceOffer)
        Dim result As New List(Of TradePriceOffer)
        For Each line In text.Replace(vbCr, "").Split(vbLf)
            Dim header = Regex.Match(line.Trim(), "^(?:\[[^\]]+\]\s*)?(?<name>[\p{L}\p{N}_-]{1,32})\s*[:>]\s*(?<body>.+)$")
            If Not header.Success Then Continue For
            Dim name = header.Groups("name").Value
            Dim body = header.Groups("body").Value
            ' A currency quote in real money is not comparable with in-game rupiahs.
            If Regex.IsMatch(body, "[$€£]|\b(?:usd|eur|php|dollars?|pesos?)\b", RegexOptions.IgnoreCase) Then Continue For
            Dim known = recipients.Where(Function(r) String.Equals(r.CharacterName, name, StringComparison.Ordinal)).
                SelectMany(Function(r) r.Items.Split(","c)).Select(Function(s) s.Trim()).Where(Function(s) s <> "").Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            For Each segment In Regex.Split(body, "\s*(?:;|\||\s--\s)\s*")
                Dim prices = PricePattern.Matches(segment).Cast(Of Match)().Where(Function(m) m.Groups("unit").Success OrElse Regex.IsMatch(segment, "\b(?:price|offer|for|sell|selling)\b", RegexOptions.IgnoreCase) OrElse segment.Trim() = m.Value).ToList()
                ' Multiple numbers in one clause can be quantities, levels, or alternative prices.
                If prices.Count <> 1 Then Continue For
                Dim quote = prices(0)
                Dim amount = quote.Groups("value").Value
                If Regex.IsMatch(amount, "^\d{1,3}(?:,\d{3})+$") Then
                    amount = amount.Replace(",", "")
                Else
                    amount = amount.Replace(",", ".")
                End If
                Dim value As Decimal
                If Not Decimal.TryParse(amount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, value) OrElse value <= 0 Then Continue For
                Select Case quote.Groups("unit").Value.ToLowerInvariant()
                    Case "k" : value *= 1000D
                    Case "kk", "m", "million" : value *= 1000000D
                    Case "b" : value *= 1000000000D
                End Select
                Dim matches = known.Where(Function(item) segment.Contains(item, StringComparison.OrdinalIgnoreCase)).ToList()
                Dim remaining = segment.Remove(quote.Index, quote.Length).Trim(" "c, "."c, "!"c, "?"c)
                Dim priceOnlyReply = Regex.IsMatch(remaining, "^(?:(?:price|is|its|it's|for|only|selling|sell|offer|i|can|do|best|at|my|each)\s*)*$", RegexOptions.IgnoreCase)
                Dim itemName = If(matches.Count = 1, matches(0), If(known.Count = 1 AndAlso priceOnlyReply, known(0), "Unknown - review chat"))
                result.Add(New TradePriceOffer With {.CharacterName = name, .Item = itemName, .Price = value, .Evidence = line.Trim(), .Seen = DateTime.Now})
            Next
        Next
        Return result
    End Function

    Public Shared Sub Merge(offers As List(Of TradePriceOffer), incoming As IEnumerable(Of TradePriceOffer))
        For Each offer In incoming
            Dim existing = offers.FindIndex(Function(old) old.CharacterName = offer.CharacterName AndAlso String.Equals(old.Item, offer.Item, StringComparison.OrdinalIgnoreCase))
            If existing < 0 Then
                offers.Add(offer)
            ElseIf offers(existing).Evidence <> offer.Evidence Then
                offers(existing) = offer
            End If
        Next
        offers.Sort(Function(a, b) a.Price.CompareTo(b.Price))
    End Sub
End Class
