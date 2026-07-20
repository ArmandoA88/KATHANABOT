Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography

Friend NotInheritable Class SkillCooldownScaler
    Implements IDisposable

    Private Const SupportedGameSha256 As String = "4FE36C46E4A819862D1062DA7EB3914311FAAF3934C5714B1400359584C37E25"

    ' FUN_1400dc490: normal skill-cooldown duration before it is copied to quick-slot entries.
    Private Const InitialCooldownPatchRva As Integer = &HDC5A2
    Private Const InitialCooldownReturnRva As Integer = &HDC5B5
    Private Const InitialCooldownPatchLength As Integer = 19

    ' FUN_1400dc7a0: scale both server remaining time (EBP) and the queried full
    ' duration before the routine derives its total and elapsed-offset values.
    Private Const AdjustedCooldownPatchRva As Integer = &HDC814
    Private Const AdjustedCooldownReturnRva As Integer = &HDC822
    Private Const AdjustedCooldownPatchLength As Integer = 14
    Private Const GameTickFunctionRva As Integer = &H2AEC0

    ' FUN_140172d40: server acknowledgement deadline used by the automatic buff
    ' scheduler. It must match slowed cooldowns instead of reverting them to 1x.
    Private Const AutoBuffDeadlinePatchRva As Integer = &H172D5C
    Private Const AutoBuffDeadlineReturnRva As Integer = &H172D6B
    Private Const AutoBuffDeadlinePatchLength As Integer = 15

    Private Const InitialCodeOffset As Integer = 16
    Private Const AdjustedCodeOffset As Integer = 112
    Private Const AutoBuffCodeOffset As Integer = 256
    Private Const AllocationSize As UInteger = 384UI

    Private Const PROCESS_QUERY_INFORMATION As UInteger = &H400UI
    Private Const PROCESS_VM_OPERATION As UInteger = &H8UI
    Private Const PROCESS_VM_READ As UInteger = &H10UI
    Private Const PROCESS_VM_WRITE As UInteger = &H20UI
    Private Const THREAD_SUSPEND_RESUME As UInteger = &H2UI
    Private Const MEM_COMMIT As UInteger = &H1000UI
    Private Const MEM_RESERVE As UInteger = &H2000UI
    Private Const MEM_RELEASE As UInteger = &H8000UI
    Private Const PAGE_EXECUTE_READWRITE As UInteger = &H40UI

    Private ReadOnly _syncRoot As New Object()
    Private _processHandle As IntPtr = IntPtr.Zero
    Private _processId As UInteger
    Private _initialPatchAddress As IntPtr = IntPtr.Zero
    Private _adjustedPatchAddress As IntPtr = IntPtr.Zero
    Private _autoBuffPatchAddress As IntPtr = IntPtr.Zero
    Private _remoteAllocation As IntPtr = IntPtr.Zero
    Private _initialOriginalBytes As Byte() = Nothing
    Private _adjustedOriginalBytes As Byte() = Nothing
    Private _autoBuffOriginalBytes As Byte() = Nothing
    Private _initialInstalledBytes As Byte() = Nothing
    Private _adjustedInstalledBytes As Byte() = Nothing
    Private _autoBuffInstalledBytes As Byte() = Nothing
    Private _currentMultiplier As Single = 1.0F
    Private _nextVerificationTick As Long
    Private _disposed As Boolean

    Public Function TrySetMultiplier(gameWindow As IntPtr, multiplier As Single, ByRef errorMessage As String) As Boolean
        errorMessage = ""
        multiplier = Math.Max(0.1F, Math.Min(10.0F, multiplier))

        SyncLock _syncRoot
            If _disposed Then
                errorMessage = "The skill cooldown scaler has already been disposed."
                Return False
            End If

            Try
                Dim pid As UInteger = 0UI
                If gameWindow <> IntPtr.Zero Then
                    GetWindowThreadProcessId(gameWindow, pid)
                End If

                If Math.Abs(multiplier - 1.0F) < 0.001F OrElse pid = 0UI Then
                    DetachInternal()
                    _currentMultiplier = 1.0F
                    Return True
                End If

                If _processHandle = IntPtr.Zero OrElse _processId <> pid Then
                    DetachInternal()
                    AttachInternal(pid, multiplier)
                ElseIf Math.Abs(multiplier - _currentMultiplier) >= 0.001F Then
                    WriteSingle(_processHandle, _remoteAllocation, multiplier)
                ElseIf Environment.TickCount64 >= _nextVerificationTick AndAlso Not InstalledPatchesAreHealthy() Then
                    DetachInternal()
                    AttachInternal(pid, multiplier)
                End If

                _currentMultiplier = multiplier
                Return True
            Catch ex As Exception
                errorMessage = DescribeFailure(ex)
                DetachInternal()
                _currentMultiplier = 1.0F
                Return False
            End Try
        End SyncLock
    End Function

    Private Sub AttachInternal(pid As UInteger, multiplier As Single)
        Dim moduleBase As IntPtr
        Dim executablePath As String
        Try
            Using target As Process = Process.GetProcessById(CInt(pid))
                Dim mainModule As ProcessModule = target.MainModule
                If mainModule Is Nothing Then
                    Throw New InvalidOperationException("The main executable module was not returned.")
                End If
                moduleBase = mainModule.BaseAddress
                executablePath = mainModule.FileName
            End Using
        Catch ex As Exception
            Throw New InvalidOperationException("Unable to inspect the selected game's executable module.", ex)
        End Try

        If Not Path.GetFileName(executablePath).Equals("KathanaGame.exe", StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("Skill cooldown scaling is supported only for KathanaGame.exe.")
        End If

        Dim executableHash As String = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executablePath)))
        If Not executableHash.Equals(SupportedGameSha256, StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("This KathanaGame.exe build is not supported; its skill-cooldown layout differs from the verified offline build.")
        End If

        Dim access As UInteger = PROCESS_QUERY_INFORMATION Or PROCESS_VM_OPERATION Or PROCESS_VM_READ Or PROCESS_VM_WRITE
        Dim processHandle As IntPtr = OpenProcess(access, False, pid)
        If processHandle = IntPtr.Zero Then
            Throw New Win32Exception(Marshal.GetLastWin32Error(), "Unable to open KathanaGame.exe for skill cooldown scaling.")
        End If

        Try
            Dim initialAddress As IntPtr = Add(moduleBase, InitialCooldownPatchRva)
            Dim adjustedAddress As IntPtr = Add(moduleBase, AdjustedCooldownPatchRva)
            Dim autoBuffAddress As IntPtr = Add(moduleBase, AutoBuffDeadlinePatchRva)
            Dim initialOriginal As Byte() = ReadBytes(processHandle, initialAddress, InitialCooldownPatchLength)
            Dim adjustedOriginal As Byte() = ReadBytes(processHandle, adjustedAddress, AdjustedCooldownPatchLength)
            Dim autoBuffOriginal As Byte() = ReadBytes(processHandle, autoBuffAddress, AutoBuffDeadlinePatchLength)
            ValidateInitialPatchSite(initialOriginal)
            ValidateAdjustedPatchSite(adjustedOriginal)
            ValidateAutoBuffPatchSite(autoBuffOriginal)

            Dim allocation As IntPtr = VirtualAllocEx(processHandle, IntPtr.Zero, New UIntPtr(AllocationSize),
                                                       MEM_COMMIT Or MEM_RESERVE, PAGE_EXECUTE_READWRITE)
            If allocation = IntPtr.Zero Then
                Throw New Win32Exception(Marshal.GetLastWin32Error(), "Unable to allocate the skill cooldown code block.")
            End If

            Try
                Dim initialCodeAddress As IntPtr = Add(allocation, InitialCodeOffset)
                Dim adjustedCodeAddress As IntPtr = Add(allocation, AdjustedCodeOffset)
                Dim autoBuffCodeAddress As IntPtr = Add(allocation, AutoBuffCodeOffset)
                Dim initialCode As Byte() = BuildInitialCooldownCode(
                    allocation, initialOriginal, Add(moduleBase, InitialCooldownReturnRva))
                Dim adjustedCode As Byte() = BuildAdjustedCooldownCode(
                    allocation,
                    Add(moduleBase, GameTickFunctionRva),
                    Add(moduleBase, AdjustedCooldownReturnRva))
                Dim autoBuffCode As Byte() = BuildAutoBuffDeadlineCode(
                    allocation, autoBuffOriginal, Add(moduleBase, AutoBuffDeadlineReturnRva))

                WriteSingle(processHandle, allocation, multiplier)
                WriteBytes(processHandle, initialCodeAddress, initialCode)
                WriteBytes(processHandle, adjustedCodeAddress, adjustedCode)
                WriteBytes(processHandle, autoBuffCodeAddress, autoBuffCode)
                FlushInstructionCache(processHandle, initialCodeAddress, New UIntPtr(CUInt(initialCode.Length)))
                FlushInstructionCache(processHandle, adjustedCodeAddress, New UIntPtr(CUInt(adjustedCode.Length)))
                FlushInstructionCache(processHandle, autoBuffCodeAddress, New UIntPtr(CUInt(autoBuffCode.Length)))

                Dim initialPatch As Byte() = BuildAbsoluteJump(initialCodeAddress, InitialCooldownPatchLength)
                Dim adjustedPatch As Byte() = BuildAbsoluteJump(adjustedCodeAddress, AdjustedCooldownPatchLength)
                Dim autoBuffPatch As Byte() = BuildAbsoluteJump(autoBuffCodeAddress, AutoBuffDeadlinePatchLength)
                WriteProtectedCode(processHandle, pid, initialAddress, initialPatch)
                Try
                    WriteProtectedCode(processHandle, pid, adjustedAddress, adjustedPatch)
                    Try
                        WriteProtectedCode(processHandle, pid, autoBuffAddress, autoBuffPatch)
                    Catch
                        WriteProtectedCode(processHandle, pid, adjustedAddress, adjustedOriginal)
                        Throw
                    End Try
                Catch
                    WriteProtectedCode(processHandle, pid, initialAddress, initialOriginal)
                    Throw
                End Try

                _processHandle = processHandle
                _processId = pid
                _initialPatchAddress = initialAddress
                _adjustedPatchAddress = adjustedAddress
                _autoBuffPatchAddress = autoBuffAddress
                _remoteAllocation = allocation
                _initialOriginalBytes = initialOriginal
                _adjustedOriginalBytes = adjustedOriginal
                _autoBuffOriginalBytes = autoBuffOriginal
                _initialInstalledBytes = initialPatch
                _adjustedInstalledBytes = adjustedPatch
                _autoBuffInstalledBytes = autoBuffPatch
                _nextVerificationTick = Environment.TickCount64 + 1000L
                processHandle = IntPtr.Zero
                allocation = IntPtr.Zero
            Finally
                If allocation <> IntPtr.Zero Then
                    VirtualFreeEx(processHandle, allocation, UIntPtr.Zero, MEM_RELEASE)
                End If
            End Try
        Finally
            If processHandle <> IntPtr.Zero Then
                CloseHandle(processHandle)
            End If
        End Try
    End Sub

    Private Sub DetachInternal()
        If _processHandle = IntPtr.Zero Then
            ClearAttachmentState()
            Return
        End If

        Try
            If _autoBuffPatchAddress <> IntPtr.Zero AndAlso _autoBuffOriginalBytes IsNot Nothing Then
                WriteProtectedCode(_processHandle, _processId, _autoBuffPatchAddress, _autoBuffOriginalBytes)
            End If
            If _adjustedPatchAddress <> IntPtr.Zero AndAlso _adjustedOriginalBytes IsNot Nothing Then
                WriteProtectedCode(_processHandle, _processId, _adjustedPatchAddress, _adjustedOriginalBytes)
            End If
            If _initialPatchAddress <> IntPtr.Zero AndAlso _initialOriginalBytes IsNot Nothing Then
                WriteProtectedCode(_processHandle, _processId, _initialPatchAddress, _initialOriginalBytes)
            End If
            If _remoteAllocation <> IntPtr.Zero Then
                Threading.Thread.Sleep(10)
                VirtualFreeEx(_processHandle, _remoteAllocation, UIntPtr.Zero, MEM_RELEASE)
            End If
        Catch
            ' The target may already have exited, in which case its private memory is gone.
        Finally
            CloseHandle(_processHandle)
            ClearAttachmentState()
        End Try
    End Sub

    Private Sub ClearAttachmentState()
        _processHandle = IntPtr.Zero
        _processId = 0UI
        _initialPatchAddress = IntPtr.Zero
        _adjustedPatchAddress = IntPtr.Zero
        _autoBuffPatchAddress = IntPtr.Zero
        _remoteAllocation = IntPtr.Zero
        _initialOriginalBytes = Nothing
        _adjustedOriginalBytes = Nothing
        _autoBuffOriginalBytes = Nothing
        _initialInstalledBytes = Nothing
        _adjustedInstalledBytes = Nothing
        _autoBuffInstalledBytes = Nothing
        _nextVerificationTick = 0L
    End Sub

    Private Function InstalledPatchesAreHealthy() As Boolean
        _nextVerificationTick = Environment.TickCount64 + 1000L
        If _initialPatchAddress = IntPtr.Zero OrElse _adjustedPatchAddress = IntPtr.Zero OrElse
           _autoBuffPatchAddress = IntPtr.Zero OrElse _initialInstalledBytes Is Nothing OrElse
           _adjustedInstalledBytes Is Nothing OrElse _autoBuffInstalledBytes Is Nothing Then
            Return False
        End If

        Dim currentInitial As Byte() = ReadBytes(_processHandle, _initialPatchAddress, _initialInstalledBytes.Length)
        Dim currentAdjusted As Byte() = ReadBytes(_processHandle, _adjustedPatchAddress, _adjustedInstalledBytes.Length)
        Dim currentAutoBuff As Byte() = ReadBytes(_processHandle, _autoBuffPatchAddress, _autoBuffInstalledBytes.Length)
        Return BytesEqual(currentInitial, _initialInstalledBytes) AndAlso
               BytesEqual(currentAdjusted, _adjustedInstalledBytes) AndAlso
               BytesEqual(currentAutoBuff, _autoBuffInstalledBytes)
    End Function

    Private Shared Function BytesEqual(left As Byte(), right As Byte()) As Boolean
        If left Is Nothing OrElse right Is Nothing OrElse left.Length <> right.Length Then
            Return False
        End If
        For index As Integer = 0 To left.Length - 1
            If left(index) <> right(index) Then
                Return False
            End If
        Next
        Return True
    End Function

    Private Shared Sub ValidateInitialPatchSite(bytes As Byte())
        Dim expected As Byte() = {
            &H4C, &H8B, &H84, &H24, &H80, &H0, &H0, &H0,
            &HB8, &HFC, &H3, &H0, &H0, &H41, &HBD, &HF4, &H1, &H0, &H0
        }
        ValidateExactBytes(bytes, expected, "initial skill cooldown")
    End Sub

    Private Shared Sub ValidateAdjustedPatchSite(bytes As Byte())
        Dim expected As Byte() = {
            &HE8, &HA7, &HE6, &HF4, &HFF,
            &HB9, &HF4, &H1, &H0, &H0,
            &H89, &H44, &H24, &H34
        }
        ValidateExactBytes(bytes, expected, "adjusted skill cooldown")
    End Sub

    Private Shared Sub ValidateAutoBuffPatchSite(bytes As Byte())
        Dim expected As Byte() = {
            &H85, &HFF, &H41, &HB8, &H30, &H75, &H0, &H0,
            &H44, &H8B, &HC8, &H44, &HF, &H45, &HC7
        }
        ValidateExactBytes(bytes, expected, "automatic buff deadline")
    End Sub

    Private Shared Sub ValidateExactBytes(actual As Byte(), expected As Byte(), description As String)
        If actual Is Nothing OrElse actual.Length <> expected.Length Then
            Throw New InvalidOperationException($"Unable to read the verified {description} patch site.")
        End If
        For index As Integer = 0 To expected.Length - 1
            If actual(index) <> expected(index) Then
                Throw New InvalidOperationException($"The selected game does not match the verified {description} code signature.")
            End If
        Next
    End Sub

    Private Shared Function BuildInitialCooldownCode(factorAddress As IntPtr, original As Byte(), returnAddress As IntPtr) As Byte()
        Dim code As New List(Of Byte)()
        AppendScaleEdx(code, factorAddress)
        code.AddRange({&H89, &H54, &H24, &H34})                 ' mov dword ptr [rsp+34h],edx
        code.AddRange(original)
        AppendAbsoluteJump(code, returnAddress)
        Return code.ToArray()
    End Function

    Private Shared Function BuildAdjustedCooldownCode(
        factorAddress As IntPtr,
        gameTickFunctionAddress As IntPtr,
        returnAddress As IntPtr) As Byte()

        Dim code As New List(Of Byte)()
        AppendScaleEbp(code, factorAddress)
        AppendScalePositiveStackDuration(code, factorAddress)
        AppendMovRax(code, gameTickFunctionAddress)
        code.AddRange({&HFF, &HD0})                             ' call rax
        code.AddRange({&HB9, &HF4, &H1, &H0, &H0})             ' mov ecx,1f4h
        code.AddRange({&H89, &H44, &H24, &H34})                ' mov dword ptr [rsp+34h],eax
        AppendAbsoluteJump(code, returnAddress)
        Return code.ToArray()
    End Function

    Private Shared Function BuildAutoBuffDeadlineCode(
        factorAddress As IntPtr,
        original As Byte(),
        returnAddress As IntPtr) As Byte()

        Dim code As New List(Of Byte)()
        code.AddRange(original)
        code.AddRange({&H45, &H85, &HC0, &H7E, &H27})          ' test r8d,r8d / jle skip
        code.AddRange({&H66, &H41, &HF, &H6E, &HC0, &HF, &H5B, &HC0})
        AppendMovR11(code, factorAddress)
        code.AddRange({&HF3, &H41, &HF, &H5E, &H3, &HF3, &H44, &HF, &H2C, &HC0})
        code.AddRange({&H45, &H85, &HC0, &H7F, &H6, &H41, &HB8, &H1, &H0, &H0, &H0})
        AppendAbsoluteJump(code, returnAddress)
        Return code.ToArray()
    End Function

    Private Shared Sub AppendScaleEdx(code As List(Of Byte), factorAddress As IntPtr)
        code.AddRange({&H66, &HF, &H6E, &HC2, &HF, &H5B, &HC0})
        AppendMovR11(code, factorAddress)
        code.AddRange({&HF3, &H41, &HF, &H5E, &H3, &HF3, &HF, &H2C, &HD0})
        code.AddRange({&H85, &HD2, &H7F, &H5, &HBA, &H1, &H0, &H0, &H0})
    End Sub

    Private Shared Sub AppendScaleEbp(code As List(Of Byte), factorAddress As IntPtr)
        code.AddRange({&H66, &HF, &H6E, &HC5, &HF, &H5B, &HC0})
        AppendMovR11(code, factorAddress)
        code.AddRange({&HF3, &H41, &HF, &H5E, &H3, &HF3, &HF, &H2C, &HE8})
        code.AddRange({&H85, &HED, &H7F, &H5, &HBD, &H1, &H0, &H0, &H0})
    End Sub

    Private Shared Sub AppendScalePositiveStackDuration(code As List(Of Byte), factorAddress As IntPtr)
        code.AddRange({&H8B, &H84, &H24, &H90, &H0, &H0, &H0}) ' mov eax,[rsp+90h]
        code.AddRange({&H85, &HC0, &H7E, &H2A})                 ' test eax,eax / jle skip
        code.AddRange({&H66, &HF, &H6E, &HC0, &HF, &H5B, &HC0})
        AppendMovR11(code, factorAddress)
        code.AddRange({&HF3, &H41, &HF, &H5E, &H3, &HF3, &HF, &H2C, &HC0})
        code.AddRange({&H85, &HC0, &H7F, &H5, &HB8, &H1, &H0, &H0, &H0})
        code.AddRange({&H89, &H84, &H24, &H90, &H0, &H0, &H0}) ' mov [rsp+90h],eax
    End Sub

    Private Shared Function BuildAbsoluteJump(destination As IntPtr, length As Integer) As Byte()
        If length < 14 Then
            Throw New ArgumentOutOfRangeException(NameOf(length), "An absolute jump requires at least 14 bytes.")
        End If
        Dim patch(length - 1) As Byte
        Array.Fill(patch, CByte(&H90))
        patch(0) = &HFF
        patch(1) = &H25
        Array.Copy(BitConverter.GetBytes(destination.ToInt64()), 0, patch, 6, 8)
        Return patch
    End Function

    Private Shared Sub AppendAbsoluteJump(code As List(Of Byte), destination As IntPtr)
        code.AddRange({&HFF, &H25, &H0, &H0, &H0, &H0})
        code.AddRange(BitConverter.GetBytes(destination.ToInt64()))
    End Sub

    Private Shared Sub AppendMovRax(code As List(Of Byte), address As IntPtr)
        code.AddRange({CByte(&H48), CByte(&HB8)})
        code.AddRange(BitConverter.GetBytes(address.ToInt64()))
    End Sub

    Private Shared Sub AppendMovR11(code As List(Of Byte), address As IntPtr)
        code.AddRange({CByte(&H49), CByte(&HBB)})
        code.AddRange(BitConverter.GetBytes(address.ToInt64()))
    End Sub

    Private Shared Sub WriteProtectedCode(processHandle As IntPtr, pid As UInteger, address As IntPtr, bytes As Byte())
        Dim suspended As List(Of IntPtr) = SuspendProcessThreads(pid)
        Try
            Dim oldProtection As UInteger = 0UI
            If Not VirtualProtectEx(processHandle, address, New UIntPtr(CUInt(bytes.Length)), PAGE_EXECUTE_READWRITE, oldProtection) Then
                Throw New Win32Exception(Marshal.GetLastWin32Error(), "Unable to unlock the skill cooldown code page.")
            End If
            Try
                WriteBytes(processHandle, address, bytes)
                FlushInstructionCache(processHandle, address, New UIntPtr(CUInt(bytes.Length)))
            Finally
                Dim ignored As UInteger = 0UI
                VirtualProtectEx(processHandle, address, New UIntPtr(CUInt(bytes.Length)), oldProtection, ignored)
            End Try
        Finally
            ResumeProcessThreads(suspended)
        End Try
    End Sub

    Private Shared Function SuspendProcessThreads(pid As UInteger) As List(Of IntPtr)
        Dim suspended As New List(Of IntPtr)()
        Try
            Using target As Process = Process.GetProcessById(CInt(pid))
                For Each thread As ProcessThread In target.Threads
                    Dim threadHandle As IntPtr = OpenThread(THREAD_SUSPEND_RESUME, False, CUInt(thread.Id))
                    If threadHandle = IntPtr.Zero Then
                        Continue For
                    End If
                    If SuspendThread(threadHandle) = UInteger.MaxValue Then
                        CloseHandle(threadHandle)
                    Else
                        suspended.Add(threadHandle)
                    End If
                Next
            End Using
            Return suspended
        Catch
            ResumeProcessThreads(suspended)
            Throw
        End Try
    End Function

    Private Shared Sub ResumeProcessThreads(threadHandles As List(Of IntPtr))
        For index As Integer = threadHandles.Count - 1 To 0 Step -1
            ResumeThread(threadHandles(index))
            CloseHandle(threadHandles(index))
        Next
    End Sub

    Private Shared Function ReadBytes(processHandle As IntPtr, address As IntPtr, length As Integer) As Byte()
        Dim buffer(length - 1) As Byte
        Dim bytesRead As UIntPtr = UIntPtr.Zero
        If Not ReadProcessMemory(processHandle, address, buffer, New UIntPtr(CUInt(length)), bytesRead) OrElse
           bytesRead.ToUInt64() <> CULng(length) Then
            Throw New Win32Exception(Marshal.GetLastWin32Error(), "Unable to read the skill cooldown code.")
        End If
        Return buffer
    End Function

    Private Shared Sub WriteBytes(processHandle As IntPtr, address As IntPtr, bytes As Byte())
        Dim bytesWritten As UIntPtr = UIntPtr.Zero
        If Not WriteProcessMemory(processHandle, address, bytes, New UIntPtr(CUInt(bytes.Length)), bytesWritten) OrElse
           bytesWritten.ToUInt64() <> CULng(bytes.Length) Then
            Throw New Win32Exception(Marshal.GetLastWin32Error(), "Unable to write the skill cooldown code.")
        End If
    End Sub

    Private Shared Sub WriteSingle(processHandle As IntPtr, address As IntPtr, value As Single)
        WriteBytes(processHandle, address, BitConverter.GetBytes(value))
    End Sub

    Private Shared Function Add(address As IntPtr, offset As Integer) As IntPtr
        Return New IntPtr(address.ToInt64() + offset)
    End Function

    Private Shared Function DescribeFailure(exception As Exception) As String
        If IsAccessDenied(exception) Then
            If IsEasyAntiCheatActive() Then
                Return "Game memory access was denied while Easy Anti-Cheat is still active. Use the authorized offline/private client with anti-cheat fully inactive, then restart the game and this control panel."
            End If
            Return "Game memory access was denied by permissions or game protection. Run the authorized offline/private client and the control panel at compatible privilege levels."
        End If
        Return exception.Message
    End Function

    Private Shared Function IsAccessDenied(exception As Exception) As Boolean
        Dim current As Exception = exception
        While current IsNot Nothing
            Dim win32 As Win32Exception = TryCast(current, Win32Exception)
            If (win32 IsNot Nothing AndAlso win32.NativeErrorCode = 5) OrElse
               TypeOf current Is UnauthorizedAccessException Then
                Return True
            End If
            current = current.InnerException
        End While
        Return False
    End Function

    Private Shared Function IsEasyAntiCheatActive() As Boolean
        Dim processes As Process() = Array.Empty(Of Process)()
        Try
            processes = Process.GetProcessesByName("EasyAntiCheat_EOS")
            Return processes.Length > 0
        Catch
            Return False
        Finally
            For Each process As Process In processes
                process.Dispose()
            Next
        End Try
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        SyncLock _syncRoot
            If _disposed Then
                Return
            End If
            DetachInternal()
            _disposed = True
        End SyncLock
        GC.SuppressFinalize(Me)
    End Sub

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetWindowThreadProcessId(hWnd As IntPtr, ByRef processId As UInteger) As UInteger
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function OpenProcess(desiredAccess As UInteger, inheritHandle As Boolean, processId As UInteger) As IntPtr
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function CloseHandle(handle As IntPtr) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function ReadProcessMemory(processHandle As IntPtr, baseAddress As IntPtr, buffer As Byte(), size As UIntPtr, ByRef bytesRead As UIntPtr) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function WriteProcessMemory(processHandle As IntPtr, baseAddress As IntPtr, buffer As Byte(), size As UIntPtr, ByRef bytesWritten As UIntPtr) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function VirtualAllocEx(processHandle As IntPtr, address As IntPtr, size As UIntPtr, allocationType As UInteger, protection As UInteger) As IntPtr
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function VirtualFreeEx(processHandle As IntPtr, address As IntPtr, size As UIntPtr, freeType As UInteger) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function VirtualProtectEx(processHandle As IntPtr, address As IntPtr, size As UIntPtr, newProtection As UInteger, ByRef oldProtection As UInteger) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function FlushInstructionCache(processHandle As IntPtr, baseAddress As IntPtr, size As UIntPtr) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function OpenThread(desiredAccess As UInteger, inheritHandle As Boolean, threadId As UInteger) As IntPtr
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function SuspendThread(threadHandle As IntPtr) As UInteger
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function ResumeThread(threadHandle As IntPtr) As UInteger
    End Function
End Class
