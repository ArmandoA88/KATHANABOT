$filePath = "c:\Users\Casa-Desktop\OneDrive - Dallas Independent School District\Desktop\KATHANABOT\ui\KathanaBotControlPanel\BotEngine.vb"
$content = Get-Content -Raw -Path $filePath

$importForeground = @"
    <DllImport("user32.dll", SetLastError:=True)>
    Friend Function GetForegroundWindow() As IntPtr
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
"@

# Only replace the FIRST occurrence
$content = [regex]::Replace($content, '<DllImport\("user32\.dll", SetLastError:=True\)>', $importForeground, 1)

$newAltLogic = @"
            Dim activeHwnd As IntPtr = NativeMethods.GetForegroundWindow()
            If activeHwnd = hwnd AndAlso (now - _lastRightAltAt).TotalMilliseconds >= 10000 Then
                _lastRightAltAt = now
                Dim scan As Byte = CByte(NativeMethods.MapVirtualKey(CUInt(&H12), 0UI))
                Dim KEYEVENTF_EXTENDEDKEY As UInteger = &H1
                Dim KEYEVENTF_KEYUP As UInteger = &H2
                
                Try
                    keybd_event(&HA5, scan, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero)
                    Thread.Sleep(150)
                    
                    Dim altFrame As Bitmap = CaptureClient(hwnd)
                    
                    Thread.Sleep(250)
                    keybd_event(&HA5, scan, KEYEVENTF_EXTENDEDKEY Or KEYEVENTF_KEYUP, UIntPtr.Zero)
                    
                    SetLastAction("RMENU (scan items)")
                    RaiseEvent LogLine("Auto right-alt scan (400ms).")
                    
                    If altFrame IsNot Nothing Then
                        Dim allowedNames As List(Of String) = cfg.LootAllowedNames
                        Task.Run(Sub()
                            Try
                                Dim ocrText As String = OcrReader.ReadName(altFrame)
                                altFrame.Dispose()
                                
                                If Not String.IsNullOrWhiteSpace(ocrText) AndAlso allowedNames IsNot Nothing Then
                                    Dim normOcr As String = ocrText.ToLowerInvariant()
                                    For Each item As String In allowedNames
                                        Dim normItem As String = item.ToLowerInvariant().Trim()
                                        If normItem <> "" AndAlso normOcr.Contains(normItem) Then
                                            System.Media.SystemSounds.Exclamation.Play()
                                            Console.Beep(800, 1000)
                                            Console.Beep(800, 1000)
                                            RaiseEvent LogLine("LOOT ALARM: Found " & item)
                                            Exit For
                                        End If
                                    Next
                                End If
                            Catch ex As Exception
                            End Try
                        End Sub)
                    End If
                Catch
                End Try
            End If
"@

$content = [regex]::Replace($content, "(?sm)^\s*If \(now - _lastRightAltAt\)\.TotalMilliseconds >= 10000 Then.*?End If", $newAltLogic, 1)

Set-Content -Path $filePath -Value $content -NoNewline
Write-Host "New alt logic added with GetForegroundWindow."
