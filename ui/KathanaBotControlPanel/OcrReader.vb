Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
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

    Private Shared Function GetEngine() As OcrEngine
        SyncLock _sync
            If _initAttempted Then
                Return _engine
            End If

            _initAttempted = True
            Try
                _engine = OcrEngine.TryCreateFromUserProfileLanguages()
                If _engine Is Nothing Then
                    _engine = OcrEngine.TryCreateFromLanguage(New Language("en-US"))
                End If
            Catch
                _engine = Nothing
            End Try

            Return _engine
        End SyncLock
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

    Private Shared Sub SetLastError(message As String)
        SyncLock _sync
            _lastError = If(message, "")
        End SyncLock
    End Sub
End Class
