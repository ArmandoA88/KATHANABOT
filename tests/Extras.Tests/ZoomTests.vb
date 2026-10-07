Imports System.Diagnostics
Imports System.IO
Imports System.Reflection
Imports System.Security.Cryptography
Imports System.Text

Friend Module ZoomTests
    Private checks As Integer

    Public Sub RunTests()
        checks = 0
        TestExactResources()
        TestSwapsAndRestore()
        TestMissingSettingsAndBackups()
        TestInvalidInstallationsAndScope()
        TestRunningGameGuards()
        TestReadOnlyAndLockedOriginal()
        TestFailedCommitRecoversOriginal()
        TestReparseAndDirectoryGuards()
        Console.WriteLine($"PASS: {checks} offline zoom assertions: exact x2/x3 resources, unique unchanged backups, latest restore, durable replacement/recovery, game-closed guards, scoped installations, read-only/locked files and reparse protection.")
    End Sub

    Private Sub Check(condition As Boolean, reason As String)
        checks += 1
        If Not condition Then Throw New InvalidOperationException("Extras zoom test: " & reason)
    End Sub

    Private Sub Fails(action As Action, expected As Type, reason As String)
        Dim failed As Boolean
        Try
            action()
        Catch ex As Exception
            failed = expected.IsAssignableFrom(ex.GetType())
        End Try
        Check(failed, reason)
    End Sub

    Private Function Hash(bytes As Byte()) As String
        Return Convert.ToHexString(SHA256.HashData(bytes))
    End Function

    Private Function TestService(root As String, Optional running As Func(Of Boolean) = Nothing, Optional commitFailure As Action = Nothing) As ExtrasZoomService
        Dim create = GetType(ExtrasZoomService).GetMethod("CreateForOwnedTests", BindingFlags.Static Or BindingFlags.NonPublic)
        Try
            Return DirectCast(create.Invoke(Nothing, {root, If(running, New Func(Of Boolean)(Function() False)), commitFailure}), ExtrasZoomService)
        Catch ex As TargetInvocationException When ex.InnerException IsNot Nothing
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw()
            Throw
        End Try
    End Function

    Private Sub TestExactResources()
        Dim x2 = ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X2)
        Dim x3 = ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X3)
        Check(x2.Length = 1503 AndAlso Hash(x2) = "1DC74003524758C59A3311EB8BEDF8C08D5A64A4342CA8FDC487B5E4BEE20B46", "x2 differs from the supplied binary")
        Check(x3.Length = 1518 AndAlso Hash(x3) = "D27C09A7539D565F0990B5BC0F36798587CECC81AF3B30AB9ECEAD66E63ED562", "x3 differs from the supplied binary")
        x2(0) = x2(0) Xor CByte(255)
        Check(Hash(ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X2)) = "1DC74003524758C59A3311EB8BEDF8C08D5A64A4342CA8FDC487B5E4BEE20B46", "callers can mutate future preset results")
        Fails(Sub() ExtrasZoomService.GetPresetBytes(CType(4, ExtrasZoomPreset)), GetType(ArgumentOutOfRangeException), "unsupported preset was accepted")
    End Sub

    Private Sub TestSwapsAndRestore()
        Using fixture As New InstallationFixture()
            Dim service = TestService(fixture.Root)
            Dim original = fixture.Original.ToArray()
            Dim first = service.ApplyPreset(fixture.Root, ExtrasZoomPreset.X2)
            Check(first.TargetPath = fixture.Target AndAlso first.AppliedPreset.HasValue AndAlso first.AppliedPreset.Value = ExtrasZoomPreset.X2 AndAlso first.RestoredFromBackupPath = "", "x2 result metadata is incorrect")
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X2)), "x2 target is not an exact binary replacement")
            Check(File.Exists(first.BackupPath) AndAlso File.ReadAllBytes(first.BackupPath).SequenceEqual(original), "first switch did not rename/preserve the original bytes")
            Dim second = service.ApplyPreset(fixture.Root, ExtrasZoomPreset.X3)
            Check(second.BackupPath <> first.BackupPath AndAlso File.ReadAllBytes(second.BackupPath).SequenceEqual(ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X2)), "second switch overwrote a backup or failed to preserve x2")
            Check(File.ReadAllBytes(first.BackupPath).SequenceEqual(original) AndAlso fixture.Backups().Length = 2, "later switch changed the first original backup")
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X3)), "x3 target differs from its binary resource")
            Dim restored = service.RestoreLatestBackup(fixture.Root)
            Check(restored.RestoredFromBackupPath = second.BackupPath AndAlso Not restored.AppliedPreset.HasValue, "Restore selected the wrong backup or reported a preset")
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X2)), "Restore latest did not recover the previous x2 settings")
            Check(File.ReadAllBytes(restored.BackupPath).SequenceEqual(ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X3)), "Restore discarded the current x3 settings")
            Check(File.ReadAllBytes(second.BackupPath).SequenceEqual(ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X2)) AndAlso fixture.Backups().Length = 3, "Restore consumed or edited the selected backup")
            Dim again = service.RestoreLatestBackup(fixture.Root)
            Check(again.RestoredFromBackupPath = restored.BackupPath AndAlso File.ReadAllBytes(fixture.Target).SequenceEqual(ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X3)), "second Restore could not recover its most recent previous settings")
            Dim duplicate = service.ApplyPreset(fixture.Root, ExtrasZoomPreset.X3)
            Check(File.Exists(duplicate.BackupPath) AndAlso fixture.Backups().Length = 5, "applying an identical preset skipped preservation of an existing file")
            Check(File.ReadAllBytes(first.BackupPath).SequenceEqual(original) AndAlso fixture.Temps().Length = 0, "successful swaps lost the original or leaked temporary files")
        End Using
    End Sub

    Private Sub TestMissingSettingsAndBackups()
        Using fixture As New InstallationFixture()
            Dim service = TestService(fixture.Root)
            Fails(Sub() service.RestoreLatestBackup(fixture.Root), GetType(FileNotFoundException), "restore without a backup was allowed")
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(fixture.Original), "failed restore changed existing settings")
            File.Delete(fixture.Target)
            Dim applied = service.ApplyPreset(fixture.Root, ExtrasZoomPreset.X2)
            Check(applied.BackupPath = "" AndAlso fixture.Backups().Length = 0, "a nonexistent original produced a fake backup")
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(ExtrasZoomService.GetPresetBytes(ExtrasZoomPreset.X2)), "missing engine.cfg was not created from the exact preset")
            Check(fixture.Temps().Length = 0, "creating new settings leaked a temporary file")
        End Using
    End Sub

    Private Sub TestInvalidInstallationsAndScope()
        Using fixture As New InstallationFixture(), other As New InstallationFixture()
            Dim service = TestService(fixture.Root)
            Fails(Sub() service.ApplyPreset("", ExtrasZoomPreset.X2), GetType(ArgumentException), "empty installation root was accepted")
            Fails(Sub() service.ApplyPreset("userdata", ExtrasZoomPreset.X2), GetType(ArgumentException), "relative installation root was accepted")
            Dim traversed = Path.Combine(fixture.Root, "..", Path.GetFileName(fixture.Root))
            Fails(Sub() service.ApplyPreset(traversed, ExtrasZoomPreset.X2), GetType(ArgumentException), "traversal segments were normalized into an accepted installation")
            Fails(Sub() service.ApplyPreset(other.Root, ExtrasZoomPreset.X2), GetType(ArgumentException), "the test seam modified another installation")
            Fails(Sub() TestService(Path.GetDirectoryName(fixture.Root)), GetType(ArgumentException), "the test seam accepted an unowned directory")
            File.Delete(Path.Combine(fixture.Root, "KathanaGame.exe"))
            Fails(Sub() service.ApplyPreset(fixture.Root, ExtrasZoomPreset.X2), GetType(FileNotFoundException), "installation without KathanaGame.exe was accepted")
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(fixture.Original) AndAlso fixture.Backups().Length = 0, "invalid installation checks changed settings")
            Check(File.ReadAllBytes(other.Target).SequenceEqual(other.Original), "an out-of-scope installation was touched")
        End Using
        Using fixture As New InstallationFixture()
            Directory.Move(fixture.Userdata, Path.Combine(fixture.Root, "saved-userdata"))
            Fails(Sub() TestService(fixture.Root).ApplyPreset(fixture.Root, ExtrasZoomPreset.X2), GetType(DirectoryNotFoundException), "missing userdata was created or accepted")
            Check(Not Directory.Exists(fixture.Userdata), "service created userdata for an invalid installation")
        End Using
    End Sub

    Private Sub TestRunningGameGuards()
        Using fixture As New InstallationFixture()
            Fails(Sub() TestService(fixture.Root, Function() True).ApplyPreset(fixture.Root, ExtrasZoomPreset.X2), GetType(InvalidOperationException), "running-game guard allowed replacement")
            Fails(Sub() TestService(fixture.Root, Function() ThrowGuard()).ApplyPreset(fixture.Root, ExtrasZoomPreset.X2), GetType(InvalidOperationException), "an unreadable game state did not fail closed")
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(fixture.Original) AndAlso fixture.Backups().Length = 0 AndAlso fixture.Temps().Length = 0, "initial game guards performed a write")
            Dim calls As Integer
            Dim opensDuringCommit As Func(Of Boolean) =
                Function()
                    calls += 1
                    Return calls >= 3
                End Function
            Fails(Sub() TestService(fixture.Root, opensDuringCommit).ApplyPreset(fixture.Root, ExtrasZoomPreset.X3), GetType(InvalidOperationException), "game opening immediately before commit was ignored")
            Check(calls = 3 AndAlso File.ReadAllBytes(fixture.Target).SequenceEqual(fixture.Original), "mid-operation game guard did not recover the original")
            Check(fixture.Backups().Length = 0 AndAlso fixture.Temps().Length = 0, "game-opening recovery left a failed backup or temporary file")
        End Using
    End Sub

    Private Function ThrowGuard() As Boolean
        Throw New UnauthorizedAccessException("owned offline game-check failure")
    End Function

    Private Sub TestReadOnlyAndLockedOriginal()
        Using fixture As New InstallationFixture()
            File.SetAttributes(fixture.Target, FileAttributes.ReadOnly)
            Try
                Fails(Sub() TestService(fixture.Root).ApplyPreset(fixture.Root, ExtrasZoomPreset.X2), GetType(IOException), "read-only original was replaced")
                Check(File.ReadAllBytes(fixture.Target).SequenceEqual(fixture.Original) AndAlso fixture.Backups().Length = 0, "read-only failure modified original settings")
                Check((File.GetAttributes(fixture.Target) And FileAttributes.ReadOnly) <> 0, "service removed a user's read-only attribute")
            Finally
                File.SetAttributes(fixture.Target, FileAttributes.Normal)
            End Try
            Using locked As New FileStream(fixture.Target, FileMode.Open, FileAccess.Read, FileShare.None)
                Fails(Sub() TestService(fixture.Root).ApplyPreset(fixture.Root, ExtrasZoomPreset.X3), GetType(IOException), "locked original was replaced")
            End Using
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(fixture.Original), "rename failure lost or altered a locked original")
            Check(fixture.Backups().Length = 0 AndAlso fixture.Temps().Length = 0, "locked-file failure leaked backup/temp files")
        End Using
    End Sub

    Private Sub TestFailedCommitRecoversOriginal()
        Using fixture As New InstallationFixture()
            Dim failure As Action = Sub() Throw New IOException("owned offline before-commit failure")
            Fails(Sub() TestService(fixture.Root, commitFailure:=failure).ApplyPreset(fixture.Root, ExtrasZoomPreset.X2), GetType(IOException), "injected commit failure was hidden")
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(fixture.Original), "commit failure did not recover the renamed original")
            Check(fixture.Backups().Length = 0 AndAlso fixture.Temps().Length = 0, "successful recovery left orphan files")
            Dim recovered = TestService(fixture.Root).ApplyPreset(fixture.Root, ExtrasZoomPreset.X3)
            Check(File.ReadAllBytes(recovered.BackupPath).SequenceEqual(fixture.Original), "a later successful switch did not preserve the recovered original")
        End Using
    End Sub

    Private Sub TestReparseAndDirectoryGuards()
        Using fixture As New InstallationFixture(), outside As New InstallationFixture()
            Directory.Move(fixture.Userdata, Path.Combine(fixture.Root, "saved-userdata"))
            CreateJunction(fixture.Userdata, outside.Userdata)
            Fails(Sub() TestService(fixture.Root).ApplyPreset(fixture.Root, ExtrasZoomPreset.X2), GetType(IOException), "userdata junction escaping the installation was followed")
            Check(File.ReadAllBytes(outside.Target).SequenceEqual(outside.Original) AndAlso outside.Backups().Length = 0, "a junction changed files in another installation")
            Check(Directory.GetFiles(Path.Combine(fixture.Root, "saved-userdata")).Length = 1, "reparse rejection changed the preserved local userdata")
        End Using
        Using fixture As New InstallationFixture()
            Dim aliasRoot = Path.Combine(Path.GetTempPath(), "KathanaExtrasTests-" & Guid.NewGuid().ToString("N"))
            Try
                CreateJunction(aliasRoot, fixture.Root)
                Fails(Sub() TestService(aliasRoot).ApplyPreset(aliasRoot, ExtrasZoomPreset.X2), GetType(IOException), "installation-root junction was accepted")
                Check(File.ReadAllBytes(fixture.Target).SequenceEqual(fixture.Original), "root-junction rejection changed the destination")
            Finally
                DeleteOwnedTree(aliasRoot)
            End Try
            File.Delete(fixture.Target)
            Directory.CreateDirectory(fixture.Target)
            Fails(Sub() TestService(fixture.Root).ApplyPreset(fixture.Root, ExtrasZoomPreset.X2), GetType(IOException), "directory in place of engine.cfg was accepted")
            Check(Directory.Exists(fixture.Target) AndAlso fixture.Backups().Length = 0, "directory guard altered the target")
        End Using
    End Sub

    Private Sub CreateJunction(link As String, destination As String)
        VerifyOwnedPath(link)
        VerifyOwnedPath(destination)
        Dim start As New ProcessStartInfo("cmd.exe") With {.UseShellExecute = False, .CreateNoWindow = True,
            .RedirectStandardOutput = True, .RedirectStandardError = True}
        For Each argument In {"/d", "/c", "mklink", "/J", link, destination}
            start.ArgumentList.Add(argument)
        Next
        Using helper = Process.Start(start)
            If Not helper.WaitForExit(5000) Then
                helper.Kill(entireProcessTree:=True)
                Throw New TimeoutException("Owned junction setup did not finish.")
            End If
            Check(helper.ExitCode = 0 AndAlso (File.GetAttributes(link) And FileAttributes.ReparsePoint) <> 0, "owned directory-junction fixture could not be created")
        End Using
    End Sub

    Private Sub VerifyOwnedPath(scopedPath As String)
        Dim absolute = Path.GetFullPath(scopedPath)
        Dim temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) & Path.DirectorySeparatorChar
        If Not absolute.StartsWith(temp, StringComparison.OrdinalIgnoreCase) OrElse
            Not absolute.Substring(temp.Length).Split(Path.DirectorySeparatorChar)(0).StartsWith("KathanaExtrasTests-", StringComparison.Ordinal) Then
            Throw New InvalidOperationException("Refusing filesystem cleanup/setup outside the owned temporary fixtures.")
        End If
    End Sub

    Private Sub DeleteOwnedTree(path As String)
        VerifyOwnedPath(path)
        If Not Directory.Exists(path) AndAlso Not File.Exists(path) Then Return
        Dim attributes = File.GetAttributes(path)
        If (attributes And FileAttributes.ReparsePoint) <> 0 Then
            If (attributes And FileAttributes.Directory) <> 0 Then Directory.Delete(path) Else File.Delete(path)
            Return
        End If
        If (attributes And FileAttributes.Directory) = 0 Then
            File.SetAttributes(path, FileAttributes.Normal)
            File.Delete(path)
            Return
        End If
        For Each entry In Directory.EnumerateFileSystemEntries(path)
            DeleteOwnedTree(entry)
        Next
        Directory.Delete(path)
    End Sub

    Private NotInheritable Class InstallationFixture
        Implements IDisposable

        Public ReadOnly Root As String = Path.Combine(Path.GetTempPath(), "KathanaExtrasTests-" & Guid.NewGuid().ToString("N"))
        Public ReadOnly Original As Byte() = Enumerable.Range(0, 300).Select(Function(index) CByte((index * 37) Mod 256)).ToArray()
        Public ReadOnly Property Userdata As String
            Get
                Return Path.Combine(Root, "userdata")
            End Get
        End Property
        Public ReadOnly Property Target As String
            Get
                Return Path.Combine(Userdata, "engine.cfg")
            End Get
        End Property

        Public Sub New()
            VerifyOwnedPath(Root)
            Directory.CreateDirectory(Userdata)
            File.WriteAllBytes(Path.Combine(Root, "KathanaGame.exe"), Encoding.ASCII.GetBytes("owned offline installation fixture; never executable"))
            File.WriteAllBytes(Target, Original)
        End Sub

        Public Function Backups() As String()
            Return Directory.GetFiles(Userdata, "engine.cfg.*.backup")
        End Function
        Public Function Temps() As String()
            Return Directory.GetFiles(Userdata, ".engine.cfg.kathana-*.tmp")
        End Function
        Public Sub Dispose() Implements IDisposable.Dispose
            DeleteOwnedTree(Root)
        End Sub
    End Class
End Module
