$ocrPath = "c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\OcrReader.vb"
$enginePath = "c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\BotEngine.vb"

$ocrContent = Get-Content -Raw -Path $ocrPath -Encoding UTF8
$engineContent = Get-Content -Raw -Path $enginePath -Encoding UTF8

# 1. Add ReadScreenText methods to OcrReader.vb

$newMethods = @"
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

    Private Shared Function ReadScreenTextStaFallback(source As Bitmap) As String
        Dim output As String = ""
        Dim done As New ManualResetEventSlim(False)

        Dim worker As New Thread(
            Sub()
                Try
                    output = ReadScreenTextInternal(source)
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

    Private Shared Function ReadScreenTextInternal(source As Bitmap) As String
        Dim engine = GetEngine()
        If engine Is Nothing Then
            Return ""
        End If

        ' Intentionally raw and 1:1 scale to prevent massive memory and CPU bloat
        ' when scanning an entire 1080p or 4K game client window.
        Return ReadRawTextAsync(engine, source).GetAwaiter().GetResult()
    End Function

    Public Shared Function LastError() As String
"@

$ocrContent = $ocrContent -replace 'Public Shared Function LastError\(\) As String', $newMethods

# 2. Update BotEngine.vb to use ReadScreenText

$engineTarget = 'Dim ocrText As String = OcrReader.ReadName(altFrame)'
$engineReplacement = 'Dim ocrText As String = OcrReader.ReadScreenText(altFrame)'
$engineContent = $engineContent -replace [regex]::Escape($engineTarget), $engineReplacement

Set-Content -Path $ocrPath -Value $ocrContent -Encoding UTF8 -NoNewline
Set-Content -Path $enginePath -Value $engineContent -Encoding UTF8 -NoNewline

Write-Host "Patched OCR and BotEngine for lightweight scanning."
