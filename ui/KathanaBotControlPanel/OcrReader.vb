Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Globalization
Imports System.Runtime.InteropServices.WindowsRuntime
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks
Imports Windows.Graphics.Imaging
Imports Windows.Globalization
Imports Windows.Media.Ocr
Imports Windows.Storage.Streams

Public NotInheritable Class OcrReader
    Private Shared ReadOnly _sync As New Object()
    Private Shared _engine As OcrEngine
    Private Shared _initAttempted As Boolean = False
    Private Shared _lastError As String = ""

    Private Sub New()
    End Sub

    Public Shared Function ReadName(source As Bitmap) As String
        If source Is Nothing Then
            Return ""
        End If

        Dim direct As String = ""
        Try
            direct = ReadNameInternal(source)
            If direct <> "" Then
                Return direct
            End If
        Catch
        End Try

        ' Some WinRT OCR calls can fail on MTA threads. Retry on STA.
        Return ReadNameStaFallback(source)
    End Function

    Public Shared Function ReadPercent(source As Bitmap) As Double
        If source Is Nothing Then
            Return -1
        End If

        Dim direct As Double = -1
        Try
            direct = ReadPercentInternal(source)
            If direct >= 0 Then
                Return direct
            End If
        Catch
        End Try

        Return ReadPercentStaFallback(source)
    End Function

    Public Shared Function ReadHpFraction(source As Bitmap) As String
        If source Is Nothing Then
            Return ""
        End If

        Dim direct As String = ""
        Try
            direct = ReadHpFractionInternal(source)
            If Not String.IsNullOrWhiteSpace(direct) Then
                Return direct
            End If
        Catch
        End Try

        Return ReadHpFractionStaFallback(source)
    End Function

    Public Shared Function ReadInteger(source As Bitmap) As Long
        If source Is Nothing Then
            Return -1
        End If

        Dim direct As Long = -1
        Try
            direct = ReadIntegerInternal(source)
            If direct >= 0 Then
                Return direct
            End If
        Catch
        End Try

        Return ReadIntegerStaFallback(source)
    End Function

    Public Shared Function ReadScreenText(source As Bitmap) As String
        If source Is Nothing Then
            Return ""
        End If

        Dim direct As String = ""
        Try
            direct = ReadScreenTextInternal(source)
            If Not String.IsNullOrWhiteSpace(direct) Then
                Return direct
            End If
        Catch
        End Try

        Return ReadScreenTextStaFallback(source)
    End Function

    Public Shared Function ReadScreenTextIsolated(source As Bitmap) As String
        If source Is Nothing Then
            Return ""
        End If

        Dim direct As String = ""
        Try
            direct = ReadScreenTextInternal(source, True)
            If Not String.IsNullOrWhiteSpace(direct) Then
                Return direct
            End If
        Catch
        End Try

        Return ReadScreenTextStaFallback(source, True)
    End Function

    Private Shared Function ReadHpFractionStaFallback(source As Bitmap) As String
        Dim output As String = ""
        Dim done As New ManualResetEventSlim(False)

        Dim worker As New Thread(
            Sub()
                Try
                    output = ReadHpFractionInternal(source)
                Catch ex As Exception
                    SetLastError(ex.Message)
                Finally
                    done.Set()
                End Try
            End Sub)
        worker.IsBackground = True
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start()

        If Not done.Wait(900) Then
            SetLastError("OCR timeout.")
            Return ""
        End If
        Return output
    End Function

    Private Shared Function ReadIntegerStaFallback(source As Bitmap) As Long
        Dim output As Long = -1
        Dim done As New ManualResetEventSlim(False)

        Dim worker As New Thread(
            Sub()
                Try
                    output = ReadIntegerInternal(source)
                Catch ex As Exception
                    SetLastError(ex.Message)
                Finally
                    done.Set()
                End Try
            End Sub)
        worker.IsBackground = True
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start()

        If Not done.Wait(900) Then
            SetLastError("OCR timeout.")
            Return -1
        End If
        Return output
    End Function

    Private Shared Function ReadScreenTextStaFallback(source As Bitmap, Optional isolatedEngine As Boolean = False) As String
        Dim output As String = ""
        Dim done As New ManualResetEventSlim(False)

        Dim worker As New Thread(
            Sub()
                Try
                    output = ReadScreenTextInternal(source, isolatedEngine)
                Catch ex As Exception
                    SetLastError(ex.Message)
                Finally
                    done.Set()
                End Try
            End Sub)
        worker.IsBackground = True
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start()

        If Not done.Wait(1500) Then ' Provide slightly more time for full screen
            SetLastError("OCR timeout.")
            Return ""
        End If
        Return output
    End Function

    Private Shared Function ReadScreenTextInternal(source As Bitmap, Optional isolatedEngine As Boolean = False) As String
        Dim engine As OcrEngine = If(isolatedEngine, CreateEngine(), GetEngine())
        If engine Is Nothing Then
            Return ""
        End If

        ' Intentionally raw and 1:1 scale to prevent massive memory and CPU bloat
        ' when scanning an entire 1080p or 4K game client window.
        Return ReadRawTextAsync(engine, source).GetAwaiter().GetResult()
    End Function

    Public Shared Function LastError() As String
        SyncLock _sync
            Return _lastError
        End SyncLock
    End Function

    Private Shared Function ReadNameStaFallback(source As Bitmap) As String
        Dim output As String = ""
        Dim done As New ManualResetEventSlim(False)

        Dim worker As New Thread(
            Sub()
                Try
                    output = ReadNameInternal(source)
                Catch ex As Exception
                    SetLastError(ex.Message)
                Finally
                    done.Set()
                End Try
            End Sub)
        worker.IsBackground = True
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start()

        If Not done.Wait(700) Then
            SetLastError("OCR timeout.")
            Return ""
        End If
        Return output
    End Function

    Private Shared Function ReadPercentStaFallback(source As Bitmap) As Double
        Dim output As Double = -1
        Dim done As New ManualResetEventSlim(False)

        Dim worker As New Thread(
            Sub()
                Try
                    output = ReadPercentInternal(source)
                Catch ex As Exception
                    SetLastError(ex.Message)
                Finally
                    done.Set()
                End Try
            End Sub)
        worker.IsBackground = True
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start()

        If Not done.Wait(700) Then
            SetLastError("OCR timeout.")
            Return -1
        End If
        Return output
    End Function

    Private Shared Function ReadNameInternal(source As Bitmap) As String
        Dim engine = GetEngine()
        If engine Is Nothing Then
            Return ""
        End If

        Dim candidates As List(Of Bitmap) = BuildCandidates(source)
        Dim bestText As String = ""
        Dim bestScore As Integer = -1

        Try
            For Each candidate In candidates
                Dim text As String = ReadNameAsync(engine, candidate).GetAwaiter().GetResult()
                Dim score As Integer = ScoreText(text)
                If score > bestScore Then
                    bestScore = score
                    bestText = text
                End If
                If score >= 20 Then
                    Exit For
                End If
            Next
        Finally
            For Each candidate In candidates
                candidate.Dispose()
            Next
        End Try

        Return bestText
    End Function

    Private Shared Function ReadPercentInternal(source As Bitmap) As Double
        Dim engine = GetEngine()
        If engine Is Nothing Then
            Return -1
        End If

        Dim candidates As List(Of Bitmap) = BuildCandidates(source)
        Dim bestPercent As Double = -1
        Dim bestScore As Integer = -1

        Try
            For Each candidate In candidates
                Dim text As String = ReadRawTextAsync(engine, candidate).GetAwaiter().GetResult()
                Dim value As Double = ParsePercentFromText(text)
                Dim score As Integer = ScorePercentText(text, value)
                If score > bestScore Then
                    bestScore = score
                    bestPercent = value
                End If
                If value >= 0 AndAlso score >= 40 Then
                    Exit For
                End If
            Next
        Finally
            For Each candidate In candidates
                candidate.Dispose()
            Next
        End Try

        Return bestPercent
    End Function

    Private Shared Function ReadHpFractionInternal(source As Bitmap) As String
        Dim engine = GetEngine()
        If engine Is Nothing Then
            Return ""
        End If

        Dim candidates As List(Of Bitmap) = BuildHpFractionCandidates(source)
        Dim bestText As String = ""
        Dim bestScore As Integer = -1

        Try
            For Each candidate In candidates
                Dim text As String = NormalizeHpFractionText(ReadRawTextAsync(engine, candidate).GetAwaiter().GetResult())
                Dim score As Integer = ScoreHpFractionText(text)
                If score > bestScore Then
                    bestScore = score
                    bestText = text
                End If
                If score >= 60 Then
                    Exit For
                End If
            Next
        Finally
            For Each candidate In candidates
                candidate.Dispose()
            Next
        End Try

        Return bestText
    End Function

    Private Shared Function ReadIntegerInternal(source As Bitmap) As Long
        Dim engine = GetEngine()
        If engine Is Nothing Then
            Return -1
        End If

        Dim candidates As List(Of Bitmap) = BuildDigitCandidates(source)
        Dim bestValue As Long = -1
        Dim bestScore As Integer = -1

        Try
            For Each candidate In candidates
                Dim text As String = NormalizeIntegerText(ReadRawTextAsync(engine, candidate).GetAwaiter().GetResult())
                Dim value As Long = ParseIntegerFromText(text)
                Dim score As Integer = ScoreDigitText(text, value)
                If score > bestScore Then
                    bestScore = score
                    bestValue = value
                End If
                If value >= 0 AndAlso score >= 45 Then
                    Exit For
                End If
            Next
        Finally
            For Each candidate In candidates
                candidate.Dispose()
            Next
        End Try

        Return bestValue
    End Function

    Private Shared Async Function ReadNameAsync(engine As OcrEngine, prepared As Bitmap) As Task(Of String)
        Dim soft As SoftwareBitmap = Await ConvertBitmapAsync(prepared)
        If soft Is Nothing Then
            Return ""
        End If

        Dim result = Await engine.RecognizeAsync(soft)
        If result Is Nothing OrElse String.IsNullOrWhiteSpace(result.Text) Then
            Return ""
        End If

        Dim cleaned As String = Regex.Replace(result.Text, "[^A-Za-z0-9 '\-()]", " ")
        cleaned = Regex.Replace(cleaned, "\s+", " ").Trim()
        If cleaned.Length < 2 Then
            Return ""
        End If
        Return cleaned
    End Function

    Private Shared Async Function ReadRawTextAsync(engine As OcrEngine, prepared As Bitmap) As Task(Of String)
        Dim soft As SoftwareBitmap = Await ConvertBitmapAsync(prepared)
        If soft Is Nothing Then
            Return ""
        End If

        Dim result = Await engine.RecognizeAsync(soft)
        If result Is Nothing OrElse String.IsNullOrWhiteSpace(result.Text) Then
            Return ""
        End If

        Return result.Text.Trim()
    End Function

    Private Shared Function GetEngine() As OcrEngine
        SyncLock _sync
            If _initAttempted Then
                Return _engine
            End If

            _initAttempted = True
            Try
                _engine = CreateEngine()
            Catch
                _engine = Nothing
            End Try

            Return _engine
        End SyncLock
    End Function

    Private Shared Function CreateEngine() As OcrEngine
        Dim engine As OcrEngine = OcrEngine.TryCreateFromUserProfileLanguages()
        If engine Is Nothing Then
            engine = OcrEngine.TryCreateFromLanguage(New Language("en-US"))
        End If
        Return engine
    End Function

    Private Shared Async Function ConvertBitmapAsync(source As Bitmap) As Task(Of SoftwareBitmap)
        Using ms As New MemoryStream()
            source.Save(ms, ImageFormat.Bmp)
            Dim bytes As Byte() = ms.ToArray()
            Using ras As New InMemoryRandomAccessStream()
                Await ras.WriteAsync(bytes.AsBuffer())
                ras.Seek(0)
                Dim decoder As BitmapDecoder = Await BitmapDecoder.CreateAsync(ras)
                Return Await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
            End Using
        End Using
    End Function

    Private Shared Function BuildCandidates(source As Bitmap) As List(Of Bitmap)
        Dim list As New List(Of Bitmap)()
        Dim baseScaled As Bitmap = ScaleBitmap(source, 4)
        list.Add(baseScaled)
        list.Add(ToGrayHighContrast(baseScaled))
        list.Add(ToBinary(baseScaled, 150, False))
        list.Add(ToBinary(baseScaled, 150, True))
        list.Add(IsolateLightText(baseScaled))
        Return list
    End Function

    Private Shared Function BuildHpFractionCandidates(source As Bitmap) As List(Of Bitmap)
        Dim list As New List(Of Bitmap)()
        Dim baseScaled As Bitmap = ScaleBitmap(source, 5)
        Dim whiteDigits As Bitmap = IsolateWhiteDigits(baseScaled)
        list.Add(baseScaled)
        list.Add(ToGrayHighContrast(baseScaled))
        list.Add(whiteDigits)
        list.Add(ToBinary(whiteDigits, 120, False))
        list.Add(ToBinary(baseScaled, 165, False))
        Return list
    End Function

    Private Shared Function BuildDigitCandidates(source As Bitmap) As List(Of Bitmap)
        Dim list As New List(Of Bitmap)()
        Dim baseScaled As Bitmap = ScaleBitmap(source, 5)
        Dim whiteDigits As Bitmap = IsolateWhiteDigits(baseScaled)
        list.Add(baseScaled)
        list.Add(ToGrayHighContrast(baseScaled))
        list.Add(whiteDigits)
        list.Add(ToBinary(whiteDigits, 120, False))
        list.Add(ToBinary(baseScaled, 165, False))
        Return list
    End Function

    Private Shared Function ScaleBitmap(source As Bitmap, scale As Integer) As Bitmap
        Dim w As Integer = Math.Max(1, source.Width * scale)
        Dim h As Integer = Math.Max(1, source.Height * scale)
        Dim enlarged As New Bitmap(w, h, PixelFormat.Format24bppRgb)

        Using g As Graphics = Graphics.FromImage(enlarged)
            g.InterpolationMode = InterpolationMode.HighQualityBicubic
            g.PixelOffsetMode = PixelOffsetMode.HighQuality
            g.DrawImage(source, New Rectangle(0, 0, w, h), New Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel)
        End Using
        Return enlarged
    End Function

    Private Shared Function ToGrayHighContrast(source As Bitmap) As Bitmap
        Dim outBmp As New Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb)
        For y As Integer = 0 To source.Height - 1
            For x As Integer = 0 To source.Width - 1
                Dim c As Color = source.GetPixel(x, y)
                Dim gray As Integer = CInt(Math.Min(255, Math.Max(0, (c.R * 0.299) + (c.G * 0.587) + (c.B * 0.114))))
                gray = CInt(Math.Min(255, Math.Max(0, (gray - 80) * 2.2)))
                outBmp.SetPixel(x, y, Color.FromArgb(gray, gray, gray))
            Next
        Next
        Return outBmp
    End Function

    Private Shared Function ToBinary(source As Bitmap, threshold As Integer, invert As Boolean) As Bitmap
        Dim outBmp As New Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb)
        For y As Integer = 0 To source.Height - 1
            For x As Integer = 0 To source.Width - 1
                Dim c As Color = source.GetPixel(x, y)
                Dim gray As Integer = CInt((c.R * 0.299) + (c.G * 0.587) + (c.B * 0.114))
                Dim isLight As Boolean = gray >= threshold
                If invert Then
                    isLight = Not isLight
                End If
                If isLight Then
                    outBmp.SetPixel(x, y, Color.White)
                Else
                    outBmp.SetPixel(x, y, Color.Black)
                End If
            Next
        Next
        Return outBmp
    End Function

    Private Shared Function IsolateLightText(source As Bitmap) As Bitmap
        Dim outBmp As New Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb)
        For y As Integer = 0 To source.Height - 1
            For x As Integer = 0 To source.Width - 1
                Dim c As Color = source.GetPixel(x, y)
                Dim bright As Integer = CInt(c.R) + CInt(c.G) + CInt(c.B)
                Dim isText As Boolean =
                    bright >= 350 OrElse
                    (c.R >= 150 AndAlso c.G >= 150) OrElse
                    (c.R >= 170 AndAlso c.G >= 130)
                outBmp.SetPixel(x, y, If(isText, Color.White, Color.Black))
            Next
        Next
        Return outBmp
    End Function

    Private Shared Function IsolateWhiteDigits(source As Bitmap) As Bitmap
        Dim outBmp As New Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb)
        For y As Integer = 0 To source.Height - 1
            For x As Integer = 0 To source.Width - 1
                Dim c As Color = source.GetPixel(x, y)
                Dim maxChannel As Integer = Math.Max(c.R, Math.Max(c.G, c.B))
                Dim minChannel As Integer = Math.Min(c.R, Math.Min(c.G, c.B))
                Dim isNearWhite As Boolean = maxChannel >= 165 AndAlso (maxChannel - minChannel) <= 65
                outBmp.SetPixel(x, y, If(isNearWhite, Color.White, Color.Black))
            Next
        Next
        Return outBmp
    End Function

    Private Shared Function ScoreText(text As String) As Integer
        If String.IsNullOrWhiteSpace(text) Then
            Return -1
        End If

        Dim compact As String = text.Trim()
        Dim alphaNum As Integer = Regex.Matches(compact, "[A-Za-z0-9]").Count
        Dim spaces As Integer = Regex.Matches(compact, "\s").Count
        Dim score As Integer = (alphaNum * 3) + compact.Length - spaces
        Return score
    End Function

    Private Shared Function ScorePercentText(text As String, value As Double) As Integer
        If String.IsNullOrWhiteSpace(text) Then
            Return -1
        End If

        Dim score As Integer = 0
        score += Regex.Matches(text, "\d").Count * 2
        If text.Contains("%") Then
            score += 8
        End If
        If text.Contains(".") OrElse text.Contains(",") Then
            score += 6
        End If
        If value >= 0 AndAlso value <= 100 Then
            score += 30
        End If
        Return score
    End Function

    Private Shared Function NormalizeHpFractionText(raw As String) As String
        If String.IsNullOrWhiteSpace(raw) Then
            Return ""
        End If

        Dim normalized As String = raw.ToUpperInvariant()
        normalized = normalized.Replace("O", "0").Replace("I", "1").Replace("L", "1").Replace("|", "1")
        normalized = normalized.Replace(",", "").Replace(".", "")
        normalized = Regex.Replace(normalized, "[^0-9/ ]", " ")
        normalized = Regex.Replace(normalized, "/{2,}", "/")
        normalized = Regex.Replace(normalized, "\s+", " ").Trim()

        Dim fractionMatch As Match = Regex.Match(normalized, "(\d{2,9})\s*/\s*(\d{2,9})")
        If fractionMatch.Success Then
            Return $"{fractionMatch.Groups(1).Value}/{fractionMatch.Groups(2).Value}"
        End If

        Return normalized
    End Function

    Private Shared Function ScoreHpFractionText(text As String) As Integer
        If String.IsNullOrWhiteSpace(text) Then
            Return -1
        End If

        Dim score As Integer = Regex.Matches(text, "\d").Count * 4
        If text.Contains("/"c) Then
            score += 18
        End If
        If Regex.IsMatch(text, "^\d{2,9}/\d{2,9}$") Then
            score += 40
        End If
        Return score
    End Function

    Private Shared Function NormalizeIntegerText(raw As String) As String
        If String.IsNullOrWhiteSpace(raw) Then
            Return ""
        End If

        Dim normalized As String = raw.ToUpperInvariant()
        normalized = normalized.Replace("O", "0").Replace("I", "1").Replace("L", "1").Replace("|", "1")
        normalized = normalized.Replace(",", "").Replace(".", "").Replace(" ", "")
        normalized = Regex.Replace(normalized, "[^0-9]", "")
        Return normalized
    End Function

    Private Shared Function ParseIntegerFromText(raw As String) As Long
        If String.IsNullOrWhiteSpace(raw) Then
            Return -1
        End If

        Dim normalized As String = NormalizeIntegerText(raw)
        If normalized = "" Then
            Return -1
        End If

        Dim value As Long
        If Long.TryParse(normalized, value) AndAlso value >= 0 Then
            Return value
        End If
        Return -1
    End Function

    Private Shared Function ScoreDigitText(text As String, value As Long) As Integer
        If String.IsNullOrWhiteSpace(text) Then
            Return -1
        End If

        Dim score As Integer = Regex.Matches(text, "\d").Count * 4
        If value >= 0 Then
            score += 20
        End If
        If Regex.IsMatch(text, "^\d{2,15}$") Then
            score += 15
        End If
        Return score
    End Function

    Private Shared Function ParsePercentFromText(raw As String) As Double
        If String.IsNullOrWhiteSpace(raw) Then
            Return -1
        End If

        Dim normalized As String = raw.ToLowerInvariant()
        normalized = normalized.Replace("o", "0").Replace("l", "1").Replace("i", "1")
        normalized = normalized.Replace(",", ".")
        normalized = Regex.Replace(normalized, "[^0-9.% ]", " ")
        normalized = Regex.Replace(normalized, "\s+", " ").Trim()
        If normalized = "" Then
            Return -1
        End If

        Dim matches = Regex.Matches(normalized, "\d{1,5}(?:\.\d{1,3})?")
        Dim best As Double = -1
        Dim bestWeight As Integer = -1
        For Each m As Match In matches
            Dim token As String = m.Value
            Dim parsed As Double
            If Not TryParsePercentToken(token, parsed) Then
                Continue For
            End If

            Dim weight As Integer = token.Length
            If token.Contains(".") Then
                weight += 5
            End If
            If parsed >= 0 AndAlso parsed <= 100 Then
                weight += 10
            End If

            If weight > bestWeight Then
                bestWeight = weight
                best = parsed
            End If
        Next

        Return best
    End Function

    Private Shared Function TryParsePercentToken(token As String, ByRef value As Double) As Boolean
        value = -1
        If String.IsNullOrWhiteSpace(token) Then
            Return False
        End If

        Dim direct As Double
        If Double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, direct) Then
            If direct >= 0 AndAlso direct <= 100 Then
                value = direct
                Return True
            End If
        End If

        Dim digitsOnly As String = Regex.Replace(token, "[^0-9]", "")
        If digitsOnly.Length < 3 Then
            Return False
        End If

        Dim candidate As Double = -1
        If digitsOnly.Length = 3 Then
            candidate = Double.Parse($"{digitsOnly.Substring(0, 1)}.{digitsOnly.Substring(1, 2)}", CultureInfo.InvariantCulture)
        ElseIf digitsOnly.Length = 4 Then
            candidate = Double.Parse($"{digitsOnly.Substring(0, 2)}.{digitsOnly.Substring(2, 2)}", CultureInfo.InvariantCulture)
        ElseIf digitsOnly.Length = 5 Then
            candidate = Double.Parse($"{digitsOnly.Substring(0, 3)}.{digitsOnly.Substring(3, 2)}", CultureInfo.InvariantCulture)
        End If

        If candidate >= 0 AndAlso candidate <= 100 Then
            value = candidate
            Return True
        End If

        Return False
    End Function

    Private Shared Sub SetLastError(message As String)
        SyncLock _sync
            _lastError = If(message, "")
        End SyncLock
    End Sub
End Class
