Imports System.Diagnostics
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports Microsoft.VisualBasic
Imports Microsoft.Win32

Friend NotInheritable Class KeygenPersistedState
    Public Property AccountSlug As String = ""
    Public Property EncryptedLicenseKey As String = ""
    Public Property MachineId As String = ""
End Class

Friend NotInheritable Class KeygenLicenseSession
    Public Property AccountSlug As String = ""
    Public Property LicenseKey As String = ""
    Public Property LicenseId As String = ""
    Public Property MachineId As String = ""
    Public Property ProcessId As String = ""
    Public Property Fingerprint As String = ""
    Public Property HeartbeatIntervalSeconds As Integer = 60
End Class

Friend NotInheritable Class KeygenApiFailureException
    Inherits Exception

    Public ReadOnly Property Code As String

    Public Sub New(code As String, message As String)
        MyBase.New(message)
        Me.Code = If(code, "").Trim()
    End Sub
End Class

Friend NotInheritable Class KeygenLicenseManager
    Private Shared ReadOnly ApiClient As New HttpClient() With {
        .BaseAddress = New Uri("https://api.keygen.sh/v1/"),
        .Timeout = TimeSpan.FromSeconds(15)
    }

    Private Const JsonApiMediaType As String = "application/vnd.api+json"

    Private NotInheritable Class KeygenValidationResult
        Public Property IsValid As Boolean
        Public Property Code As String = ""
        Public Property Detail As String = ""
        Public Property LicenseId As String = ""
    End Class

    Private NotInheritable Class KeygenMachineInfo
        Public Property Id As String = ""
        Public Property Fingerprint As String = ""
    End Class

    Private NotInheritable Class KeygenProcessInfo
        Public Property Id As String = ""
        Public Property IntervalSeconds As Integer = 60
    End Class

    Private Sub New()
    End Sub

    Public Shared Function ProtectLicenseKey(licenseKey As String) As String
        If String.IsNullOrWhiteSpace(licenseKey) Then
            Return ""
        End If

        Dim plainBytes As Byte() = Encoding.UTF8.GetBytes(licenseKey.Trim())
        Dim protectedBytes As Byte() = ProtectedData.Protect(plainBytes, Nothing, DataProtectionScope.CurrentUser)
        Return Convert.ToBase64String(protectedBytes)
    End Function

    Public Shared Function UnprotectLicenseKey(encryptedLicenseKey As String) As String
        If String.IsNullOrWhiteSpace(encryptedLicenseKey) Then
            Return ""
        End If

        Try
            Dim cipherBytes As Byte() = Convert.FromBase64String(encryptedLicenseKey.Trim())
            Dim plainBytes As Byte() = ProtectedData.Unprotect(cipherBytes, Nothing, DataProtectionScope.CurrentUser)
            Return Encoding.UTF8.GetString(plainBytes).Trim()
        Catch
            Return ""
        End Try
    End Function

    Public Shared Function ComputeMachineFingerprint() As String
        Dim machineGuid As String = ReadMachineGuid()
        If String.IsNullOrWhiteSpace(machineGuid) Then
            machineGuid = Environment.MachineName
        End If

        Using sha As SHA256 = SHA256.Create()
            Dim bytes As Byte() = Encoding.UTF8.GetBytes(machineGuid.Trim().ToUpperInvariant())
            Dim hash As Byte() = sha.ComputeHash(bytes)
            Return Convert.ToHexString(hash).ToLowerInvariant()
        End Using
    End Function

    Public Shared Async Function EstablishSessionAsync(accountSlug As String, licenseKey As String, fingerprint As String) As Task(Of KeygenLicenseSession)
        Dim account As String = NormalizeAccount(accountSlug)
        Dim key As String = NormalizeLicenseKey(licenseKey)
        Dim validation As KeygenValidationResult = Await ValidateKeyAsync(account, key, fingerprint).ConfigureAwait(False)

        If String.IsNullOrWhiteSpace(validation.LicenseId) Then
            Throw New KeygenApiFailureException(validation.Code, "Keygen did not return a license ID for this key.")
        End If

        Dim needsActivation As Boolean =
            String.Equals(validation.Code, "NO_MACHINE", StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(validation.Code, "NO_MACHINES", StringComparison.OrdinalIgnoreCase)

        If Not validation.IsValid AndAlso Not needsActivation Then
            Throw New KeygenApiFailureException(validation.Code, validation.Detail)
        End If

        Dim machine As KeygenMachineInfo = Await FindMachineAsync(account, key, fingerprint).ConfigureAwait(False)
        If machine Is Nothing Then
            machine = Await ActivateMachineAsync(account, key, validation.LicenseId, fingerprint).ConfigureAwait(False)
        End If

        If needsActivation Then
            validation = Await ValidateKeyAsync(account, key, fingerprint).ConfigureAwait(False)
            If Not validation.IsValid Then
                Throw New KeygenApiFailureException(validation.Code, validation.Detail)
            End If
        End If

        Dim processInfo As KeygenProcessInfo = Await SpawnProcessAsync(account, key, machine.Id).ConfigureAwait(False)

        Return New KeygenLicenseSession With {
            .AccountSlug = account,
            .LicenseKey = key,
            .LicenseId = validation.LicenseId,
            .MachineId = machine.Id,
            .ProcessId = processInfo.Id,
            .Fingerprint = fingerprint,
            .HeartbeatIntervalSeconds = Math.Max(30, processInfo.IntervalSeconds)
        }
    End Function

    Public Shared Async Function RevalidateAsync(session As KeygenLicenseSession) As Task
        If session Is Nothing Then
            Throw New KeygenApiFailureException("NO_SESSION", "No active Keygen session.")
        End If

        Dim validation As KeygenValidationResult = Await ValidateKeyAsync(session.AccountSlug, session.LicenseKey, session.Fingerprint).ConfigureAwait(False)
        If Not validation.IsValid Then
            Throw New KeygenApiFailureException(validation.Code, validation.Detail)
        End If
    End Function

    Public Shared Async Function PingProcessAsync(session As KeygenLicenseSession) As Task(Of Integer)
        If session Is Nothing OrElse String.IsNullOrWhiteSpace(session.ProcessId) Then
            Throw New KeygenApiFailureException("NO_PROCESS", "No Keygen process is active.")
        End If

        Using request As HttpRequestMessage = CreateLicenseRequest(HttpMethod.Post, $"accounts/{session.AccountSlug}/processes/{session.ProcessId}/actions/ping", session.LicenseKey)
            Using response As HttpResponseMessage = Await ApiClient.SendAsync(request).ConfigureAwait(False)
                If Not response.IsSuccessStatusCode Then
                    Throw Await CreateApiFailureAsync(response).ConfigureAwait(False)
                End If

                Dim body As String = Await response.Content.ReadAsStringAsync().ConfigureAwait(False)
                If String.IsNullOrWhiteSpace(body) Then
                    Return session.HeartbeatIntervalSeconds
                End If

                Using doc As JsonDocument = JsonDocument.Parse(body)
                    Dim intervalSeconds As Integer = GetNestedInt(doc.RootElement, "data", "attributes", "interval")
                    If intervalSeconds <= 0 Then
                        intervalSeconds = session.HeartbeatIntervalSeconds
                    End If
                    Return intervalSeconds
                End Using
            End Using
        End Using
    End Function

    Public Shared Async Function KillProcessAsync(session As KeygenLicenseSession) As Task
        If session Is Nothing OrElse String.IsNullOrWhiteSpace(session.ProcessId) Then
            Return
        End If

        Using request As HttpRequestMessage = CreateLicenseRequest(HttpMethod.Delete, $"accounts/{session.AccountSlug}/processes/{session.ProcessId}", session.LicenseKey)
            Using response As HttpResponseMessage = Await ApiClient.SendAsync(request).ConfigureAwait(False)
                If response.IsSuccessStatusCode OrElse response.StatusCode = Net.HttpStatusCode.NotFound Then
                    Return
                End If

                Throw Await CreateApiFailureAsync(response).ConfigureAwait(False)
            End Using
        End Using
    End Function

    Private Shared Async Function ValidateKeyAsync(accountSlug As String, licenseKey As String, fingerprint As String) As Task(Of KeygenValidationResult)
        Dim payload = New With {
            .meta = New With {
                .key = NormalizeLicenseKey(licenseKey),
                .scope = New With {
                    .fingerprint = fingerprint
                }
            }
        }

        Using request As HttpRequestMessage = CreateJsonRequest(HttpMethod.Post, $"accounts/{NormalizeAccount(accountSlug)}/licenses/actions/validate-key", payload)
            Using response As HttpResponseMessage = Await ApiClient.SendAsync(request).ConfigureAwait(False)
                Dim body As String = Await response.Content.ReadAsStringAsync().ConfigureAwait(False)
                If Not response.IsSuccessStatusCode Then
                    Throw BuildApiFailureFromBody(body, $"Keygen validation failed with HTTP {(CInt(response.StatusCode)).ToString()}.")
                End If

                Using doc As JsonDocument = JsonDocument.Parse(body)
                    Return New KeygenValidationResult With {
                        .IsValid = GetNestedBoolean(doc.RootElement, "meta", "valid"),
                        .Code = GetNestedString(doc.RootElement, "meta", "code"),
                        .Detail = GetNestedString(doc.RootElement, "meta", "detail"),
                        .LicenseId = GetNestedString(doc.RootElement, "data", "id")
                    }
                End Using
            End Using
        End Using
    End Function

    Private Shared Async Function FindMachineAsync(accountSlug As String, licenseKey As String, fingerprint As String) As Task(Of KeygenMachineInfo)
        Using request As HttpRequestMessage = CreateLicenseRequest(HttpMethod.Get, $"accounts/{NormalizeAccount(accountSlug)}/machines?limit=100", licenseKey)
            Using response As HttpResponseMessage = Await ApiClient.SendAsync(request).ConfigureAwait(False)
                If Not response.IsSuccessStatusCode Then
                    Throw Await CreateApiFailureAsync(response).ConfigureAwait(False)
                End If

                Dim body As String = Await response.Content.ReadAsStringAsync().ConfigureAwait(False)
                Using doc As JsonDocument = JsonDocument.Parse(body)
                    Dim dataElement As JsonElement
                    If Not doc.RootElement.TryGetProperty("data", dataElement) OrElse dataElement.ValueKind <> JsonValueKind.Array Then
                        Return Nothing
                    End If

                    For Each entry As JsonElement In dataElement.EnumerateArray()
                        Dim entryFingerprint As String = GetNestedString(entry, "attributes", "fingerprint")
                        If String.Equals(entryFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) Then
                            Return New KeygenMachineInfo With {
                                .Id = GetNestedString(entry, "id"),
                                .Fingerprint = entryFingerprint
                            }
                        End If
                    Next
                End Using
            End Using
        End Using

        Return Nothing
    End Function

    Private Shared Async Function ActivateMachineAsync(accountSlug As String, licenseKey As String, licenseId As String, fingerprint As String) As Task(Of KeygenMachineInfo)
        Dim payload = New With {
            .data = New With {
                .type = "machines",
                .attributes = New With {
                    .fingerprint = fingerprint,
                    .name = Environment.MachineName,
                    .platform = "windows"
                },
                .relationships = New With {
                    .license = New With {
                        .data = New With {
                            .type = "licenses",
                            .id = licenseId
                        }
                    }
                }
            }
        }

        Using request As HttpRequestMessage = CreateLicenseRequest(HttpMethod.Post, $"accounts/{NormalizeAccount(accountSlug)}/machines", licenseKey, payload)
            Using response As HttpResponseMessage = Await ApiClient.SendAsync(request).ConfigureAwait(False)
                If Not response.IsSuccessStatusCode Then
                    Throw Await CreateApiFailureAsync(response).ConfigureAwait(False)
                End If

                Dim body As String = Await response.Content.ReadAsStringAsync().ConfigureAwait(False)
                Using doc As JsonDocument = JsonDocument.Parse(body)
                    Return New KeygenMachineInfo With {
                        .Id = GetNestedString(doc.RootElement, "data", "id"),
                        .Fingerprint = GetNestedString(doc.RootElement, "data", "attributes", "fingerprint")
                    }
                End Using
            End Using
        End Using
    End Function

    Private Shared Async Function SpawnProcessAsync(accountSlug As String, licenseKey As String, machineId As String) As Task(Of KeygenProcessInfo)
        Dim payload = New With {
            .data = New With {
                .type = "processes",
                .attributes = New With {
                    .pid = Process.GetCurrentProcess().Id.ToString(Globalization.CultureInfo.InvariantCulture),
                    .metadata = New With {
                        .exe = Application.ProductName,
                        .machine = Environment.MachineName
                    }
                },
                .relationships = New With {
                    .machine = New With {
                        .data = New With {
                            .type = "machines",
                            .id = machineId
                        }
                    }
                }
            }
        }

        Using request As HttpRequestMessage = CreateLicenseRequest(HttpMethod.Post, $"accounts/{NormalizeAccount(accountSlug)}/processes", licenseKey, payload)
            Using response As HttpResponseMessage = Await ApiClient.SendAsync(request).ConfigureAwait(False)
                If Not response.IsSuccessStatusCode Then
                    Throw Await CreateApiFailureAsync(response).ConfigureAwait(False)
                End If

                Dim body As String = Await response.Content.ReadAsStringAsync().ConfigureAwait(False)
                Using doc As JsonDocument = JsonDocument.Parse(body)
                    Dim intervalSeconds As Integer = GetNestedInt(doc.RootElement, "data", "attributes", "interval")
                    If intervalSeconds <= 0 Then
                        intervalSeconds = 60
                    End If

                    Return New KeygenProcessInfo With {
                        .Id = GetNestedString(doc.RootElement, "data", "id"),
                        .IntervalSeconds = intervalSeconds
                    }
                End Using
            End Using
        End Using
    End Function

    Private Shared Function CreateJsonRequest(method As HttpMethod, relativeUrl As String, payload As Object) As HttpRequestMessage
        Dim request As New HttpRequestMessage(method, relativeUrl)
        request.Headers.Accept.Clear()
        request.Headers.Accept.Add(New MediaTypeWithQualityHeaderValue(JsonApiMediaType))
        request.Content = CreateJsonContent(payload)
        Return request
    End Function

    Private Shared Function CreateLicenseRequest(method As HttpMethod, relativeUrl As String, licenseKey As String, Optional payload As Object = Nothing) As HttpRequestMessage
        Dim request As New HttpRequestMessage(method, relativeUrl)
        request.Headers.Accept.Clear()
        request.Headers.Accept.Add(New MediaTypeWithQualityHeaderValue(JsonApiMediaType))
        request.Headers.Authorization = New AuthenticationHeaderValue("License", NormalizeLicenseKey(licenseKey))
        If payload IsNot Nothing Then
            request.Content = CreateJsonContent(payload)
        End If
        Return request
    End Function

    Private Shared Function CreateJsonContent(payload As Object) As StringContent
        Return New StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, JsonApiMediaType)
    End Function

    Private Shared Async Function CreateApiFailureAsync(response As HttpResponseMessage) As Task(Of KeygenApiFailureException)
        Dim body As String = ""
        Try
            body = Await response.Content.ReadAsStringAsync().ConfigureAwait(False)
        Catch
        End Try
        Return BuildApiFailureFromBody(body, $"Keygen request failed with HTTP {(CInt(response.StatusCode)).ToString()}.")
    End Function

    Private Shared Function BuildApiFailureFromBody(body As String, fallbackMessage As String) As KeygenApiFailureException
        If Not String.IsNullOrWhiteSpace(body) Then
            Try
                Using doc As JsonDocument = JsonDocument.Parse(body)
                    Dim message As String = GetNestedString(doc.RootElement, "meta", "detail")
                    Dim code As String = GetNestedString(doc.RootElement, "meta", "code")

                    Dim errorsElement As JsonElement
                    If doc.RootElement.TryGetProperty("errors", errorsElement) AndAlso errorsElement.ValueKind = JsonValueKind.Array AndAlso errorsElement.GetArrayLength() > 0 Then
                        Dim firstError As JsonElement = errorsElement(0)
                        If String.IsNullOrWhiteSpace(code) Then
                            code = GetNestedString(firstError, "code")
                        End If
                        If String.IsNullOrWhiteSpace(message) Then
                            message = GetNestedString(firstError, "detail")
                        End If
                        If String.IsNullOrWhiteSpace(message) Then
                            message = GetNestedString(firstError, "title")
                        End If
                    End If

                    If String.IsNullOrWhiteSpace(message) Then
                        message = fallbackMessage
                    End If

                    Return New KeygenApiFailureException(code, message)
                End Using
            Catch
            End Try
        End If

        Return New KeygenApiFailureException("", fallbackMessage)
    End Function

    Private Shared Function GetNestedString(element As JsonElement, ParamArray path As String()) As String
        Dim current As JsonElement = element
        For Each segment As String In path
            If current.ValueKind <> JsonValueKind.Object OrElse Not current.TryGetProperty(segment, current) Then
                Return ""
            End If
        Next

        If current.ValueKind = JsonValueKind.String Then
            Return current.GetString()
        End If

        If current.ValueKind = JsonValueKind.Number OrElse current.ValueKind = JsonValueKind.True OrElse current.ValueKind = JsonValueKind.False Then
            Return current.ToString()
        End If

        Return ""
    End Function

    Private Shared Function GetNestedBoolean(element As JsonElement, ParamArray path As String()) As Boolean
        Dim value As String = GetNestedString(element, path)
        Dim parsed As Boolean
        If Boolean.TryParse(value, parsed) Then
            Return parsed
        End If
        Return False
    End Function

    Private Shared Function GetNestedInt(element As JsonElement, ParamArray path As String()) As Integer
        Dim value As String = GetNestedString(element, path)
        Dim parsed As Integer
        If Integer.TryParse(value, parsed) Then
            Return parsed
        End If
        Return 0
    End Function

    Private Shared Function NormalizeAccount(accountSlug As String) As String
        Return If(accountSlug, "").Trim()
    End Function

    Private Shared Function NormalizeLicenseKey(licenseKey As String) As String
        Return If(licenseKey, "").Trim()
    End Function

    Private Shared Function ReadMachineGuid() As String
        Dim views As RegistryView() = {RegistryView.Registry64, RegistryView.Registry32}
        For Each view As RegistryView In views
            Try
                Using baseKey As RegistryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view)
                    Using subKey As RegistryKey = baseKey.OpenSubKey("SOFTWARE\Microsoft\Cryptography", False)
                        Dim value As String = TryCast(subKey?.GetValue("MachineGuid"), String)
                        If Not String.IsNullOrWhiteSpace(value) Then
                            Return value
                        End If
                    End Using
                End Using
            Catch
            End Try
        Next

        Return ""
    End Function
