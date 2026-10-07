Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json.Nodes
Imports System.Threading
Imports System.Threading.Tasks

Module MosaicTests
    Private Const Commit As String = "0123456789abcdef0123456789abcdef01234567"
    Private Const LatestName As String = "RemoteDesktopMosaic_20261006_AllScreens.exe"
    Private _checks As Integer

    Public Sub RunTests()
        BundledExtraction()
        LatestDownload().GetAwaiter().GetResult()
        FailedDownloads().GetAwaiter().GetResult()
        LockedDestination().GetAwaiter().GetResult()
        CancellationPreservesInstalledFiles().GetAwaiter().GetResult()
        StalledBodyDeadline().GetAwaiter().GetResult()
        Console.WriteLine($"PASS {_checks} Mosaic checks: embedded portable binary, pinned latest selection, checksums, GitHub hosts, bounds, atomic backup, cancellation and preserved prior installs; no live downloads or launches.")
    End Sub

    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New Exception("Mosaic: " & message)
        _checks += 1
    End Sub

    Private Function Exe(Optional marker As Byte = 7) As Byte()
        Dim bytes(127) As Byte
        Array.Fill(bytes, marker)
        bytes(0) = &H4D
        bytes(1) = &H5A
        Return bytes
    End Function

    Private Function Hash(bytes As Byte()) As String
        Return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
    End Function

    Private Sub BundledExtraction()
        Dim unopenedFolder = Path.Combine(Path.GetTempPath(), "KathanaMosaicTests-" & Guid.NewGuid().ToString("N"))
        Check(RemoteDesktopMosaicService.InstalledPath(unopenedFolder) = Path.Combine(unopenedFolder, "Extras", "RemoteDesktopMosaic.exe") AndAlso Not Directory.Exists(unopenedFolder), "showing the portable path must not install or download anything")
        Using fixture As New PortableFixture()
            Dim result = RemoteDesktopMosaicService.ExtractBundled(fixture.BaseFolder)
            Check(result = fixture.Target, "bundled install must stay beside the portable app")
            Using source = File.OpenRead(result)
                Check(source.Length = 56800882, "the build must contain the supplied complete standalone Mosaic")
                Check(source.ReadByte() = &H4D AndAlso source.ReadByte() = &H5A, "bundled output must be an EXE")
                source.Position = 0
                Check(Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant() = RemoteDesktopMosaicService.BundledSha256, "the bundled output must match its verified checksum")
            End Using
            Check(File.ReadAllBytes(fixture.Target & ".previous").SequenceEqual(fixture.Original), "bundled replacement must keep the previous installed binary")
            fixture.CheckNoTemporaryFiles()
        End Using
    End Sub

    Private Async Function LatestDownload() As Task
        Using fixture As New PortableFixture(), handler As New FakeGitHub(), http As New HttpClient(handler)
            Dim result = Await RemoteDesktopMosaicService.DownloadLatestAsync(fixture.BaseFolder, CancellationToken.None, http)
            Check(result = fixture.Target, "latest install must keep a stable portable path")
            Check(File.ReadAllBytes(result).SequenceEqual(handler.Executable), "latest matching dated Mosaic must be installed")
            Check(File.ReadAllBytes(result & ".previous").SequenceEqual(fixture.Original), "previous executable must remain recoverable")
            Check(handler.Requests.Count = 4, "latest install should use one HEAD, one pinned listing, checksum and EXE request")
            Check(handler.Requests(1) = "https://api.github.com/repos/ArmandoA88/KATHANABOT/contents?ref=" & Commit, "file listing must use the exact pinned commit")
            Check(handler.Requests(2) = "https://raw.githubusercontent.com/ArmandoA88/KATHANABOT/" & Commit & "/" & LatestName & ".sha256", "checksum must come from the same commit")
            Check(handler.Requests(3) = "https://raw.githubusercontent.com/ArmandoA88/KATHANABOT/" & Commit & "/" & LatestName, "EXE must use the canonical pinned raw URL")
            Check(handler.Requests.All(Function(address) New Uri(address).Scheme = "https" AndAlso {"api.github.com", "raw.githubusercontent.com"}.Contains(New Uri(address).Host)), "do not follow unrelated executable or untrusted download_url metadata")
            Check(Not handler.SawAuthorization, "the public updater must not use stored secrets")
            fixture.CheckNoTemporaryFiles()

            Dim firstInstall = File.ReadAllBytes(fixture.Target)
            handler.Executable = Exe(22)
            Await RemoteDesktopMosaicService.DownloadLatestAsync(fixture.BaseFolder, CancellationToken.None, http)
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(handler.Executable), "a later explicit update must replace the stable target")
            Check(File.ReadAllBytes(fixture.Target & ".previous").SequenceEqual(firstInstall), "backup must become the immediately previous installed version")
            fixture.CheckNoTemporaryFiles()
        End Using
        Using fixture As New PortableFixture(), handler As New FakeGitHub With {.UseSameDateTie = True}, http As New HttpClient(handler)
            Await RemoteDesktopMosaicService.DownloadLatestAsync(fixture.BaseFolder, CancellationToken.None, http)
            Check(handler.Requests.Last().EndsWith("/RemoteDesktopMosaic_20261006_Zulu.exe", StringComparison.Ordinal), "same-date candidates must have a deterministic name tie-break")
            fixture.CheckNoTemporaryFiles()
        End Using
    End Function

    Private Async Function FailedDownloads() As Task
        Dim cases As Action(Of FakeGitHub)() = {
            Sub(handler) handler.BadChecksum = True,
            Sub(handler) handler.InvalidSignature = True,
            Sub(handler) handler.WrongContentLength = True,
            Sub(handler) handler.ShortStream = True,
            Sub(handler) handler.LongStream = True,
            Sub(handler) handler.UnsafeMetadataPath = True,
            Sub(handler) handler.MissingLatestChecksum = True,
            Sub(handler) handler.NetworkFailure = True,
            Sub(handler) handler.StatusFailure = True,
            Sub(handler) handler.BadCommit = True,
            Sub(handler) handler.OversizedMetadata = True,
            Sub(handler) handler.OversizedChecksum = True,
            Sub(handler) handler.RedirectedResponse = True,
            Sub(handler) handler.OversizedExecutableMetadata = True,
            Sub(handler) handler.WrongChecksumName = True
        }
        For Each configure In cases
            Using fixture As New PortableFixture(), handler As New FakeGitHub(), http As New HttpClient(handler)
                configure(handler)
                Dim failed As Boolean
                Try
                    Await RemoteDesktopMosaicService.DownloadLatestAsync(fixture.BaseFolder, CancellationToken.None, http)
                Catch ex As Exception When TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is HttpRequestException
                    failed = True
                End Try
                Check(failed, "invalid or failed downloads must not install a partial binary")
                fixture.CheckOriginalPreserved()
                fixture.CheckNoTemporaryFiles()
            End Using
        Next
    End Function

    Private Async Function CancellationPreservesInstalledFiles() As Task
        Using fixture As New PortableFixture(), cancellation As New CancellationTokenSource(), handler As New FakeGitHub(), http As New HttpClient(handler)
            cancellation.Cancel()
            Await ExpectCancelled(Function() RemoteDesktopMosaicService.DownloadLatestAsync(fixture.BaseFolder, cancellation.Token, http))
            Check(handler.Requests.Count = 0, "pre-cancelled install must not contact GitHub")
            fixture.CheckOriginalPreserved()
            fixture.CheckNoTemporaryFiles()
            Try
                RemoteDesktopMosaicService.ExtractBundled(fixture.BaseFolder, cancellation.Token)
                Throw New Exception("Cancelled bundled extraction unexpectedly succeeded")
            Catch ex As OperationCanceledException
                Check(True, "cancelled bundled extraction must stop")
            End Try
            fixture.CheckOriginalPreserved()
        End Using
        Using fixture As New PortableFixture(), cancellation As New CancellationTokenSource(), handler As New FakeGitHub With {.CancelDuringTransfer = cancellation}, http As New HttpClient(handler)
            Await ExpectCancelled(Function() RemoteDesktopMosaicService.DownloadLatestAsync(fixture.BaseFolder, cancellation.Token, http))
            Check(handler.Requests.Count = 4, "transfer cancellation fixture must exercise an in-progress EXE copy")
            fixture.CheckOriginalPreserved()
            fixture.CheckNoTemporaryFiles()
            handler.CancelDuringTransfer = Nothing
            Await RemoteDesktopMosaicService.DownloadLatestAsync(fixture.BaseFolder, CancellationToken.None, http)
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(handler.Executable), "a cancelled transfer must release the install gate")
        End Using
    End Function

    Private Async Function LockedDestination() As Task
        Using fixture As New PortableFixture(), handler As New FakeGitHub(), http As New HttpClient(handler)
            Dim failed As Boolean
            Using locked As New FileStream(fixture.Target, FileMode.Open, FileAccess.Read, FileShare.None)
                Try
                    Await RemoteDesktopMosaicService.DownloadLatestAsync(fixture.BaseFolder, CancellationToken.None, http)
                Catch ex As IOException
                    failed = True
                End Try
            End Using
            Check(failed, "a locked executable must not be replaced")
            fixture.CheckOriginalPreserved()
            fixture.CheckNoTemporaryFiles()
        End Using
    End Function

    Private Async Function StalledBodyDeadline() As Task
        Using fixture As New PortableFixture(), handler As New FakeGitHub With {.StallDuringTransfer = True}, http As New HttpClient(handler)
            Dim timedOut As Boolean
            Try
                Await RemoteDesktopMosaicService.DownloadLatestForTestingAsync(fixture.BaseFolder, CancellationToken.None, http, TimeSpan.FromMilliseconds(500))
            Catch ex As TimeoutException
                timedOut = True
            End Try
            Check(timedOut AndAlso handler.BodyReadStarted, "the overall deadline must cancel a stalled body after response headers arrive")
            Check(handler.Requests.Count = 4, "the stalled fixture must reach its pinned EXE body")
            fixture.CheckOriginalPreserved()
            fixture.CheckNoTemporaryFiles()
            handler.StallDuringTransfer = False
            Await RemoteDesktopMosaicService.DownloadLatestAsync(fixture.BaseFolder, CancellationToken.None, http)
            Check(File.ReadAllBytes(fixture.Target).SequenceEqual(handler.Executable), "timeout must release the install gate for the next explicit download")
        End Using
    End Function

    Private Async Function ExpectCancelled(action As Func(Of Task(Of String))) As Task
        Try
            Await action()
        Catch ex As OperationCanceledException
            Check(True, "cancellation propagated")
            Return
        End Try
        Throw New Exception("Expected cancelled Mosaic transfer")
    End Function

    Private NotInheritable Class PortableFixture
        Implements IDisposable
        Friend ReadOnly Property BaseFolder As String = Path.Combine(Path.GetTempPath(), "KathanaMosaicTests-" & Guid.NewGuid().ToString("N"))
        Friend ReadOnly Property Original As Byte() = Exe(1)
        Friend ReadOnly Property Previous As Byte() = Exe(2)
        Friend ReadOnly Property Target As String
            Get
                Return RemoteDesktopMosaicService.InstalledPath(BaseFolder)
            End Get
        End Property

        Friend Sub New()
            Directory.CreateDirectory(Path.GetDirectoryName(Target))
            File.WriteAllBytes(Target, Original)
            File.WriteAllBytes(Target & ".previous", Previous)
        End Sub

        Friend Sub CheckOriginalPreserved()
            Check(File.ReadAllBytes(Target).SequenceEqual(Original), "failed install changed the existing executable")
            Check(File.ReadAllBytes(Target & ".previous").SequenceEqual(Previous), "failed install changed the previous backup")
        End Sub

        Friend Sub CheckNoTemporaryFiles()
            Check(Not Directory.EnumerateFiles(Path.GetDirectoryName(Target), ".RemoteDesktopMosaic.*.tmp").Any(), "owned partial download was not removed")
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Dim resolved = Path.GetFullPath(BaseFolder)
            Dim allowedPrefix = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "KathanaMosaicTests-")
            If Not resolved.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) OrElse Path.GetFileName(resolved) <> Path.GetFileName(BaseFolder) Then Throw New InvalidOperationException("Unexpected fixture cleanup path")
            If Directory.Exists(resolved) Then Directory.Delete(resolved, True)
        End Sub
    End Class

    Private NotInheritable Class FakeGitHub
        Inherits HttpMessageHandler
        Friend Property Executable As Byte() = Exe()
        Friend Property BadChecksum As Boolean
        Friend Property InvalidSignature As Boolean
        Friend Property WrongContentLength As Boolean
        Friend Property ShortStream As Boolean
        Friend Property LongStream As Boolean
        Friend Property UnsafeMetadataPath As Boolean
        Friend Property MissingLatestChecksum As Boolean
        Friend Property NetworkFailure As Boolean
        Friend Property StatusFailure As Boolean
        Friend Property BadCommit As Boolean
        Friend Property OversizedMetadata As Boolean
        Friend Property OversizedChecksum As Boolean
        Friend Property RedirectedResponse As Boolean
        Friend Property OversizedExecutableMetadata As Boolean
        Friend Property WrongChecksumName As Boolean
        Friend Property UseSameDateTie As Boolean
        Friend Property CancelDuringTransfer As CancellationTokenSource
        Friend Property StallDuringTransfer As Boolean
        Friend Property BodyReadStarted As Boolean
        Friend ReadOnly Requests As New List(Of String)()
        Friend Property SawAuthorization As Boolean

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            cancellationToken.ThrowIfCancellationRequested()
            Dim address = request.RequestUri.AbsoluteUri
            Requests.Add(address)
            SawAuthorization = SawAuthorization OrElse request.Headers.Authorization IsNot Nothing
            Dim response As HttpResponseMessage
            If address = "https://api.github.com/repos/ArmandoA88/KATHANABOT/commits/agent-ai" Then
                response = Reply(New JsonObject From {{"sha", If(BadCommit, "../bad-source", Commit)}}.ToJsonString())
                If RedirectedResponse Then response.RequestMessage = New HttpRequestMessage(HttpMethod.Get, "https://untrusted.example/download.exe")
            ElseIf address = "https://api.github.com/repos/ArmandoA88/KATHANABOT/contents?ref=" & Commit Then
                response = Reply(If(OversizedMetadata, New String("x"c, 2 * 1024 * 1024 + 1), Listing()))
            ElseIf address = RawName() & ".sha256" Then
                Dim checksum = If(BadChecksum, New String("0"c, 64), Hash(DownloadBytes()))
                response = Reply(If(OversizedChecksum, New String("x"c, 4097), checksum & "  " & If(WrongChecksumName, "KathanaBot.exe", SelectedName())))
            ElseIf address = RawName() Then
                If StatusFailure Then Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.Forbidden) With {.Content = New StringContent("fixture forbidden")})
                Dim bytes = DownloadBytes()
                If ShortStream Then bytes = bytes.Take(bytes.Length - 1).ToArray()
                If LongStream Then bytes = bytes.Concat(New Byte() {3}).ToArray()
                Dim stream As New ChunkStream(bytes, NetworkFailure, CancelDuringTransfer, StallDuringTransfer, Sub() BodyReadStarted = True)
                response = New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StreamContent(stream)}
                If WrongContentLength Then response.Content.Headers.ContentLength = Executable.Length + 1L
            Else
                Throw New Exception("Unexpected network URI: " & address)
            End If
            Return Task.FromResult(response)
        End Function

        Private Function DownloadBytes() As Byte()
            Dim bytes = CType(Executable.Clone(), Byte())
            If InvalidSignature Then bytes(0) = 0
            Return bytes
        End Function

        Private Function SelectedName() As String
            Return If(UseSameDateTie, "RemoteDesktopMosaic_20261006_Zulu.exe", LatestName)
        End Function

        Private Function RawName() As String
            Return "https://raw.githubusercontent.com/ArmandoA88/KATHANABOT/" & Commit & "/" & SelectedName()
        End Function

        Private Function Listing() As String
            Dim entries As New JsonArray()
            entries.Add(FileEntry("KathanaBotControlPanel_Standalone_99.0.exe", "KathanaBotControlPanel_Standalone_99.0.exe", 999))
            entries.Add(FileEntry("RemoteDesktopMosaic_20271344_Invalid.exe", "RemoteDesktopMosaic_20271344_Invalid.exe", 2))
            entries.Add(FileEntry("RemoteDesktopMosaic_20260929_AllScreens.exe", "RemoteDesktopMosaic_20260929_AllScreens.exe", 128))
            entries.Add(FileEntry("RemoteDesktopMosaic_20260929_AllScreens.exe.sha256", "RemoteDesktopMosaic_20260929_AllScreens.exe.sha256", 110))
            entries.Add(FileEntry("RemoteDesktopMosaic_20261006_Backup.exe.bak", "RemoteDesktopMosaic_20261006_Backup.exe.bak", 999))
            entries.Add(FileEntry(LatestName, If(UnsafeMetadataPath, "../" & LatestName, LatestName), If(OversizedExecutableMetadata, RemoteDesktopMosaicService.MaximumDownloadBytes + 1, CLng(Executable.Length))))
            If Not MissingLatestChecksum Then entries.Add(FileEntry(LatestName & ".sha256", LatestName & ".sha256", 110))
            If UseSameDateTie Then
                entries.Add(FileEntry(SelectedName(), SelectedName(), Executable.Length))
                entries.Add(FileEntry(SelectedName() & ".sha256", SelectedName() & ".sha256", 110))
            End If
            Return entries.ToJsonString()
        End Function

        Private Shared Function FileEntry(name As String, path As String, size As Long) As JsonObject
            Return New JsonObject From {{"type", "file"}, {"name", name}, {"path", path}, {"size", size}, {"download_url", "https://untrusted.example/installer.exe"}}
        End Function

        Private Shared Function Reply(body As String) As HttpResponseMessage
            Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(body, Encoding.UTF8, "application/json")}
        End Function
    End Class

    Private NotInheritable Class ChunkStream
        Inherits Stream
        Private ReadOnly _bytes As Byte()
        Private ReadOnly _fail As Boolean
        Private ReadOnly _cancel As CancellationTokenSource
        Private ReadOnly _stall As Boolean
        Private ReadOnly _onRead As Action
        Private _position As Integer
        Private _reads As Integer

        Friend Sub New(bytes As Byte(), fail As Boolean, cancellation As CancellationTokenSource, Optional stall As Boolean = False, Optional onRead As Action = Nothing)
            _bytes = bytes
            _fail = fail
            _cancel = cancellation
            _stall = stall
            _onRead = onRead
        End Sub

        Public Overrides ReadOnly Property CanRead As Boolean = True
        Public Overrides ReadOnly Property CanSeek As Boolean = False
        Public Overrides ReadOnly Property CanWrite As Boolean = False
        Public Overrides ReadOnly Property Length As Long
            Get
                Throw New NotSupportedException()
            End Get
        End Property
        Public Overrides Property Position As Long
            Get
                Return _position
            End Get
            Set(value As Long)
                Throw New NotSupportedException()
            End Set
        End Property

        Public Overrides Function ReadAsync(buffer As Memory(Of Byte), Optional cancellationToken As CancellationToken = Nothing) As ValueTask(Of Integer)
            _onRead?.Invoke()
            If _stall Then Return New ValueTask(Of Integer)(WaitForDeadlineAsync(cancellationToken))
            _reads += 1
            If _reads = 2 Then
                If _fail Then Throw New HttpRequestException("fixture transfer interrupted")
                If _cancel IsNot Nothing Then _cancel.Cancel()
            End If
            cancellationToken.ThrowIfCancellationRequested()
            Dim count = Math.Min(Math.Min(buffer.Length, 32), _bytes.Length - _position)
            _bytes.AsMemory(_position, count).CopyTo(buffer)
            _position += count
            Return New ValueTask(Of Integer)(count)
        End Function

        Private Shared Async Function WaitForDeadlineAsync(cancellationToken As CancellationToken) As Task(Of Integer)
            Await Task.Delay(Timeout.Infinite, cancellationToken)
            Return 0
        End Function

        Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
            Return ReadAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult()
        End Function
        Public Overrides Sub Flush()
        End Sub
        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Throw New NotSupportedException()
        End Function
        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException()
        End Sub
        Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
            Throw New NotSupportedException()
        End Sub
    End Class
End Module
