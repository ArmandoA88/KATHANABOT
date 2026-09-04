Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports System.Text.Json.Serialization
Imports System.Threading
Imports System.Threading.Tasks

Friend Class QuizSolveResult
    <JsonPropertyName("question_text")>
    Public Property QuestionText As String = ""
    <JsonPropertyName("answer_text")>
    Public Property AnswerText As String = ""
    <JsonPropertyName("button_number")>
    Public Property ButtonNumber As Integer
    <JsonPropertyName("grid_column")>
    Public Property GridColumn As Integer
    <JsonPropertyName("grid_row")>
    Public Property GridRow As Integer
    <JsonPropertyName("confidence")>
    Public Property Confidence As Double
    <JsonPropertyName("is_guess")>
    Public Property IsGuess As Boolean
End Class

Friend NotInheritable Class QuizImageTools
    Public Const LocationGridSize As Integer = 16

    Private Sub New()
    End Sub

    Public Shared Function ScaleRegion(region As RectRegion,
                                       referenceWidth As Integer,
                                       referenceHeight As Integer,
                                       clientWidth As Integer,
                                       clientHeight As Integer) As Rectangle
        If region Is Nothing OrElse referenceWidth <= 0 OrElse referenceHeight <= 0 OrElse clientWidth <= 0 OrElse clientHeight <= 0 Then
            Return Rectangle.Empty
        End If
        Dim scaleX As Double = clientWidth / CDbl(referenceWidth)
        Dim scaleY As Double = clientHeight / CDbl(referenceHeight)
        Dim scaled As New RectRegion(
            CInt(Math.Round(region.X * scaleX)),
            CInt(Math.Round(region.Y * scaleY)),
            Math.Max(1, CInt(Math.Round(region.W * scaleX))),
            Math.Max(1, CInt(Math.Round(region.H * scaleY))))
        Return scaled.Clamp(clientWidth, clientHeight)
    End Function

    Public Shared Function Crop(source As Bitmap, area As Rectangle) As Bitmap
        If source Is Nothing Then Throw New ArgumentNullException(NameOf(source))
        area.Intersect(New Rectangle(0, 0, source.Width, source.Height))
        If area.Width <= 0 OrElse area.Height <= 0 Then Throw New ArgumentException("Crop area is outside the image.")
        Return source.Clone(area, PixelFormat.Format24bppRgb)
    End Function

    Public Shared Function DetectAnswerButtons(source As Bitmap) As List(Of Rectangle)
        Dim results As New List(Of Rectangle)()
        If source Is Nothing OrElse source.Width < 40 OrElse source.Height < 20 Then Return results

        Using working As New Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb)
            Using g As Graphics = Graphics.FromImage(working)
                g.DrawImageUnscaled(source, 0, 0)
            End Using

            Dim bounds As New Rectangle(0, 0, working.Width, working.Height)
            Dim data As BitmapData = working.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb)
            Try
                Dim stride As Integer = Math.Abs(data.Stride)
                Dim bytes(stride * working.Height - 1) As Byte
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length)
                Dim warm(working.Width * working.Height - 1) As Boolean

                For y As Integer = 0 To working.Height - 1
                    Dim row As Integer = y * stride
                    For x As Integer = 0 To working.Width - 1
                        Dim p As Integer = row + x * 3
                        Dim b As Integer = bytes(p)
                        Dim gr As Integer = bytes(p + 1)
                        Dim r As Integer = bytes(p + 2)
                        Dim brightness As Integer = Math.Max(r, Math.Max(gr, b))
                        warm(y * working.Width + x) =
                            brightness >= 72 AndAlso r >= 68 AndAlso gr >= 48 AndAlso
                            r >= CInt(b * 0.78R) AndAlso gr >= CInt(b * 0.53R) AndAlso
                            (r + gr - b) >= 65 AndAlso (r - b >= 12 OrElse gr - b >= 8)
                    Next
                Next

                ' A one-pixel expansion reconnects anti-aliased gold borders without joining the
                ' separate answer buttons, whose gaps are much larger.
                Dim expanded = CType(warm.Clone(), Boolean())
                For y As Integer = 1 To working.Height - 2
                    For x As Integer = 1 To working.Width - 2
                        Dim index As Integer = y * working.Width + x
                        If warm(index) Then
                            expanded(index - 1) = True
                            expanded(index + 1) = True
                            expanded(index - working.Width) = True
                            expanded(index + working.Width) = True
                        End If
                    Next
                Next

                Dim visited(expanded.Length - 1) As Boolean
                Dim queue(expanded.Length - 1) As Integer
                Dim minWidth As Integer = Math.Max(55, CInt(working.Width * 0.14R))
                Dim minHeight As Integer = Math.Max(18, CInt(working.Height * 0.055R))

                For seed As Integer = 0 To expanded.Length - 1
                    If Not expanded(seed) OrElse visited(seed) Then Continue For
                    Dim head As Integer = 0
                    Dim tail As Integer = 1
                    queue(0) = seed
                    visited(seed) = True
                    Dim minX As Integer = seed Mod working.Width
                    Dim maxX As Integer = minX
                    Dim minY As Integer = seed \ working.Width
                    Dim maxY As Integer = minY
                    Dim count As Integer = 0

                    While head < tail
                        Dim current As Integer = queue(head)
                        head += 1
                        count += 1
                        Dim x As Integer = current Mod working.Width
                        Dim y As Integer = current \ working.Width
                        minX = Math.Min(minX, x) : maxX = Math.Max(maxX, x)
                        minY = Math.Min(minY, y) : maxY = Math.Max(maxY, y)

                        If x > 0 Then EnqueuePixel(current - 1, expanded, visited, queue, tail)
                        If x + 1 < working.Width Then EnqueuePixel(current + 1, expanded, visited, queue, tail)
                        If y > 0 Then EnqueuePixel(current - working.Width, expanded, visited, queue, tail)
                        If y + 1 < working.Height Then EnqueuePixel(current + working.Width, expanded, visited, queue, tail)
                    End While

                    Dim candidate As New Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1)
                    Dim density As Double = count / CDbl(Math.Max(1, candidate.Width * candidate.Height))
                    If candidate.Width >= minWidth AndAlso candidate.Height >= minHeight AndAlso
                       candidate.Width <= CInt(working.Width * 0.98R) AndAlso
                       candidate.Height <= CInt(working.Height * 0.48R) AndAlso
                       candidate.Width / CDbl(Math.Max(1, candidate.Height)) >= 1.45R AndAlso density >= 0.025R AndAlso
                       HasRectangularBorder(expanded, working.Width, working.Height, candidate) Then
                        candidate.Inflate(2, 2)
                        candidate.Intersect(bounds)
                        results.Add(candidate)
                   End If
                Next
            Finally
                working.UnlockBits(data)
            End Try
        End Using

        results = RemoveDuplicateRectangles(results)
        Return SortIntoReadingOrder(results)
    End Function

    Private Shared Function HasRectangularBorder(mask() As Boolean, imageWidth As Integer, imageHeight As Integer, area As Rectangle) As Boolean
        Dim band As Integer = Math.Max(2, Math.Min(5, Math.Min(area.Width, area.Height) \ 8))
        Dim horizontalSamples As Integer = 0
        Dim topHits As Integer = 0
        Dim bottomHits As Integer = 0
        For x As Integer = area.Left To area.Right - 1
            horizontalSamples += 1
            For offset As Integer = 0 To band - 1
                Dim topY = Math.Min(imageHeight - 1, area.Top + offset)
                Dim bottomY = Math.Max(0, area.Bottom - 1 - offset)
                If mask(topY * imageWidth + x) Then topHits += 1 : Exit For
            Next
            For offset As Integer = 0 To band - 1
                Dim bottomY = Math.Max(0, area.Bottom - 1 - offset)
                If mask(bottomY * imageWidth + x) Then bottomHits += 1 : Exit For
            Next
        Next

        Dim verticalSamples As Integer = 0
        Dim leftHits As Integer = 0
        Dim rightHits As Integer = 0
        For y As Integer = area.Top To area.Bottom - 1
            verticalSamples += 1
            For offset As Integer = 0 To band - 1
                Dim leftX = Math.Min(imageWidth - 1, area.Left + offset)
                If mask(y * imageWidth + leftX) Then leftHits += 1 : Exit For
            Next
            For offset As Integer = 0 To band - 1
                Dim rightX = Math.Max(0, area.Right - 1 - offset)
                If mask(y * imageWidth + rightX) Then rightHits += 1 : Exit For
            Next
        Next

        Return horizontalSamples > 0 AndAlso verticalSamples > 0 AndAlso
               topHits / CDbl(horizontalSamples) >= 0.3R AndAlso
               bottomHits / CDbl(horizontalSamples) >= 0.3R AndAlso
               leftHits / CDbl(verticalSamples) >= 0.28R AndAlso
               rightHits / CDbl(verticalSamples) >= 0.28R
    End Function

    Public Shared Function IsPlausibleQuizLayout(buttons As IReadOnlyList(Of Rectangle)) As Boolean
        If buttons Is Nothing OrElse buttons.Count < 2 OrElse buttons.Count > 12 Then Return False
        Dim widths = buttons.Select(Function(r) r.Width).OrderBy(Function(v) v).ToArray()
        Dim heights = buttons.Select(Function(r) r.Height).OrderBy(Function(v) v).ToArray()
        Dim medianWidth = widths(widths.Length \ 2)
        Dim medianHeight = heights(heights.Length \ 2)
        Dim matching = buttons.Where(
            Function(r)
                Return r.Width >= medianWidth * 0.62R AndAlso r.Width <= medianWidth * 1.62R AndAlso
                       r.Height >= medianHeight * 0.62R AndAlso r.Height <= medianHeight * 1.62R
            End Function).Count()
        Return matching >= Math.Max(2, CInt(Math.Ceiling(buttons.Count * 0.75R)))
    End Function

    Private Shared Sub EnqueuePixel(index As Integer, mask() As Boolean, visited() As Boolean, queue() As Integer, ByRef tail As Integer)
        If index < 0 OrElse index >= mask.Length OrElse visited(index) OrElse Not mask(index) Then Return
        visited(index) = True
        queue(tail) = index
        tail += 1
    End Sub

    Private Shared Function RemoveDuplicateRectangles(input As List(Of Rectangle)) As List(Of Rectangle)
        Dim ordered = input.OrderByDescending(Function(r) r.Width * r.Height).ToList()
        Dim kept As New List(Of Rectangle)()
        For Each candidate In ordered
            Dim duplicate As Boolean = kept.Any(
                Function(existing)
                    Dim intersection As Rectangle = Rectangle.Intersect(existing, candidate)
                    Dim smaller As Integer = Math.Min(existing.Width * existing.Height, candidate.Width * candidate.Height)
                    Return smaller > 0 AndAlso intersection.Width * intersection.Height >= smaller * 0.72R
                End Function)
            If Not duplicate Then kept.Add(candidate)
        Next
        Return kept
    End Function

    Private Shared Function SortIntoReadingOrder(input As List(Of Rectangle)) As List(Of Rectangle)
        Dim remaining = input.OrderBy(Function(r) r.Top).ThenBy(Function(r) r.Left).ToList()
        Dim result As New List(Of Rectangle)()
        While remaining.Count > 0
            Dim first = remaining(0)
            Dim rowTolerance As Double = Math.Max(10.0R, first.Height * 0.65R)
            Dim row = remaining.Where(Function(r) Math.Abs((r.Top + r.Height / 2.0R) - (first.Top + first.Height / 2.0R)) <= rowTolerance).OrderBy(Function(r) r.Left).ToList()
            result.AddRange(row)
            For Each item In row
                remaining.Remove(item)
            Next
        End While
        Return result
    End Function

    Public Shared Function CreateAnnotatedQuiz(quizImage As Bitmap,
                                               answerAreaWithinQuiz As Rectangle,
                                               buttons As IReadOnlyList(Of Rectangle),
                                               Optional clickedButtonNumber As Integer = 0,
                                               Optional clickPointWithinQuiz As System.Drawing.Point? = Nothing) As Bitmap
        Dim annotated As New Bitmap(quizImage)
        Using g As Graphics = Graphics.FromImage(annotated)
            g.SmoothingMode = SmoothingMode.AntiAlias
            Using gridPen As New Pen(Color.FromArgb(155, 0, 220, 255), 1.0F),
                  answerPen As New Pen(Color.Lime, 3.0F),
                  labelBrush As New SolidBrush(Color.FromArgb(225, 0, 0, 0)),
                  textBrush As New SolidBrush(Color.White),
                  font As New Font("Segoe UI", 12.0F, FontStyle.Bold)
                g.DrawRectangle(answerPen, answerAreaWithinQuiz)
                For i As Integer = 1 To LocationGridSize - 1
                    Dim x As Single = CSng(answerAreaWithinQuiz.Left + answerAreaWithinQuiz.Width * i / CDbl(LocationGridSize))
                    Dim y As Single = CSng(answerAreaWithinQuiz.Top + answerAreaWithinQuiz.Height * i / CDbl(LocationGridSize))
                    g.DrawLine(gridPen, x, answerAreaWithinQuiz.Top, x, answerAreaWithinQuiz.Bottom)
                    g.DrawLine(gridPen, answerAreaWithinQuiz.Left, y, answerAreaWithinQuiz.Right, y)
                Next
                For i As Integer = 0 To buttons.Count - 1
                    Dim local = buttons(i)
                    Dim mapped As New Rectangle(answerAreaWithinQuiz.X + local.X, answerAreaWithinQuiz.Y + local.Y, local.Width, local.Height)
                    If clickedButtonNumber = i + 1 Then
                        Using clickedPen As New Pen(Color.Red, 4.0F)
                            g.DrawRectangle(clickedPen, mapped)
                        End Using
                    Else
                        g.DrawRectangle(answerPen, mapped)
                    End If
                    Dim badge As New Rectangle(mapped.Left + 4, mapped.Top + 4, 31, 25)
                    g.FillRectangle(labelBrush, badge)
                    g.DrawString((i + 1).ToString(), font, textBrush, badge.Left + 5, badge.Top + 1)
                Next
                If clickPointWithinQuiz.HasValue Then
                    Dim clickPoint = clickPointWithinQuiz.Value
                    Using clickBrush As New SolidBrush(Color.FromArgb(175, Color.Red)), clickPen As New Pen(Color.White, 2.0F)
                        g.FillEllipse(clickBrush, clickPoint.X - 10, clickPoint.Y - 10, 20, 20)
                        g.DrawEllipse(clickPen, clickPoint.X - 10, clickPoint.Y - 10, 20, 20)
                        g.DrawLine(clickPen, clickPoint.X - 14, clickPoint.Y, clickPoint.X + 14, clickPoint.Y)
                        g.DrawLine(clickPen, clickPoint.X, clickPoint.Y - 14, clickPoint.X, clickPoint.Y + 14)
                    End Using
                End If
            End Using
        End Using
        Return annotated
    End Function

    Public Shared Function PerceptualHash(source As Bitmap) As String
        Using small As New Bitmap(8, 8, PixelFormat.Format24bppRgb)
            Using g = Graphics.FromImage(small)
                g.InterpolationMode = InterpolationMode.HighQualityBilinear
                g.DrawImage(source, New Rectangle(0, 0, 8, 8))
            End Using
            Dim values As New List(Of Integer)(64)
            For y As Integer = 0 To 7
                For x As Integer = 0 To 7
                    Dim c = small.GetPixel(x, y)
                    values.Add((c.R * 30 + c.G * 59 + c.B * 11) \ 100)
                Next
            Next
            Dim average As Double = values.Average()
            Dim bits As ULong = 0UL
            For i As Integer = 0 To 63
                If values(i) >= average Then bits = bits Or (1UL << i)
            Next
            Return bits.ToString("X16")
        End Using
    End Function
