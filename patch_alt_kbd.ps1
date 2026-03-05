$filePath = "c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\BotEngine.vb"
$content = Get-Content -Raw -Path $filePath

$newSendKey = @"
    <DllImport("user32.dll", SetLastError:=True)>
    Friend Shared Sub keybd_event(bVk As Byte, bScan As Byte, dwFlags As UInteger, dwExtraInfo As UIntPtr)
    End Sub

    Public Shared Function SendKey(hwnd As IntPtr, keyName As String, pressMs As Integer) As Boolean
        If hwnd = IntPtr.Zero Then
            Return False
        End If

        Dim vk As Integer
        If Not KeyMap.TryGetValue(keyName.ToUpperInvariant(), vk) Then
            Return False
        End If

        ' Use keybd_event for ALT keys as games often use GetAsyncKeyState which ignores PostMessage
        If vk = &HA4 OrElse vk = &HA5 OrElse vk = &H12 Then
            Dim scan As Byte = CByte(NativeMethods.MapVirtualKey(CUInt(&H12), 0UI))
            Dim KEYEVENTF_EXTENDEDKEY As UInteger = &H1
            Dim KEYEVENTF_KEYUP As UInteger = &H2
            
            Dim flagsDown As UInteger = 0
            Dim flagsUp As UInteger = KEYEVENTF_KEYUP
            If vk = &HA5 Then ' RMENU
                flagsDown = flagsDown Or KEYEVENTF_EXTENDEDKEY
                flagsUp = flagsUp Or KEYEVENTF_EXTENDEDKEY
            End If

            Try
                keybd_event(CByte(vk), scan, flagsDown, UIntPtr.Zero)
                Thread.Sleep(Math.Max(5, pressMs))
                keybd_event(CByte(vk), scan, flagsUp, UIntPtr.Zero)
                Return True
            Catch
                Return False
            End Try
        End If

        Dim scanPost As UInteger = NativeMethods.MapVirtualKey(CUInt(vk), 0UI)
        Dim lparamDown As Integer = 1 Or (CInt(scanPost) << 16)
        Dim lparamUp As Integer = lparamDown Or (1 << 30) Or (1 << 31)

        Try
            NativeMethods.PostMessage(hwnd, CUInt(&H100), New IntPtr(vk), New IntPtr(lparamDown))
            Thread.Sleep(Math.Max(5, pressMs))
            NativeMethods.PostMessage(hwnd, CUInt(&H101), New IntPtr(vk), New IntPtr(lparamUp))
            Return True
        Catch
            Return False
        End Try
    End Function
"@

$pattern = "(?s)    Public Shared Function SendKey.*?End Function"
$content = [regex]::Replace($content, $pattern, $newSendKey, 1)

Set-Content -Path $filePath -Value $content -NoNewline
Write-Host "SendKey updated for keybd_event."
