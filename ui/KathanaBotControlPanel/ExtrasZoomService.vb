Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Text.RegularExpressions

Public Enum ExtrasZoomPreset
    X2 = 2
    X3 = 3
End Enum

Public NotInheritable Class ExtrasZoomResult
    Public ReadOnly Property TargetPath As String
    Public ReadOnly Property BackupPath As String
    Public ReadOnly Property RestoredFromBackupPath As String
    Public ReadOnly Property AppliedPreset As ExtrasZoomPreset?

    Friend Sub New(target As String, backup As String, restoredFrom As String, preset As ExtrasZoomPreset?)
        TargetPath = target
        BackupPath = backup
        RestoredFromBackupPath = restoredFrom
        AppliedPreset = preset
    End Sub
End Class

Public NotInheritable Class ExtrasZoomService
    Private Shared ReadOnly OperationGate As New Object()
    Private Shared ReadOnly BackupName As New Regex("^engine\.cfg\.[0-9]{8}T[0-9]{13}Z\.[a-f0-9]{32}\.backup$", RegexOptions.CultureInvariant)
    Private ReadOnly gameRunning As Func(Of Boolean)
    Private ReadOnly ownedTestRoot As String
    Private ReadOnly beforeCommit As Action

    Public Sub New()
        gameRunning = AddressOf IsAnyGameRunning
    End Sub

    Private Sub New(testRoot As String, running As Func(Of Boolean), commitFailure As Action)
        ownedTestRoot = testRoot
        gameRunning = running
        beforeCommit = commitFailure
    End Sub

    ' Only owned, uniquely named temporary installations can bypass the real-game
    ' guard or inject an offline filesystem failure. Production uses New().
    Friend Shared Function CreateForOwnedTests(root As String, running As Func(Of Boolean), Optional commitFailure As Action = Nothing) As ExtrasZoomService
        Dim full = CanonicalRoot(root)
        Dim temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))
        If Not String.Equals(Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase) OrElse
            Not Regex.IsMatch(Path.GetFileName(full), "^KathanaExtrasTests-[a-fA-F0-9]{32}$", RegexOptions.CultureInvariant) Then
            Throw New ArgumentException("The test seam only accepts its owned temporary installation.")
        End If
        If running Is Nothing Then Throw New ArgumentNullException(NameOf(running))
        Return New ExtrasZoomService(full, running, commitFailure)
    End Function

    Public Shared Function GetPresetBytes(preset As ExtrasZoomPreset) As Byte()
        Dim suffix As String
        Select Case preset
            Case ExtrasZoomPreset.X2
                suffix = "engine-x2.cfg"
            Case ExtrasZoomPreset.X3
                suffix = "engine-x3.cfg"
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(preset), "Choose zoom x2 or x3.")
        End Select
        Using resource = GetType(ExtrasZoomService).Assembly.GetManifestResourceStream("KathanaBotControlPanel.Extras." & suffix)
            If resource Is Nothing Then Throw New InvalidOperationException("The bundled zoom preset is missing.")
            Using data As New MemoryStream()
                resource.CopyTo(data)
                Dim result = data.ToArray()
                If result.Length = 0 Then Throw New InvalidOperationException("The bundled zoom preset is empty.")
                Return result
            End Using
        End Using
    End Function

    Public Function ApplyPreset(installationRoot As String, preset As ExtrasZoomPreset) As ExtrasZoomResult
        Dim bytes = GetPresetBytes(preset)
        SyncLock OperationGate
            Dim root = ValidateInstallation(installationRoot)
            EnsureGameClosed()
            Return ReplaceSettings(root, bytes, preset, "")
        End SyncLock
    End Function

    Public Function RestoreLatestBackup(installationRoot As String) As ExtrasZoomResult
        SyncLock OperationGate
            Dim root = ValidateInstallation(installationRoot)
            EnsureGameClosed()
            Dim userdata = Path.Combine(root, "userdata")
            Dim backup = Directory.EnumerateFiles(userdata, "engine.cfg.*.backup", SearchOption.TopDirectoryOnly).
                Where(Function(file) BackupName.IsMatch(Path.GetFileName(file))).
                OrderByDescending(Function(file) Path.GetFileName(file), StringComparer.Ordinal).FirstOrDefault()
            If backup Is Nothing Then Throw New FileNotFoundException("No zoom settings backup exists in this installation.")
            RequireRegularFile(backup)
            Dim bytes = File.ReadAllBytes(backup)
            Return ReplaceSettings(root, bytes, Nothing, backup)
        End SyncLock
    End Function

    Private Function ReplaceSettings(root As String, bytes As Byte(), preset As ExtrasZoomPreset?, restoredFrom As String) As ExtrasZoomResult
        Dim userdata = Path.Combine(root, "userdata")
        Dim target = Path.Combine(userdata, "engine.cfg")
        Dim temporary = Path.Combine(userdata, ".engine.cfg.kathana-" & Guid.NewGuid().ToString("N") & ".tmp")
        Dim backup As String = ""
        Dim movedOriginal As Boolean
        Try
            ValidateInstallation(root)
            If File.Exists(target) AndAlso (File.GetAttributes(target) And FileAttributes.ReadOnly) <> 0 Then
                Throw New IOException("engine.cfg is read-only. No settings were changed.")
            End If
            Using stream As New FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)
                stream.Write(bytes, 0, bytes.Length)
                stream.Flush(flushToDisk:=True)
            End Using
            ValidateInstallation(root)
            EnsureGameClosed()
            If File.Exists(target) Then
                backup = Path.Combine(userdata, "engine.cfg." & NextBackupTimestamp(userdata) &
                    "." & Guid.NewGuid().ToString("N") & ".backup")
                File.Move(target, backup) ' A unique rename preserves every original byte and never replaces a backup.
                movedOriginal = True
            End If
            beforeCommit?.Invoke()
            ValidateInstallation(root)
            EnsureGameClosed()
            File.Move(temporary, target) ' Same-directory rename; no truncation and no overwrite of an unexpected target.
            Return New ExtrasZoomResult(target, backup, restoredFrom, preset)
        Catch failure As Exception
            If movedOriginal Then
                Try
                    ValidateInstallation(root)
                    If File.Exists(target) OrElse Directory.Exists(target) Then
                        Throw New IOException("Another engine.cfg appeared before recovery.")
                    End If
                    File.Move(backup, target)
                Catch recovery As Exception
                    Throw New IOException("Zoom settings could not be applied. The original is preserved at " & backup &
                        "; automatic recovery also failed.", New AggregateException(failure, recovery))
                End Try
            End If
            Throw
        Finally
            ' This is the exact temporary file created above, never a directory or a recursive cleanup.
            If File.Exists(temporary) Then
                Try
                    File.Delete(temporary)
                Catch ex As IOException
                Catch ex As UnauthorizedAccessException
                End Try
            End If
        End Try
    End Function

    Private Function ValidateInstallation(value As String) As String
        Dim root = CanonicalRoot(value)
        If ownedTestRoot IsNot Nothing AndAlso Not String.Equals(root, ownedTestRoot, StringComparison.OrdinalIgnoreCase) Then
            Throw New ArgumentException("The owned test service cannot modify another installation.")
        End If
        If Not Directory.Exists(root) Then Throw New DirectoryNotFoundException("Select the installation folder containing KathanaGame.exe.")
        RequireSafeDirectoryChain(root)
        RequireRegularFile(Path.Combine(root, "KathanaGame.exe"))
        Dim userdata = Path.Combine(root, "userdata")
        If Not Directory.Exists(userdata) Then Throw New DirectoryNotFoundException("This installation has no userdata folder.")
        RequireSafeDirectoryChain(userdata)
        Dim target = Path.Combine(userdata, "engine.cfg")
        If Directory.Exists(target) Then Throw New IOException("engine.cfg must be a file.")
        ' Enumerating the parent also catches dangling symbolic links for which File.Exists is false.
        If Directory.EnumerateFileSystemEntries(userdata, "engine.cfg", SearchOption.TopDirectoryOnly).Any() Then RequireRegularFile(target)
        Return root
    End Function

    Private Shared Function NextBackupTimestamp(userdata As String) As String
        ' UTC clocks can repeat or move backwards. Keep generated backup names
        ' strictly ordered so Restore latest consistently means the previous settings.
        Dim ticks = DateTime.UtcNow.Ticks
        For Each backup In Directory.EnumerateFiles(userdata, "engine.cfg.*.backup", SearchOption.TopDirectoryOnly)
            Dim name = Path.GetFileName(backup)
            If Not BackupName.IsMatch(name) Then Continue For
            Dim stamp As DateTime
            If DateTime.TryParseExact(name.Substring(11, 23), "yyyyMMdd'T'HHmmssfffffff'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal, stamp) Then ticks = Math.Max(ticks, stamp.Ticks + 1)
        Next
        Return New DateTime(ticks, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmssfffffff'Z'", CultureInfo.InvariantCulture)
    End Function

    Private Shared Function CanonicalRoot(value As String) As String
        If String.IsNullOrWhiteSpace(value) OrElse Not Path.IsPathFullyQualified(value) OrElse
            value.StartsWith("\\?\", StringComparison.Ordinal) OrElse value.StartsWith("\\.\", StringComparison.Ordinal) Then
            Throw New ArgumentException("Choose a normal absolute Kathana installation folder.")
        End If
        If value.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(Function(part) part = "." OrElse part = "..") Then
            Throw New ArgumentException("Installation paths cannot contain traversal segments.")
        End If
        Return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value))
    End Function

    Private Shared Sub RequireSafeDirectoryChain(path As String)
        Dim current As New DirectoryInfo(path)
        While current IsNot Nothing
            If Not current.Exists Then Throw New DirectoryNotFoundException("The installation path no longer exists.")
            If (current.Attributes And FileAttributes.ReparsePoint) <> 0 Then
                Throw New IOException("Zoom settings cannot be changed through a junction or symbolic-link directory.")
            End If
            current = current.Parent
        End While
    End Sub

    Private Shared Sub RequireRegularFile(path As String)
        Dim attributes = File.GetAttributes(path)
        If (attributes And (FileAttributes.ReparsePoint Or FileAttributes.Directory)) <> 0 Then
            Throw New IOException("Zoom settings cannot use a symbolic link or directory in place of a file.")
        End If
    End Sub

    Private Sub EnsureGameClosed()
        Dim running As Boolean
        Try
            running = gameRunning()
        Catch ex As Exception
            Throw New InvalidOperationException("Could not verify that Kathana is closed. No settings were changed.", ex)
        End Try
        If running Then Throw New InvalidOperationException("Close KathanaGame before changing zoom settings.")
    End Sub

    Private Shared Function IsAnyGameRunning() As Boolean
        Dim processes = Process.GetProcessesByName("KathanaGame")
        Try
            Return processes.Length <> 0
        Finally
            For Each game In processes
                game.Dispose()
            Next
        End Try
    End Function
End Class
