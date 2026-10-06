Imports System.IO
Imports System.Net
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Extensions.Logging

' Separate from the game's local command API: this endpoint can only import a
' bounded text snapshot. A random per-session capability is shared explicitly
' with the user's browser extension. It contains no Discord credential.
Public NotInheritable Class DiscordBrowserCaptureServer
    Implements IDisposable

    Public ReadOnly Property CaptureId As String = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
    Private receiverAddress As String = ""
    Public ReadOnly Property ReceiverUrl As String
        Get
            Return receiverAddress
        End Get
    End Property
    Private ReadOnly target As DiscordTradeChannelTarget
    Private ReadOnly messageLimit As Integer
    Private ReadOnly receive As Func(Of DiscordTradeSnapshot, Task(Of Boolean))
    Private ReadOnly lifetime As New CancellationTokenSource()
    Private ReadOnly submission As New SemaphoreSlim(1, 1)
    Private ReadOnly gate As New Object()
    Private app As WebApplication
    Private running As Integer
    Private pausedValue As Integer
    Private accepted As Integer
    Private started As Boolean
    Private expiresAt As DateTimeOffset

    Public Sub New(sourceChannelUrl As String, messageLimit As Integer, receive As Func(Of DiscordTradeSnapshot, Task(Of Boolean)))
        target = DiscordTradeService.ParseChannel(sourceChannelUrl)
        If target.GuildId.Length = 0 Then Throw New ArgumentException("Enter a complete Discord server/channel link.")
        If messageLimit < 1 OrElse messageLimit > DiscordTradeService.MaximumImportMessageCount Then Throw New ArgumentOutOfRangeException(NameOf(messageLimit))
        If receive Is Nothing Then Throw New ArgumentNullException(NameOf(receive))
        Me.messageLimit = messageLimit
        Me.receive = receive
    End Sub

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return Volatile.Read(running) <> 0 AndAlso DateTimeOffset.UtcNow < expiresAt
        End Get
    End Property

    Public Property Paused As Boolean
        Get
            Return Volatile.Read(pausedValue) <> 0
        End Get
        Set(value As Boolean)
            Interlocked.Exchange(pausedValue, If(value, 1, 0))
        End Set
    End Property

    Public ReadOnly Property ConnectionJson As String
        Get
            If String.IsNullOrEmpty(ReceiverUrl) Then Throw New InvalidOperationException("Start browser capture first.")
            Return JsonSerializer.Serialize(New With {.format = "KathanaCaptureConnection", .version = 1, .receiverUrl = ReceiverUrl,
                .sourceUrl = target.ChannelLink, .messageLimit = messageLimit})
        End Get
    End Property

    Public Sub Start()
        SyncLock gate
            If started Then Throw New InvalidOperationException("Create a new browser capture connection to restart.")
            started = True
        End SyncLock
        StartCoreAsync().GetAwaiter().GetResult()
    End Sub

    Private Async Function StartCoreAsync() As Task
        Dim builder = WebApplication.CreateSlimBuilder(New String() {})
        builder.Logging.ClearProviders() ' No routes, capabilities, or body content in request logs.
        builder.WebHost.ConfigureKestrel(Sub(options)
                                            options.Listen(IPAddress.Loopback, 0)
                                            options.Limits.MaxRequestBodySize = DiscordTradeService.MaximumBrowserCaptureBytes
                                            options.Limits.MaxConcurrentConnections = 4
                                            options.Limits.MaxRequestHeadersTotalSize = 8192
                                            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10)
                                            options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(10)
                                        End Sub)
        Dim server = builder.Build()
        server.Run(New RequestDelegate(AddressOf HandleAsync))
        SyncLock gate
            app = server
        End SyncLock
        Try
            Await server.StartAsync(lifetime.Token).ConfigureAwait(False)
            receiverAddress = server.Urls.Single().TrimEnd("/"c) & "/kathana-capture/" & CaptureId & "/"
            ' Slow browser collection can take over 30 minutes for 10,000 posts.
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(90)
            Interlocked.Exchange(running, 1)
        Catch
            Me.Stop()
            Throw New InvalidOperationException("The local browser capture connection could not start.")
        End Try
    End Function

    Private Async Function HandleAsync(context As HttpContext) As Task
        context.Response.ContentType = "application/json"
        context.Response.Headers.CacheControl = "no-store"
        Dim path = context.Request.Path.Value
        Dim basePath = "/kathana-capture/" & CaptureId & "/"
        If context.Request.QueryString.HasValue OrElse Not {basePath & "state", basePath & "capture"}.Contains(path) Then
            Await ReplyAsync(context, 404, New With {.error = "Capture connection unavailable."})
            Return
        End If
        Dim address = New Uri(ReceiverUrl)
        If context.Request.Host.Host <> "127.0.0.1" OrElse context.Request.Host.Port.GetValueOrDefault() <> address.Port OrElse
            context.Connection.RemoteIpAddress Is Nothing OrElse Not IPAddress.IsLoopback(context.Connection.RemoteIpAddress) Then
            Await ReplyAsync(context, 403, New With {.error = "Local capture connections only."})
            Return
        End If
        Dim origin = context.Request.Headers.Origin.ToString()
        If origin.Length > 0 AndAlso Not Regex.IsMatch(origin, "\Achrome-extension://[a-p]{32}\z") Then
            Await ReplyAsync(context, 403, New With {.error = "Browser extension connections only."})
            Return
        End If
        If origin.Length > 0 Then
            context.Response.Headers.AccessControlAllowOrigin = origin
            context.Response.Headers.Vary = "Origin"
        End If
        If context.Request.Method = "OPTIONS" Then
            context.Response.Headers.AccessControlAllowMethods = "GET, POST"
            context.Response.Headers.AccessControlAllowHeaders = "Content-Type"
            context.Response.StatusCode = 204
            Return
        End If
        If Not IsRunning Then
            Await ReplyAsync(context, 410, New With {.active = False, .error = "Browser capture stopped or expired."})
            Return
        End If
        If path = basePath & "state" AndAlso context.Request.Method = "GET" Then
            Await ReplyAsync(context, 200, New With {.active = Volatile.Read(accepted) = 0, .paused = Paused,
                .messageLimit = messageLimit, .sourceChannelId = target.ChannelId})
            Return
        End If
        If path <> basePath & "capture" OrElse context.Request.Method <> "POST" Then
            Await ReplyAsync(context, 405, New With {.error = "Capture method unavailable."})
            Return
        End If
        If Paused OrElse Volatile.Read(accepted) <> 0 OrElse Not Await submission.WaitAsync(0).ConfigureAwait(False) Then
            Await ReplyAsync(context, 409, New With {.error = "Capture import is paused or already completed."})
            Return
        End If
        Dim failureCode As Integer
        Dim failureBody As Object = Nothing
        Try
            If context.Request.ContentLength.HasValue AndAlso context.Request.ContentLength.Value > DiscordTradeService.MaximumBrowserCaptureBytes Then
                Await ReplyAsync(context, 413, New With {.error = "Browser capture is too large."})
                Return
            End If
            If Not If(context.Request.ContentType, "").StartsWith("application/json", StringComparison.OrdinalIgnoreCase) OrElse
                context.Request.Headers.ContainsKey("Content-Encoding") Then
                Await ReplyAsync(context, 415, New With {.error = "Send a JSON capture."})
                Return
            End If
            Using deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, context.RequestAborted)
                deadline.CancelAfter(TimeSpan.FromSeconds(30))
                Dim bytes As Byte()
                Using buffer As New MemoryStream()
                    Dim chunk(8191) As Byte
                    Do
                        Dim count = Await context.Request.Body.ReadAsync(chunk.AsMemory(), deadline.Token).ConfigureAwait(False)
                        If count = 0 Then Exit Do
                        If buffer.Length + count > DiscordTradeService.MaximumBrowserCaptureBytes Then Throw New BadHttpRequestException("Capture body limit.", 413)
                        buffer.Write(chunk, 0, count)
                    Loop
                    bytes = buffer.ToArray()
                End Using
                Dim snapshot = DiscordTradeService.ParseBrowserCaptureJson(target.ChannelLink, New UTF8Encoding(False, True).GetString(bytes), CaptureId, messageLimit)
                If Not IsRunning OrElse Paused OrElse Volatile.Read(accepted) <> 0 Then
                    Await ReplyAsync(context, 409, New With {.error = "Capture import is paused or already completed."})
                    Return
                End If
                Dim imported = Await receive(snapshot).WaitAsync(deadline.Token).ConfigureAwait(False)
                If Not imported OrElse Not IsRunning Then
                    Await ReplyAsync(context, 409, New With {.error = "Capture import is paused or its source changed."})
                    Return
                End If
                Interlocked.Exchange(accepted, 1)
                Await ReplyAsync(context, 200, New With {.imported = True, .messageCount = snapshot.ImportedMessageCount})
            End Using
        Catch ex As DiscordTradeException
            failureCode = 400
            failureBody = New With {.error = "Invalid or empty browser capture; existing posts are kept."}
        Catch ex As DecoderFallbackException
            failureCode = 400
            failureBody = New With {.error = "Invalid browser capture encoding."}
        Catch ex As BadHttpRequestException
            failureCode = If(ex.StatusCode = 413, 413, 400)
            failureBody = New With {.error = "Invalid or oversized browser capture."}
        Catch ex As OperationCanceledException
            failureCode = 408
            failureBody = New With {.error = "Capture stopped or timed out."}
        Catch
            failureCode = 500
            failureBody = New With {.error = "Browser capture could not be imported; existing posts are kept."}
        Finally
            submission.Release()
        End Try
        If failureBody IsNot Nothing AndAlso Not context.RequestAborted.IsCancellationRequested Then Await ReplyAsync(context, failureCode, failureBody)
    End Function

    Private Shared Async Function ReplyAsync(context As HttpContext, code As Integer, body As Object) As Task
        context.Response.StatusCode = code
        Await context.Response.WriteAsync(JsonSerializer.Serialize(body), context.RequestAborted).ConfigureAwait(False)
    End Function

    Public Sub [Stop]()
        Interlocked.Exchange(running, 0)
        lifetime.Cancel()
        Dim server As WebApplication
        SyncLock gate
            server = app
            app = Nothing
        End SyncLock
        If server Is Nothing Then Return
        Task.Run(Async Function()
                     Try
                         Using timeout As New CancellationTokenSource(TimeSpan.FromSeconds(3))
                             Await server.StopAsync(timeout.Token).ConfigureAwait(False)
                         End Using
                     Catch
                         ' Shutdown errors do not expose request data.
                     End Try
                     Try
                         Await server.DisposeAsync().ConfigureAwait(False)
                     Catch
                         ' Closing remains nonblocking and does not log payloads.
                     End Try
                 End Function)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Me.Stop()
    End Sub
End Class
