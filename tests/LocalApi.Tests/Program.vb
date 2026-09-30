Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
    Private Async Function Run() As Task
        Dim calls = 0
        Dim api As New LocalBotApi(Function(route, command)
                                       calls += 1
                                       If route = "/key" Then Throw New InvalidOperationException("No foreground target")
                                       Return Task.FromResult(Of Object)(New With {.running = False, .processId = command.ProcessId})
                                   End Function)
        Await api.StartAsync()
        Try
            Check(New Uri(api.Address).Host = "127.0.0.1", "loopback binding")
            Using client As New HttpClient With {.BaseAddress = New Uri(api.Address)}
                Check((Await client.GetAsync("/status")).StatusCode = HttpStatusCode.Unauthorized, "missing token")
                client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", "wrong")
                Check((Await client.GetAsync("/status")).StatusCode = HttpStatusCode.Unauthorized, "wrong token")
                Check(calls = 0, "unauthorized commands never dispatched")
                client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", api.Token)
                Check((Await client.GetAsync("/status")).StatusCode = HttpStatusCode.OK, "status")
                Check((Await client.GetAsync("/start")).StatusCode = HttpStatusCode.NotFound, "GET cannot mutate")
                Check((Await client.PostAsync("/start", Nothing)).StatusCode = HttpStatusCode.OK, "start")
                Check((Await client.PostAsync("/stop", Nothing)).StatusCode = HttpStatusCode.OK, "stop")
                Check((Await client.PostAsync("/key", New StringContent("{"))).StatusCode = HttpStatusCode.BadRequest, "bad JSON")
                Check((Await client.PostAsync("/key", New StringContent("null"))).StatusCode = HttpStatusCode.BadRequest, "null JSON")
                Check((Await client.PostAsync("/key", New StringContent("{}"))).StatusCode = HttpStatusCode.Conflict, "command rejection")
                Check((Await client.PostAsync("/click", New StringContent("{""processId"":42}"))).StatusCode = HttpStatusCode.OK, "click")
                Check((Await client.PostAsync("/key", New StringContent(New String("x"c, 3000)))).StatusCode = HttpStatusCode.BadRequest, "body limit")
                client.DefaultRequestHeaders.Add("Origin", "https://example.com")
                Check((Await client.GetAsync("/status")).StatusCode = HttpStatusCode.Unauthorized, "browser origin denied")
                Check(calls = 5, "only valid authorized routes dispatched")
            End Using
        Finally
            api.StopAsync().GetAwaiter().GetResult()
        End Try
        Console.WriteLine("PASS: local API authentication, routes, validation, errors, loopback and shutdown.")
    End Function
    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New Exception("FAIL: " & message)
    End Sub
End Module