End Class

Friend NotInheritable Class QuizOpenAiClient
    Private Shared ReadOnly Client As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(20)}

    Private Sub New()
    End Sub

    Public Shared Async Function SolveAsync(apiKey As String, model As String, cleanImage As Bitmap, annotatedImage As Bitmap, cancellationToken As CancellationToken) As Task(Of QuizSolveResult)
        Dim cleanDataUrl As String
        Using stream As New MemoryStream()
            cleanImage.Save(stream, ImageFormat.Png)
            cleanDataUrl = "data:image/png;base64," & Convert.ToBase64String(stream.ToArray())
        End Using
        Dim annotatedDataUrl As String
        Using stream As New MemoryStream()
            annotatedImage.Save(stream, ImageFormat.Png)
            annotatedDataUrl = "data:image/png;base64," & Convert.ToBase64String(stream.ToArray())
        End Using

        Dim payload As JsonObject = BuildPayload(model, cleanDataUrl, annotatedDataUrl, True)
        Dim response = Await PostAsync(apiKey, payload, cancellationToken).ConfigureAwait(False)
        If response.StatusCode = Net.HttpStatusCode.BadRequest Then
            Dim firstError As String = Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
            response.Dispose()
            If firstError.IndexOf("service_tier", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               firstError.IndexOf("priority", StringComparison.OrdinalIgnoreCase) >= 0 Then
                response = Await PostAsync(apiKey, BuildPayload(model, cleanDataUrl, annotatedDataUrl, False), cancellationToken).ConfigureAwait(False)
            Else
                Throw New InvalidOperationException("OpenAI request failed: " & LimitError(firstError))
            End If
        End If

        Using response
            Dim body As String = Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
            If Not response.IsSuccessStatusCode Then
                Throw New InvalidOperationException($"OpenAI request failed ({CInt(response.StatusCode)}): {LimitError(body)}")
            End If
            Return ParseResponse(body)
        End Using
    End Function

    Private Shared Function BuildPayload(model As String, cleanDataUrl As String, annotatedDataUrl As String, priority As Boolean) As JsonObject
        Dim schema As New JsonObject From {
            {"type", "object"},
            {"additionalProperties", False},
            {"properties", New JsonObject From {
                {"question_text", New JsonObject From {{"type", "string"}}},
                {"answer_text", New JsonObject From {{"type", "string"}}},
                {"button_number", New JsonObject From {{"type", "integer"}, {"minimum", 1}}},
                {"grid_column", New JsonObject From {{"type", "integer"}, {"minimum", 1}, {"maximum", QuizImageTools.LocationGridSize}}},
                {"grid_row", New JsonObject From {{"type", "integer"}, {"minimum", 1}, {"maximum", QuizImageTools.LocationGridSize}}},
                {"confidence", New JsonObject From {{"type", "number"}, {"minimum", 0}, {"maximum", 1}}},
                {"is_guess", New JsonObject From {{"type", "boolean"}}}
            }},
            {"required", New JsonArray("question_text", "answer_text", "button_number", "grid_column", "grid_row", "confidence", "is_guess")}
        }
        Dim format As New JsonObject From {
            {"type", "json_schema"},
            {"name", "quiz_answer"},
            {"strict", True},
            {"schema", schema}
        }
        Dim userContent As New JsonArray From {
            New JsonObject From {
                {"type", "input_text"},
                {"text", "Two images follow. The FIRST is clean: use it to read the complete question and answer text without grid lines breaking words. The SECOND is the same scene with detected buttons and a fine 16x16 location grid: use it only for geometry. Pick the correct answer, return its green/red button_number, and return the grid cell containing the CENTER of that whole button. Never locate by an individual word or partial word. If uncertain, make the best educated guess among visible choices and set is_guess=true."}
            },
            New JsonObject From {
                {"type", "input_image"},
                {"image_url", cleanDataUrl},
                {"detail", "high"}
            },
            New JsonObject From {
                {"type", "input_image"},
                {"image_url", annotatedDataUrl},
                {"detail", "high"}
            }
        }
        Dim input As New JsonArray From {
            New JsonObject From {
                {"role", "user"},
                {"content", userContent}
            }
        }
        Dim root As New JsonObject From {
            {"model", If(String.IsNullOrWhiteSpace(model), "gpt-5.4-mini", model.Trim())},
            {"instructions", "You solve an on-screen multiple-choice game quiz. Be fast, inspect exact button geometry, and always choose one visible answer."},
            {"input", input},
            {"reasoning", New JsonObject From {{"effort", "none"}}},
            {"text", New JsonObject From {{"format", format}}},
            {"max_output_tokens", 220},
            {"store", False}
        }
        If priority Then root("service_tier") = "priority"
        Return root
    End Function

    Private Shared Async Function PostAsync(apiKey As String, payload As JsonObject, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
        Using request As New HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses")
            request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", apiKey.Trim())
            request.Content = New StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
            Return Await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(False)
        End Using
    End Function

    Private Shared Function ParseResponse(body As String) As QuizSolveResult
        Using document = JsonDocument.Parse(body)
            Dim output As JsonElement
            If Not document.RootElement.TryGetProperty("output", output) Then Throw New InvalidOperationException("OpenAI returned no output.")
            For Each item In output.EnumerateArray()
                Dim content As JsonElement
                If Not item.TryGetProperty("content", content) Then Continue For
                For Each part In content.EnumerateArray()
                    Dim textNode As JsonElement
                    If part.TryGetProperty("text", textNode) AndAlso textNode.ValueKind = JsonValueKind.String Then
                        Dim result = JsonSerializer.Deserialize(Of QuizSolveResult)(textNode.GetString(), New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True})
                        If result IsNot Nothing Then Return result
                    End If
                Next
            Next
        End Using
        Throw New InvalidOperationException("OpenAI returned no quiz answer.")
    End Function

    Private Shared Function LimitError(value As String) As String
        value = If(value, "").Replace(ControlChars.Cr, " ").Replace(ControlChars.Lf, " ").Trim()
        Return If(value.Length <= 300, value, value.Substring(0, 300) & "...")
    End Function
End Class

Friend NotInheritable Class QuizSecretStore
    Private Const CryptProtectUiForbidden As UInteger = &H1UI

    <StructLayout(LayoutKind.Sequential)>
    Private Structure DataBlob
        Public Size As Integer
        Public Data As IntPtr
    End Structure

    <DllImport("crypt32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
    Private Shared Function CryptProtectData(ByRef input As DataBlob, description As String, entropy As IntPtr, reserved As IntPtr, prompt As IntPtr, flags As UInteger, ByRef output As DataBlob) As Boolean
    End Function

    <DllImport("crypt32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
    Private Shared Function CryptUnprotectData(ByRef input As DataBlob, ByRef description As IntPtr, entropy As IntPtr, reserved As IntPtr, prompt As IntPtr, flags As UInteger, ByRef output As DataBlob) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function LocalFree(memory As IntPtr) As IntPtr
    End Function

    Private Sub New()
    End Sub

    Public Shared Function Protect(secret As String) As String
        If String.IsNullOrWhiteSpace(secret) Then Return ""
        Dim clear = Encoding.UTF8.GetBytes(secret.Trim())
        Dim input As DataBlob = ToBlob(clear)
        Dim output As New DataBlob()
        Try
            If Not CryptProtectData(input, "Kathana quiz API key", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, output) Then
                Throw New ComponentModel.Win32Exception(Marshal.GetLastWin32Error())
            End If
            Dim encrypted(output.Size - 1) As Byte
            Marshal.Copy(output.Data, encrypted, 0, encrypted.Length)
            Return "dpapi:" & Convert.ToBase64String(encrypted)
        Finally
            If input.Data <> IntPtr.Zero Then Marshal.FreeHGlobal(input.Data)
            If output.Data <> IntPtr.Zero Then LocalFree(output.Data)
            CryptographicOperations.ZeroMemory(clear)
        End Try
    End Function

    Public Shared Function Unprotect(saved As String) As String
        If String.IsNullOrWhiteSpace(saved) OrElse Not saved.StartsWith("dpapi:", StringComparison.OrdinalIgnoreCase) Then Return ""
        Dim encrypted = Convert.FromBase64String(saved.Substring(6))
        Dim input As DataBlob = ToBlob(encrypted)
        Dim output As New DataBlob()
        Dim description As IntPtr = IntPtr.Zero
        Try
            If Not CryptUnprotectData(input, description, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, output) Then Return ""
            Dim clear(output.Size - 1) As Byte
            Marshal.Copy(output.Data, clear, 0, clear.Length)
            Try
                Return Encoding.UTF8.GetString(clear)
            Finally
                CryptographicOperations.ZeroMemory(clear)
            End Try
        Catch
            Return ""
        Finally
            If input.Data <> IntPtr.Zero Then Marshal.FreeHGlobal(input.Data)
            If output.Data <> IntPtr.Zero Then LocalFree(output.Data)
            If description <> IntPtr.Zero Then LocalFree(description)
            CryptographicOperations.ZeroMemory(encrypted)
        End Try
    End Function

    Private Shared Function ToBlob(bytes() As Byte) As DataBlob
        Dim blob As New DataBlob With {.Size = bytes.Length, .Data = Marshal.AllocHGlobal(bytes.Length)}
        Marshal.Copy(bytes, 0, blob.Data, bytes.Length)
        Return blob
    End Function
End Class
