Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Runtime.InteropServices.WindowsRuntime
Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks
Imports Windows.Graphics.Imaging
Imports Windows.Globalization
Imports Windows.Media.Ocr
Imports Windows.Storage.Streams

Public NotInheritable Class OcrReader
    Public NotInheritable Class OcrTextRegion
        Public Property Text As String = ""
        Public Property Bounds As Rectangle = Rectangle.Empty
    End Class

    Private Shared ReadOnly _sync As New Object()
    Private Shared _engine As OcrEngine
    Private Shared _initAttempted As Boolean = False
    Private Shared _lastError As String = ""
    Private Shared _prewarmStarted As Integer = 0
    Private Shared ReadOnly _staQueue As New BlockingCollection(Of OcrStaWorkItem)()
    Private Shared _staWorkerStarted As Integer = 0
    Private Shared _staWorkerThreadId As Integer = -1

    Private NotInheritable Class OcrStaWorkItem
        Public Property Work As Func(Of Object)
        Public Property Completion As TaskCompletionSource(Of Object)
    End Class

    Private Sub New()
    End Sub

    Public Shared Sub PrewarmAsync()
        If Interlocked.Exchange(_prewarmStarted, 1) = 1 Then
            Return
        End If

        Task.Run(
            Sub()
                Try
                    RunOnStaWorker(
                        Function()
                            Dim engine As OcrEngine = GetEngine()
                            If engine Is Nothing Then
                                Return False
                            End If

                            Using bmp As New Bitmap(8, 8, PixelFormat.Format24bppRgb)
                                Using g As Graphics = Graphics.FromImage(bmp)
                                    g.Clear(Color.White)
                                End Using
                                ReadRawTextAsync(engine, bmp).GetAwaiter().GetResult()
                            End Using
                            Return True
                        End Function,
                        3000,
                        False)
                Catch ex As Exception
                    SetLastError(ex.Message)
                End Try
            End Sub)
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

    Public Shared Function ReadScreenTextRegionsIsolated(source As Bitmap) As List(Of OcrTextRegion)
        If source Is Nothing Then
            Return New List(Of OcrTextRegion)()
        End If

        Try
            Dim direct As List(Of OcrTextRegion) = ReadScreenTextRegionsInternal(source, True)
            If direct IsNot Nothing AndAlso direct.Count > 0 Then
                Return direct
            End If
        Catch
        End Try

        Return ReadScreenTextRegionsStaFallback(source, True)
    End Function

    Private Shared Function ReadHpFractionStaFallback(source As Bitmap) As String
        Return RunOnStaWorker(Function() ReadHpFractionInternal(source), 900, "")
    End Function

    Private Shared Function ReadIntegerStaFallback(source As Bitmap) As Long
        Return RunOnStaWorker(Function() ReadIntegerInternal(source), 900, CLng(-1))
    End Function

    Private Shared Function ReadScreenTextStaFallback(source As Bitmap, Optional isolatedEngine As Boolean = False) As String
        Return RunOnStaWorker(Function() ReadScreenTextInternal(source, isolatedEngine), 1500, "")
    End Function

    Private Shared Function ReadScreenTextRegionsStaFallback(source As Bitmap, Optional isolatedEngine As Boolean = False) As List(Of OcrTextRegion)
        Dim output As List(Of OcrTextRegion) = RunOnStaWorker(Function() ReadScreenTextRegionsInternal(source, isolatedEngine), 1500, New List(Of OcrTextRegion)())
        Return If(output, New List(Of OcrTextRegion)())
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

    Private Shared Function ReadScreenTextRegionsInternal(source As Bitmap, Optional isolatedEngine As Boolean = False) As List(Of OcrTextRegion)
        Dim engine As OcrEngine = If(isolatedEngine, CreateEngine(), GetEngine())
        If engine Is Nothing Then
            Return New List(Of OcrTextRegion)()
        End If

        Return ReadRawRegionsAsync(engine, source).GetAwaiter().GetResult()
    End Function

    Public Shared Function LastError() As String
        SyncLock _sync
            Return _lastError
        End SyncLock
    End Function

    Private Shared Function ReadNameStaFallback(source As Bitmap) As String
        Return RunOnStaWorker(Function() ReadNameInternal(source), 700, "")
    End Function

    Private Shared Function ReadPercentStaFallback(source As Bitmap) As Double
        Return RunOnStaWorker(Function() ReadPercentInternal(source), 700, -1.0R)
    End Function

    Private Shared Function RunOnStaWorker(Of T)(work As Func(Of T), timeoutMs As Integer, fallbackValue As T) As T
        If work Is Nothing Then
            Return fallbackValue
        End If

        If Thread.CurrentThread.ManagedThreadId = _staWorkerThreadId Then
            Try
                Return work()
            Catch ex As Exception
                SetLastError(ex.Message)
                Return fallbackValue
            End Try
        End If

        EnsureStaWorkerStarted()

        Dim completion As New TaskCompletionSource(Of Object)(TaskCreationOptions.RunContinuationsAsynchronously)
        Dim item As New OcrStaWorkItem With {
            .Work = Function() DirectCast(work(), Object),
            .Completion = completion
        }

        Try
            _staQueue.Add(item)
        Catch ex As Exception
            SetLastError(ex.Message)
            Return fallbackValue
        End Try

        If Not completion.Task.Wait(Math.Max(1, timeoutMs)) Then
            SetLastError("OCR timeout.")
            Return fallbackValue
        End If

        Try
            Return DirectCast(completion.Task.Result, T)
        Catch ex As Exception
            Dim message As String = ex.Message
            If TypeOf ex Is AggregateException AndAlso DirectCast(ex, AggregateException).InnerException IsNot Nothing Then
                message = DirectCast(ex, AggregateException).InnerException.Message
            End If
            SetLastError(message)
            Return fallbackValue
        End Try
    End Function

    Private Shared Sub EnsureStaWorkerStarted()
        If Interlocked.Exchange(_staWorkerStarted, 1) = 1 Then
            Return
        End If

        Dim worker As New Thread(AddressOf StaWorkerLoop)
        worker.IsBackground = True
        worker.Name = "KathanaBot OCR STA Worker"
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start()
    End Sub

    Private Shared Sub StaWorkerLoop()
        _staWorkerThreadId = Thread.CurrentThread.ManagedThreadId
        For Each item As OcrStaWorkItem In _staQueue.GetConsumingEnumerable()
            Try
                Dim result As Object = If(item.Work Is Nothing, Nothing, item.Work())
                item.Completion.TrySetResult(result)
            Catch ex As Exception
                SetLastError(ex.Message)
                item.Completion.TrySetException(ex)
            End Try
        Next
    End Sub

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

    Private Shared Async Function ReadRawRegionsAsync(engine As OcrEngine, prepared As Bitmap) As Task(Of List(Of OcrTextRegion))
        Dim items As New List(Of OcrTextRegion)()
        Dim soft As SoftwareBitmap = Await ConvertBitmapAsync(prepared)
        If soft Is Nothing Then
            Return items
        End If

        Dim result = Await engine.RecognizeAsync(soft)
        If result Is Nothing OrElse result.Lines Is Nothing OrElse result.Lines.Count = 0 Then
            Return items
        End If

        For Each line In result.Lines
            If line Is Nothing Then
                Continue For
            End If

            Dim text As String = If(line.Text, "").Trim()
            If text = "" Then
                Continue For
            End If

            Dim bounds As Rectangle = Rectangle.Empty
            If line.Words IsNot Nothing AndAlso line.Words.Count > 0 Then
                Dim minX As Integer = Integer.MaxValue
                Dim minY As Integer = Integer.MaxValue
                Dim maxRight As Integer = Integer.MinValue
                Dim maxBottom As Integer = Integer.MinValue

                For Each word In line.Words
                    Dim rect = word.BoundingRect
                    Dim x As Integer = CInt(Math.Floor(rect.X))
                    Dim y As Integer = CInt(Math.Floor(rect.Y))
                    Dim right As Integer = CInt(Math.Ceiling(rect.X + rect.Width))
                    Dim bottom As Integer = CInt(Math.Ceiling(rect.Y + rect.Height))
                    minX = Math.Min(minX, x)
                    minY = Math.Min(minY, y)
                    maxRight = Math.Max(maxRight, right)
                    maxBottom = Math.Max(maxBottom, bottom)
                Next

                If minX <= maxRight AndAlso minY <= maxBottom Then
                    bounds = Rectangle.FromLTRB(minX, minY, maxRight, maxBottom)
                End If
            End If

            If bounds = Rectangle.Empty Then
                Continue For
            End If

            items.Add(New OcrTextRegion With {
                .Text = text,
                .Bounds = bounds
            })
        Next

        Return items
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
        Dim rect As New Rectangle(0, 0, source.Width, source.Height)
        Dim srcData As BitmapData = Nothing
        Dim dstData As BitmapData = Nothing
        Try
            srcData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb)
            dstData = outBmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb)
            Dim srcBytes(Math.Abs(srcData.Stride) * source.Height - 1) As Byte
            Dim dstBytes(Math.Abs(dstData.Stride) * outBmp.Height - 1) As Byte
            Marshal.Copy(srcData.Scan0, srcBytes, 0, srcBytes.Length)

            For y As Integer = 0 To source.Height - 1
                Dim srcRow As Integer = y * srcData.Stride
                Dim dstRow As Integer = y * dstData.Stride
                For x As Integer = 0 To source.Width - 1
                    Dim si As Integer = srcRow + (x * 3)
                    Dim b As Integer = srcBytes(si)
                    Dim g As Integer = srcBytes(si + 1)
                    Dim r As Integer = srcBytes(si + 2)
                    Dim gray As Integer = CInt(Math.Min(255, Math.Max(0, (r * 0.299R) + (g * 0.587R) + (b * 0.114R))))
                    gray = CInt(Math.Min(255, Math.Max(0, (gray - 80) * 2.2R)))
                    Dim di As Integer = dstRow + (x * 3)
                    Dim value As Byte = CByte(gray)
                    dstBytes(di) = value
                    dstBytes(di + 1) = value
                    dstBytes(di + 2) = value
                Next
            Next

            Marshal.Copy(dstBytes, 0, dstData.Scan0, dstBytes.Length)
        Finally
            If srcData IsNot Nothing Then
                source.UnlockBits(srcData)
            End If
            If dstData IsNot Nothing Then
                outBmp.UnlockBits(dstData)
            End If
        End Try
        Return outBmp
    End Function

    Private Shared Function ToBinary(source As Bitmap, threshold As Integer, invert As Boolean) As Bitmap
        Dim outBmp As New Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb)
        Dim rect As New Rectangle(0, 0, source.Width, source.Height)
        Dim srcData As BitmapData = Nothing
        Dim dstData As BitmapData = Nothing
        Try
            srcData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb)
            dstData = outBmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb)
            Dim srcBytes(Math.Abs(srcData.Stride) * source.Height - 1) As Byte
            Dim dstBytes(Math.Abs(dstData.Stride) * outBmp.Height - 1) As Byte
            Marshal.Copy(srcData.Scan0, srcBytes, 0, srcBytes.Length)

            For y As Integer = 0 To source.Height - 1
                Dim srcRow As Integer = y * srcData.Stride
                Dim dstRow As Integer = y * dstData.Stride
                For x As Integer = 0 To source.Width - 1
                    Dim si As Integer = srcRow + (x * 3)
                    Dim b As Integer = srcBytes(si)
                    Dim g As Integer = srcBytes(si + 1)
                    Dim r As Integer = srcBytes(si + 2)
                    Dim gray As Integer = CInt((r * 0.299R) + (g * 0.587R) + (b * 0.114R))
                    Dim isLight As Boolean = gray >= threshold
                    If invert Then
                        isLight = Not isLight
                    End If

                    Dim value As Byte = If(isLight, CByte(255), CByte(0))
                    Dim di As Integer = dstRow + (x * 3)
                    dstBytes(di) = value
                    dstBytes(di + 1) = value
                    dstBytes(di + 2) = value
                Next
            Next

            Marshal.Copy(dstBytes, 0, dstData.Scan0, dstBytes.Length)
        Finally
            If srcData IsNot Nothing Then
                source.UnlockBits(srcData)
            End If
            If dstData IsNot Nothing Then
                outBmp.UnlockBits(dstData)
            End If
        End Try
        Return outBmp
    End Function

    Private Shared Function IsolateLightText(source As Bitmap) As Bitmap
        Dim outBmp As New Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb)
        Dim rect As New Rectangle(0, 0, source.Width, source.Height)
        Dim srcData As BitmapData = Nothing
        Dim dstData As BitmapData = Nothing
        Try
            srcData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb)
            dstData = outBmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb)
            Dim srcBytes(Math.Abs(srcData.Stride) * source.Height - 1) As Byte
            Dim dstBytes(Math.Abs(dstData.Stride) * outBmp.Height - 1) As Byte
            Marshal.Copy(srcData.Scan0, srcBytes, 0, srcBytes.Length)

            For y As Integer = 0 To source.Height - 1
                Dim srcRow As Integer = y * srcData.Stride
                Dim dstRow As Integer = y * dstData.Stride
                For x As Integer = 0 To source.Width - 1
                    Dim si As Integer = srcRow + (x * 3)
                    Dim b As Integer = srcBytes(si)
                    Dim g As Integer = srcBytes(si + 1)
                    Dim r As Integer = srcBytes(si + 2)
                    Dim bright As Integer = r + g + b
                    Dim isText As Boolean =
                        bright >= 350 OrElse
                        (r >= 150 AndAlso g >= 150) OrElse
                        (r >= 170 AndAlso g >= 130)
                    Dim value As Byte = If(isText, CByte(255), CByte(0))
                    Dim di As Integer = dstRow + (x * 3)
                    dstBytes(di) = value
                    dstBytes(di + 1) = value
                    dstBytes(di + 2) = value
                Next
            Next

            Marshal.Copy(dstBytes, 0, dstData.Scan0, dstBytes.Length)
        Finally
            If srcData IsNot Nothing Then
                source.UnlockBits(srcData)
            End If
            If dstData IsNot Nothing Then
                outBmp.UnlockBits(dstData)
            End If
        End Try
        Return outBmp
    End Function

    Private Shared Function IsolateWhiteDigits(source As Bitmap) As Bitmap
        Dim outBmp As New Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb)
        Dim rect As New Rectangle(0, 0, source.Width, source.Height)
        Dim srcData As BitmapData = Nothing
        Dim dstData As BitmapData = Nothing
        Try
            srcData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb)
            dstData = outBmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb)
            Dim srcBytes(Math.Abs(srcData.Stride) * source.Height - 1) As Byte
            Dim dstBytes(Math.Abs(dstData.Stride) * outBmp.Height - 1) As Byte
            Marshal.Copy(srcData.Scan0, srcBytes, 0, srcBytes.Length)

            For y As Integer = 0 To source.Height - 1
                Dim srcRow As Integer = y * srcData.Stride
                Dim dstRow As Integer = y * dstData.Stride
                For x As Integer = 0 To source.Width - 1
                    Dim si As Integer = srcRow + (x * 3)
                    Dim b As Integer = srcBytes(si)
                    Dim g As Integer = srcBytes(si + 1)
                    Dim r As Integer = srcBytes(si + 2)
                    Dim maxChannel As Integer = Math.Max(r, Math.Max(g, b))
                    Dim minChannel As Integer = Math.Min(r, Math.Min(g, b))
                    Dim isNearWhite As Boolean = maxChannel >= 165 AndAlso (maxChannel - minChannel) <= 65
                    Dim value As Byte = If(isNearWhite, CByte(255), CByte(0))
                    Dim di As Integer = dstRow + (x * 3)
                    dstBytes(di) = value
                    dstBytes(di + 1) = value
                    dstBytes(di + 2) = value
                Next
            Next

            Marshal.Copy(dstBytes, 0, dstData.Scan0, dstBytes.Length)
        Finally
            If srcData IsNot Nothing Then
                source.UnlockBits(srcData)
            End If
            If dstData IsNot Nothing Then
                outBmp.UnlockBits(dstData)
            End If
        End Try
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
