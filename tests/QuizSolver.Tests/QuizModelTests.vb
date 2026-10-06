Imports System.Drawing
Imports System.Reflection
Imports System.Text.Json
Imports System.Threading
Imports System.Windows.Forms

Module QuizModelTests
    Private ReadOnly Flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic

    Public Sub Run()
        Dim failure As Exception = Nothing
        Dim worker As New Thread(Sub()
                                     Try
                                         SelectorAndPersistence()
                                     Catch ex As Exception
                                         failure = ex
                                     End Try
                                 End Sub)
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start()
        worker.Join()
        If failure IsNot Nothing Then Throw failure
        Console.WriteLine("PASS Quiz model labels, default Luna migration, exact API ID profile round trips and visible cost/speed legend (owned offline UI only).")
    End Sub

    Private Sub Check(value As Boolean, message As String)
        If Not value Then Throw New Exception("Quiz models: " & message)
    End Sub

    Private Sub SetField(owner As Object, name As String, value As Object)
        GetType(Form1).GetField(name, Flags).SetValue(owner, value)
    End Sub

    Private Function Invoke(owner As Object, name As String, ParamArray arguments As Object()) As Object
        Return GetType(Form1).GetMethod(name, Flags).Invoke(owner, arguments)
    End Function

    Private Sub SelectorAndPersistence()
        ' This fixture never constructs the app, loads a profile or reads a real API key.
        Dim owner = Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        SetField(owner, "_quizSettingsLoading", True)
        Dim answerListType = GetType(Form1).GetField("_quizAnswerDatabase", Flags).FieldType
        SetField(owner, "_quizAnswerDatabase", Activator.CreateInstance(answerListType))
        SetField(owner, "_quizNoticeCancellation", New CancellationTokenSource())
        Using timer As New System.Windows.Forms.Timer()
            SetField(owner, "_quizScanTimer", timer)
            Using tab = DirectCast(Invoke(owner, "BuildQuizTab"), TabPage)
                Dim selector = DirectCast(GetType(Form1).GetField("cboQuizModel", Flags).GetValue(owner), ComboBox)
                Dim stateType = GetType(Form1).GetNestedType("PersistedQuizState", BindingFlags.NonPublic)
                Dim modelProperty = stateType.GetProperty("Model")
                Check(selector.Items.Count = 2, "the selector must expose only the two requested models")
                Check(selector.Items(0).ToString() = "GPT-6 Luna — Fast / Cheap", "Luna needs a clear speed/cost label")
                Check(selector.Items(1).ToString() = "GPT-6 Astra — Ultrafast / Expensive", "Astra needs a clear speed/cost label")
                Check(CStr(Invoke(owner, "GetSelectedQuizModel")) = "gpt-6-luna", "default selection must be Luna")
                Check(CStr(modelProperty.GetValue(JsonSerializer.Deserialize("{}", stateType))) = "gpt-6-luna", "old profiles without a model need the Luna default")

                For Each savedModel In {"", "gpt-5.4-mini", "gpt-5-mini", "unavailable-model", "gpt-6-luna", "gpt-6-astra"}
                    Dim fixtureState = Activator.CreateInstance(stateType)
                    modelProperty.SetValue(fixtureState, savedModel)
                    Invoke(owner, "ApplyPersistedQuizState", fixtureState)
                    SetField(owner, "_quizSettingsLoading", True)
                    Dim expectedId = If(savedModel = "gpt-6-astra", "gpt-6-astra", "gpt-6-luna")
                    Check(CStr(Invoke(owner, "GetSelectedQuizModel")) = expectedId, "saved selection did not migrate or restore: " & savedModel)
                    Dim built = Invoke(owner, "BuildPersistedQuizState")
                    Check(CStr(modelProperty.GetValue(built)) = expectedId, "the profile must save the API ID, not its display label")
                    Dim fixtureJson = JsonSerializer.Serialize(built, stateType)
                    Check(Not fixtureJson.Contains("Fast / Cheap") AndAlso Not fixtureJson.Contains("Ultrafast / Expensive"), "display labels must never enter persisted API model IDs")
                    Invoke(owner, "ApplyPersistedQuizState", JsonSerializer.Deserialize(fixtureJson, stateType))
                    SetField(owner, "_quizSettingsLoading", True)
                    Check(CStr(Invoke(owner, "GetSelectedQuizModel")) = expectedId, "model choice did not round trip")
                Next

                selector.SelectedIndex = 0
                Check(CStr(Invoke(owner, "GetSelectedQuizModel")) = "gpt-6-luna", "manual Luna selection must submit its API ID")
                selector.SelectedIndex = 1
                Check(CStr(Invoke(owner, "GetSelectedQuizModel")) = "gpt-6-astra", "manual Astra selection must submit its API ID")

                Using host As New Form With {.ClientSize = New Size(1100, 880), .ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual, .Location = New Point(-30000, -30000)}, tabs As New TabControl With {.Dock = DockStyle.Fill}
                    tabs.TabPages.Add(tab)
                    host.Controls.Add(tabs)
                    host.Show()
                    Application.DoEvents()
                    Dim legend = tab.Controls.Find("QuizModelLegend", True).OfType(Of Label)().Single()
                    Check(legend.Text.Contains("Fast / Cheap") AndAlso legend.Text.Contains("Ultrafast / Expensive"), "the legend must describe both selected service tiers")
                    Check(selector.Width >= TextRenderer.MeasureText(selector.SelectedItem.ToString(), selector.Font).Width + 28, "the longer Astra label is clipped")
                    Dim legendPosition = host.PointToClient(legend.PointToScreen(Point.Empty))
                    Check(legendPosition.Y >= 0 AndAlso legendPosition.Y + legend.Height <= host.ClientSize.Height, "the model legend must remain visible near the selector")
                    Check(legend.Width >= TextRenderer.MeasureText(legend.Text, legend.Font).Width, "the model legend is clipped")
                    Using bitmap As New Bitmap(host.ClientSize.Width, host.ClientSize.Height)
                        host.DrawToBitmap(bitmap, New Rectangle(Point.Empty, host.ClientSize))
                        bitmap.Save(IO.Path.Combine(AppContext.BaseDirectory, "quiz-model-controls.png"))
                    End Using
                    timer.Stop()
                End Using
            End Using
            DirectCast(GetType(Form1).GetField("_quizNoticeCancellation", Flags).GetValue(owner), CancellationTokenSource).Dispose()
        End Using
    End Sub
End Module
