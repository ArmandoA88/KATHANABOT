Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks

Friend NotInheritable Class RemoteDesktopMosaicService
    Friend Const BundledFileName As String = "RemoteDesktopMosaic_20261007_AllScreens.exe"
    Friend Const BundledSha256 As String = "6574a359a54afb7609827137f632ede8fa4ea6872e37b859d3eb777e291cd9d1"
    Friend Const MaximumDownloadBytes As Long = 100L * 1024L * 1024L
    Private Const MaximumJsonBytes As Integer = 2 * 1024 * 1024
    Private Const MaximumChecksumBytes As Integer = 4096
    Private Const RepositoryPath As String = "ArmandoA88/KATHANABOT"
    Private Const ExeResource As String = "KathanaBotControlPanel.Extras.RemoteDesktopMosaic.exe"
    Private Const ChecksumResource As String = ExeResource & ".sha256"
    Private Shared ReadOnly InstallGate As New SemaphoreSlim(1, 1)
    Private Shared ReadOnly Client As New HttpClient(New HttpClientHandler With {.AllowAutoRedirect = False}) With {.Timeout = TimeSpan.FromMinutes(10)}
    Private Shared ReadOnly DatedExe As New Regex("^RemoteDesktopMosaic_(?<date>\d{8})(?:_[A-Za-z0-9][A-Za-z0-9_-]*)?\.exe$", RegexOptions.CultureInvariant)

    Private Sub New()
    End Sub

    Friend Shared Function InstalledPath(baseFolder As String) As String
        If String.IsNullOrWhiteSpace(baseFolder) Then Throw New ArgumentException("The portable app folder is required.", NameOf(baseFolder))
        Return Path.Combine(Path.GetFullPath(baseFolder), "Extras", "RemoteDesktopMosaic.exe")
    End Function

    Friend Shared Function ExtractBundled(baseFolder As String, Optional cancellationToken As CancellationToken = Nothing) As String
        InstallGate.Wait(cancellationToken)
        Dim temporaryPath As String = Nothing
        Try
            Dim target = PrepareDestination(baseFolder)
            Dim assembly = GetType(RemoteDesktopMosaicService).Assembly
            Using checksumStream = assembly.GetManifestResourceStream(ChecksumResource), source = assembly.GetManifestResourceStream(ExeResource)
                If checksumStream Is Nothing OrElse source Is Nothing Then Throw New InvalidOperationException("The bundled Remote Desktop Mosaic is missing from this build.")
                Dim expectedHash = ParseChecksum(Encoding.UTF8.GetString(ReadBounded(checksumStream, MaximumChecksumBytes, cancellationToken)), BundledFileName)
                If expectedHash <> BundledSha256 Then Throw New InvalidOperationException("The bundled Mosaic checksum does not match this app build.")
                If Not source.CanSeek OrElse source.Length < 2 OrElse source.Length > MaximumDownloadBytes Then Throw New InvalidOperationException("The bundled Mosaic executable has an invalid size.")
                temporaryPath = NewTemporaryPath(target)
                Using destination As New FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)
                    CopyAndVerify(source, destination, source.Length, expectedHash, cancellationToken)
                    destination.Flush(True)
                End Using
                cancellationToken.ThrowIfCancellationRequested()
                InstallVerifiedFile(temporaryPath, target)
                temporaryPath = Nothing
                Return target
            End Using
        Finally
            DeleteOwnedTemporary(temporaryPath)
            InstallGate.Release()
        End Try
    End Function

    Friend Shared Function DownloadLatestAsync(baseFolder As String, cancellationToken As CancellationToken, Optional transport As HttpClient = Nothing) As Task(Of String)
        Return DownloadLatestCoreAsync(baseFolder, cancellationToken, If(transport, Client), TimeSpan.FromMinutes(10))
    End Function

    ' A short deadline is available only to owned offline transports; the app always uses ten minutes.
    Friend Shared Function DownloadLatestForTestingAsync(baseFolder As String, cancellationToken As CancellationToken, transport As HttpClient, operationTimeout As TimeSpan) As Task(Of String)
        If transport Is Nothing OrElse operationTimeout <= TimeSpan.Zero OrElse operationTimeout > TimeSpan.FromSeconds(5) Then Throw New ArgumentException("A supplied test transport and a deadline of at most five seconds are required.")
        Return DownloadLatestCoreAsync(baseFolder, cancellationToken, transport, operationTimeout)
    End Function

    Private Shared Async Function DownloadLatestCoreAsync(baseFolder As String, cancellationToken As CancellationToken, http As HttpClient, operationTimeout As TimeSpan) As Task(Of String)
        Using deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            deadline.CancelAfter(operationTimeout)
            Dim operationToken = deadline.Token
            Dim gateAcquired As Boolean
            Dim temporaryPath As String = Nothing
            Try
                Await InstallGate.WaitAsync(operationToken).ConfigureAwait(False)
                gateAcquired = True
                Dim target = PrepareDestination(baseFolder)
                Dim headBytes = Await GetBoundedAsync(http, New Uri("https://api.github.com/repos/" & RepositoryPath & "/commits/agent-ai"), MaximumJsonBytes, operationToken).ConfigureAwait(False)
                Dim commit As String
                Using document = JsonDocument.Parse(headBytes, New JsonDocumentOptions With {.MaxDepth = 32})
                    commit = JsonString(document.RootElement, "sha")
                End Using
                If Not Regex.IsMatch(commit, "\A[0-9a-fA-F]{40}\z", RegexOptions.CultureInvariant) Then Throw New InvalidOperationException("GitHub did not return a valid Mosaic source revision.")
                commit = commit.ToLowerInvariant()
                Dim listing = Await GetBoundedAsync(http, New Uri("https://api.github.com/repos/" & RepositoryPath & "/contents?ref=" & commit), MaximumJsonBytes, operationToken).ConfigureAwait(False)
                Dim latest = SelectLatest(listing)
                Dim checksumBytes = Await GetBoundedAsync(http, PinnedRawUri(commit, latest.Name & ".sha256"), MaximumChecksumBytes, operationToken).ConfigureAwait(False)
                Dim expectedHash = ParseChecksum(Encoding.UTF8.GetString(checksumBytes), latest.Name)
                temporaryPath = NewTemporaryPath(target)
                Using response = Await GetResponseAsync(http, PinnedRawUri(commit, latest.Name), operationToken).ConfigureAwait(False)
                    If response.Content.Headers.ContentLength.HasValue AndAlso response.Content.Headers.ContentLength.Value <> latest.Size Then Throw New InvalidOperationException("The downloaded Mosaic size does not match its pinned repository metadata.")
                    Using source = Await response.Content.ReadAsStreamAsync(operationToken).ConfigureAwait(False), destination As New FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous)
                        Await CopyAndVerifyAsync(source, destination, latest.Size, expectedHash, operationToken).ConfigureAwait(False)
                        Await destination.FlushAsync(operationToken).ConfigureAwait(False)
                        destination.Flush(True)
                    End Using
                End Using
                operationToken.ThrowIfCancellationRequested()
                InstallVerifiedFile(temporaryPath, target)
                temporaryPath = Nothing
                Return target
            Catch ex As OperationCanceledException When Not cancellationToken.IsCancellationRequested
                Throw New TimeoutException("The Mosaic download exceeded its time limit; the installed copy and backup were kept.", ex)
            Finally
                DeleteOwnedTemporary(temporaryPath)
                If gateAcquired Then InstallGate.Release()
            End Try
        End Using
    End Function

    Private NotInheritable Class MosaicFile
        Friend Property Name As String = ""
        Friend Property Size As Long
        Friend Property DateStamp As DateTime
    End Class

    Private Shared Function SelectLatest(listing As Byte()) As MosaicFile
        Using document = JsonDocument.Parse(listing, New JsonDocumentOptions With {.MaxDepth = 32})
            If document.RootElement.ValueKind <> JsonValueKind.Array Then Throw New InvalidOperationException("GitHub did not return a Mosaic file listing.")
            If document.RootElement.GetArrayLength() >= 1000 Then Throw New InvalidOperationException("The repository listing is too large to reliably select the latest Mosaic.")
            Dim candidates As New List(Of MosaicFile)()
            Dim checksums As New HashSet(Of String)(StringComparer.Ordinal)
            For Each entry In document.RootElement.EnumerateArray()
                If entry.ValueKind <> JsonValueKind.Object OrElse JsonString(entry, "type") <> "file" Then Continue For
                Dim name = JsonString(entry, "name")
                Dim match = DatedExe.Match(name)
                If match.Success Then
                    Dim dateStamp As DateTime
                    If Not DateTime.TryParseExact(match.Groups("date").Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, dateStamp) Then Continue For
                    ValidateRootFile(entry, name)
                    Dim size = JsonSize(entry)
                    If size < 2 OrElse size > MaximumDownloadBytes Then Throw New InvalidOperationException("The latest Mosaic executable exceeds the permitted download size.")
                    candidates.Add(New MosaicFile With {.Name = name, .DateStamp = dateStamp, .Size = size})
                ElseIf name.EndsWith(".exe.sha256", StringComparison.Ordinal) AndAlso DatedExe.IsMatch(name.Substring(0, name.Length - 7)) Then
                    ValidateRootFile(entry, name)
                    Dim size = JsonSize(entry)
                    If size <= 0 OrElse size > MaximumChecksumBytes Then Throw New InvalidOperationException("A Mosaic checksum file has an invalid size.")
                    checksums.Add(name)
                End If
            Next
            Dim latest = candidates.OrderByDescending(Function(candidate) candidate.DateStamp).ThenByDescending(Function(candidate) candidate.Name, StringComparer.Ordinal).FirstOrDefault()
            If latest Is Nothing Then Throw New InvalidOperationException("No dated Remote Desktop Mosaic executable was found in the repository.")
            If Not checksums.Contains(latest.Name & ".sha256") Then Throw New InvalidOperationException("The latest Mosaic has no matching checksum at the same repository revision; the installed copy was kept.")
            Return latest
        End Using
    End Function

    Private Shared Sub ValidateRootFile(entry As JsonElement, name As String)
        If JsonString(entry, "path") <> name OrElse Path.GetFileName(name) <> name Then Throw New InvalidOperationException("GitHub returned an unsafe Mosaic file path.")
    End Sub

    Private Shared Function JsonString(element As JsonElement, name As String) As String
        Dim value As JsonElement
        If element.ValueKind = JsonValueKind.Object AndAlso element.TryGetProperty(name, value) AndAlso value.ValueKind = JsonValueKind.String Then Return If(value.GetString(), "")
        Return ""
    End Function

    Private Shared Function JsonSize(element As JsonElement) As Long
        Dim value As JsonElement
        Dim size As Long
        If element.TryGetProperty("size", value) AndAlso value.ValueKind = JsonValueKind.Number AndAlso value.TryGetInt64(size) Then Return size
        Throw New InvalidOperationException("GitHub returned invalid Mosaic file size metadata.")
    End Function

    Private Shared Function PinnedRawUri(commit As String, name As String) As Uri
        Return New Uri("https://raw.githubusercontent.com/" & RepositoryPath & "/" & commit & "/" & Uri.EscapeDataString(name))
    End Function

    Private Shared Async Function GetResponseAsync(http As HttpClient, address As Uri, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
        ValidateHost(address)
        Using request As New HttpRequestMessage(HttpMethod.Get, address)
            request.Headers.TryAddWithoutValidation("User-Agent", "KathanaBot-Extras")
            request.Headers.TryAddWithoutValidation("Accept", If(address.Host = "api.github.com", "application/vnd.github+json", "application/octet-stream"))
            If address.Host = "api.github.com" Then request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28")
            Dim response = Await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(False)
            Try
                If response.RequestMessage?.RequestUri IsNot Nothing Then
                    ValidateHost(response.RequestMessage.RequestUri)
                    If response.RequestMessage.RequestUri.AbsoluteUri <> address.AbsoluteUri Then Throw New InvalidOperationException("The Mosaic download unexpectedly changed its pinned GitHub address.")
                End If
                If Not response.IsSuccessStatusCode Then Throw New InvalidOperationException($"GitHub could not provide Remote Desktop Mosaic (HTTP {CInt(response.StatusCode)}); the installed copy was kept.")
                Return response
            Catch
                response.Dispose()
                Throw
            End Try
        End Using
    End Function

    Private Shared Sub ValidateHost(address As Uri)
        If address Is Nothing OrElse address.Scheme <> Uri.UriSchemeHttps OrElse Not address.IsDefaultPort OrElse address.UserInfo.Length > 0 OrElse
            (address.Host <> "api.github.com" AndAlso address.Host <> "raw.githubusercontent.com") Then Throw New InvalidOperationException("Mosaic downloads must use the pinned HTTPS GitHub source.")
    End Sub

    Private Shared Async Function GetBoundedAsync(http As HttpClient, address As Uri, limit As Integer, cancellationToken As CancellationToken) As Task(Of Byte())
        Using response = Await GetResponseAsync(http, address, cancellationToken).ConfigureAwait(False)
            If response.Content.Headers.ContentLength.HasValue AndAlso response.Content.Headers.ContentLength.Value > limit Then Throw New InvalidOperationException("GitHub returned oversized Mosaic metadata.")
            Using source = Await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(False), destination As New MemoryStream()
                Dim buffer(8191) As Byte
                Do
                    Dim count = Await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(False)
                    If count = 0 Then Exit Do
                    If destination.Length + count > limit Then Throw New InvalidOperationException("GitHub returned oversized Mosaic metadata.")
                    Await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(False)
                Loop
                Return destination.ToArray()
            End Using
        End Using
    End Function

    Private Shared Function ReadBounded(source As Stream, limit As Integer, cancellationToken As CancellationToken) As Byte()
        Using destination As New MemoryStream()
            Dim buffer(8191) As Byte
            Do
                cancellationToken.ThrowIfCancellationRequested()
                Dim count = source.Read(buffer, 0, buffer.Length)
                If count = 0 Then Exit Do
                If destination.Length + count > limit Then Throw New InvalidOperationException("The bundled Mosaic checksum is oversized.")
                destination.Write(buffer, 0, count)
            Loop
            Return destination.ToArray()
        End Using
    End Function

    Private Shared Function ParseChecksum(text As String, expectedName As String) As String
        Dim match = Regex.Match(text.Trim(), "\A(?<hash>[0-9a-fA-F]{64})(?:\s+\*?(?<name>[A-Za-z0-9_.-]+))?\z", RegexOptions.CultureInvariant)
        If Not match.Success OrElse (match.Groups("name").Success AndAlso match.Groups("name").Value <> expectedName) Then Throw New InvalidOperationException("The Mosaic checksum is invalid or names a different executable.")
        Return match.Groups("hash").Value.ToLowerInvariant()
    End Function

    Private Shared Sub CopyAndVerify(source As Stream, destination As Stream, expectedSize As Long, expectedHash As String, cancellationToken As CancellationToken)
        Using hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
            Dim buffer(65535) As Byte
            Dim total As Long
            Dim signature(1) As Byte
            Do
                cancellationToken.ThrowIfCancellationRequested()
                Dim count = source.Read(buffer, 0, buffer.Length)
                If count = 0 Then Exit Do
                VerifyChunk(buffer, count, total, signature, expectedSize)
                hash.AppendData(buffer, 0, count)
                destination.Write(buffer, 0, count)
                total += count
            Loop
            VerifyFinished(total, signature, expectedSize, expectedHash, hash)
        End Using
    End Sub

    Private Shared Async Function CopyAndVerifyAsync(source As Stream, destination As Stream, expectedSize As Long, expectedHash As String, cancellationToken As CancellationToken) As Task
        Using hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
            Dim buffer(65535) As Byte
            Dim total As Long
            Dim signature(1) As Byte
            Do
                Dim count = Await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(False)
                If count = 0 Then Exit Do
                VerifyChunk(buffer, count, total, signature, expectedSize)
                hash.AppendData(buffer, 0, count)
                Await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(False)
                total += count
            Loop
            VerifyFinished(total, signature, expectedSize, expectedHash, hash)
        End Using
    End Function

    Private Shared Sub VerifyChunk(buffer As Byte(), count As Integer, total As Long, signature As Byte(), expectedSize As Long)
        If total + count > expectedSize OrElse total + count > MaximumDownloadBytes Then Throw New InvalidOperationException("The Mosaic download exceeded its expected size.")
        For offset As Integer = 0 To Math.Min(count - 1, 1 - CInt(Math.Min(total, 2L)))
            signature(CInt(total) + offset) = buffer(offset)
        Next
    End Sub

    Private Shared Sub VerifyFinished(total As Long, signature As Byte(), expectedSize As Long, expectedHash As String, hash As IncrementalHash)
        If total <> expectedSize Then Throw New InvalidOperationException("The Mosaic download was incomplete.")
        If signature(0) <> &H4D OrElse signature(1) <> &H5A Then Throw New InvalidOperationException("The Mosaic download is not a Windows executable.")
        If Not String.Equals(Convert.ToHexString(hash.GetHashAndReset()), expectedHash, StringComparison.OrdinalIgnoreCase) Then Throw New InvalidOperationException("The Mosaic SHA-256 checksum did not match; the installed copy was kept.")
    End Sub

    Private Shared Function PrepareDestination(baseFolder As String) As String
        Dim target = InstalledPath(baseFolder)
        Dim folder = Path.GetDirectoryName(target)
        If Directory.Exists(folder) AndAlso (File.GetAttributes(folder) And FileAttributes.ReparsePoint) <> 0 Then Throw New InvalidOperationException("The portable Extras folder cannot be a redirected directory.")
        Directory.CreateDirectory(folder)
        For Each existing In {target, target & ".previous"}
            If File.Exists(existing) AndAlso (File.GetAttributes(existing) And FileAttributes.ReparsePoint) <> 0 Then Throw New InvalidOperationException("The installed Mosaic or backup cannot be a redirected file.")
        Next
        Return target
    End Function

    Private Shared Function NewTemporaryPath(target As String) As String
        Return Path.Combine(Path.GetDirectoryName(target), ".RemoteDesktopMosaic." & Guid.NewGuid().ToString("N") & ".tmp")
    End Function

    Private Shared Sub InstallVerifiedFile(temporaryPath As String, target As String)
        If File.Exists(target) Then
            File.Replace(temporaryPath, target, target & ".previous", True)
        Else
            File.Move(temporaryPath, target)
        End If
    End Sub

    Private Shared Sub DeleteOwnedTemporary(temporaryPath As String)
        If temporaryPath Is Nothing Then Return
        Try
            File.Delete(temporaryPath)
        Catch
            ' Cleanup failure must not conceal the original error or replace the installed copy.
        End Try
    End Sub
End Class
