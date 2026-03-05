$filePath = "c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\BotEngine.vb"
$content = Get-Content -Raw -Path $filePath

$newSendKey = @"
    Public Shared Function SendKey(hwnd As IntPtr, keyName As String, pressMs As Integer) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim vk As Integer
        If Not KeyMap.TryGetValue(keyName.ToUpperInvariant(), vk) Then
            Return False
        End If

        Dim scan As UInteger = NativeMethods.MapVirtualKey(CUInt(vk), 0UI)
        If vk = &HA4 OrElse vk = &HA5 OrElse vk = &H12 Then
            scan = NativeMethods.MapVirtualKey(CUInt(&H12), 0UI)
        End If

        Dim lparamDown As Integer = 1 Or (CInt(scan) << 16)
        If vk = &HA5 Then
            lparamDown = lparamDown Or &H1000000
        End If

        Dim lparamUp As Integer = lparamDown Or (1 << 30) Or (1 << 31)

        Dim msgDown As UInteger = CUInt(&H100)
        Dim msgUp As UInteger = CUInt(&H101)
        If vk = &HA4 OrElse vk = &HA5 OrElse vk = &H12 Then
            msgDown = CUInt(&H104)
            msgUp = CUInt(&H105)
        End If

        Try
            NativeMethods.PostMessage(hwnd, msgDown, New IntPtr(vk), New IntPtr(lparamDown))
            Thread.Sleep(Math.Max(5, pressMs))
            NativeMethods.PostMessage(hwnd, msgUp, New IntPtr(vk), New IntPtr(lparamUp))
            Return True
        Catch
            Return False
        End Try
    End Function
"@

$pattern = "(?s)    Public Shared Function SendKey.*?End Function"
$content = [regex]::Replace($content, $pattern, $newSendKey, 1)

Set-Content -Path $filePath -Value $content -NoNewline
Write-Host "SendKey updated."
