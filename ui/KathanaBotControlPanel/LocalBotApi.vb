Imports System.Net
Imports System.Security.Cryptography
Imports System.Text.Json
Imports System.Threading.Tasks
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Extensions.Logging

' Loopback-only command transport. The host owns all game/UI behavior.
Public NotInheritable Class LocalBotApi
    Public ReadOnly Token As String = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
    Public Property Address As String
    Private app As WebApplication
    Private ReadOnly handler As Func(Of String, ApiCommand, Task(Of Object))
    Public Sub New(dispatch As Func(Of String, ApiCommand, Task(Of Object)))
        handler = dispatch
    End Sub
    Public Async Function StartAsync() As Task
        Dim builder = WebApplication.CreateSlimBuilder(New String() {})
        builder.Logging.ClearProviders()
        builder.WebHost.ConfigureKestrel(Sub(options)
                                           options.Listen(IPAddress.Loopback, 0)
                                           options.Limits.MaxRequestBodySize = 2048
                                           options.Limits.MaxConcurrentConnections = 16
                                       End Sub)
        app = builder.Build()
        app.Run(New RequestDelegate(AddressOf HandleAsync))
        Await app.StartAsync()
        Address = app.Urls.Single()
    End Function
    Private Async Function HandleAsync(context As HttpContext) As Task
        context.Response.ContentType = "application/json"
        context.Response.Headers.CacheControl = "no-store"
        If context.Request.Headers.ContainsKey("Origin") OrElse
            context.Request.Headers.Authorization.ToString() <> "Bearer " & Token Then
            context.Response.StatusCode = 401
            Return
        End If
        Dim route = context.Request.Path.Value
        Dim command As New ApiCommand()
        If route = "/status" AndAlso context.Request.Method = "GET" Then
            ' Read-only request.
        ElseIf {"/start", "/stop", "/key", "/click"}.Contains(route) AndAlso context.Request.Method = "POST" Then
            If route = "/key" OrElse route = "/click" Then
                Try
                    command = Await JsonSerializer.DeserializeAsync(Of ApiCommand)(context.Request.Body,
                        New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}, context.RequestAborted)
                    If command Is Nothing Then Throw New JsonException()
                Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is BadHttpRequestException
                    context.Response.StatusCode = 400
                    Return
                End Try
            End If
        Else
            context.Response.StatusCode = 404
            Return
        End If
        Dim result As Object
        Try
            result = Await handler(route, command)
        Catch ex As InvalidOperationException
            context.Response.StatusCode = 409
            result = New With {.error = ex.Message}
        Catch ex As Exception
            context.Response.StatusCode = 500
            result = New With {.error = "Command failed; inspect the bot log."}
            RuntimeJournal.Record("Local API", ex.Message)
        End Try
        Await context.Response.WriteAsync(JsonSerializer.Serialize(result), context.RequestAborted)
    End Function
    Public Async Function StopAsync() As Task
        If app Is Nothing Then Return
        Await app.StopAsync(New Threading.CancellationTokenSource(TimeSpan.FromSeconds(2)).Token)
        Await app.DisposeAsync()
    End Function
End Class

Public Class ApiCommand
    Public Property ProcessId As Integer
    Public Property Key As Integer
End Class