End Class

Friend NotInheritable Class KeygenLicensePromptForm
    Inherits Form

    Private ReadOnly _txtAccount As TextBox
    Private ReadOnly _txtLicense As TextBox

    Public ReadOnly Property AccountSlug As String
        Get
            Return _txtAccount.Text.Trim()
        End Get
    End Property

    Public ReadOnly Property LicenseKey As String
        Get
            Return _txtLicense.Text.Trim()
        End Get
    End Property

    Public Sub New(accountSlug As String, licenseKey As String)
        Text = "Keygen License Setup"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        ClientSize = New Size(470, 210)
        Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(14),
            .ColumnCount = 1,
            .RowCount = 5
        }
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        Dim lblIntro As New Label() With {
            .Dock = DockStyle.Fill,
            .AutoSize = True,
            .Text = "Enter your Keygen account slug and the license key you created for this prototype. The app will activate this PC and keep only one live session at a time."
        }
        root.Controls.Add(lblIntro, 0, 0)

        Dim accountPanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Top,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(0, 12, 0, 0)
        }
        accountPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        accountPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        accountPanel.Controls.Add(New Label() With {.Text = "Account slug", .Dock = DockStyle.Fill, .AutoSize = True}, 0, 0)
        _txtAccount = New TextBox() With {.Dock = DockStyle.Top, .Text = If(accountSlug, "").Trim()}
        accountPanel.Controls.Add(_txtAccount, 0, 1)
        root.Controls.Add(accountPanel, 0, 1)

        Dim licensePanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Top,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(0, 10, 0, 0)
        }
        licensePanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        licensePanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        licensePanel.Controls.Add(New Label() With {.Text = "License key", .Dock = DockStyle.Fill, .AutoSize = True}, 0, 0)
        _txtLicense = New TextBox() With {.Dock = DockStyle.Top, .Text = If(licenseKey, "").Trim()}
        licensePanel.Controls.Add(_txtLicense, 0, 1)
        root.Controls.Add(licensePanel, 0, 2)

        Dim lblHint As New Label() With {
            .Dock = DockStyle.Fill,
            .AutoSize = True,
            .ForeColor = Color.FromArgb(90, 90, 90),
            .Margin = New Padding(0, 10, 0, 0),
            .Text = "Tip: in Keygen, use a timed policy with 72 hours, max machines = 1, max processes = 1, and process leasing per license."
        }
        root.Controls.Add(lblHint, 0, 3)

        Dim buttons As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .AutoSize = True,
            .WrapContents = False,
            .Margin = New Padding(0, 14, 0, 0)
        }

        Dim btnOk As New Button() With {.Text = "Activate", .AutoSize = True}
        Dim btnCancel As New Button() With {.Text = "Cancel", .AutoSize = True}
        AddHandler btnOk.Click,
            Sub()
                If Me.AccountSlug = "" OrElse Me.LicenseKey = "" Then
                    MessageBox.Show(Me, "Enter both the Keygen account slug and the license key.", "License Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If

                DialogResult = DialogResult.OK
                Close()
            End Sub
        AddHandler btnCancel.Click,
            Sub()
                DialogResult = DialogResult.Cancel
                Close()
            End Sub

        buttons.Controls.Add(btnOk)
        buttons.Controls.Add(btnCancel)
        root.Controls.Add(buttons, 0, 4)

        Controls.Add(root)
        AcceptButton = btnOk
        CancelButton = btnCancel
    End Sub
End Class
