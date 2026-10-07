Imports System.Drawing
Imports System.IO
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Windows.Forms

Friend Module HomeDashboardTests
    Private ReadOnly InstanceFlags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
    Private ReadOnly StaticFlags As BindingFlags = BindingFlags.Static Or BindingFlags.NonPublic
    Private checks As Integer

    Public Sub Run()
        checks = 0
        Using fixture As New OwnedHomeFixture()
            TestInitialRunningBeforeOcr(fixture)
            TestStaleLiteTelemetry(fixture)
            TestTaskbarUsesFreshState(fixture)
            TestHiddenHomeAndStop(fixture)
            TestUnknownHealthAlertState()
            SaveOwnedHomePreview(fixture)
        End Using
        Console.WriteLine($"PASS: {checks} Home startup assertions: stopped theme restoration, complete initial cards/play/green chrome before OCR, stale Lite rejection, fresh taskbar state, hidden Home refresh and real Stop (owned UI only; no engine Start, game input, API or COM calls).")
    End Sub

    Private Sub Check(condition As Boolean, reason As String)
        checks += 1
        If Not condition Then Throw New InvalidOperationException("Home startup test: " & reason)
    End Sub

    Private NotInheritable Class OwnedHomeHost
        Inherits Form

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

    Private NotInheritable Class OwnedHomeFixture
        Implements IDisposable

        Public ReadOnly Owner As Object = RuntimeHelpers.GetUninitializedObject(GetType(Form1))
        Public ReadOnly Full As New BotEngine()
        Public ReadOnly Lite As New BotEngine()
        Public ReadOnly Host As New OwnedHomeHost With {.ClientSize = New Size(1370, 930), .ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual, .Location = New Point(-30000, -30000), .Text = "Owned Home preview"}
        Public ReadOnly Frame As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(5), .BackColor = Color.Red}
        Public ReadOnly Banner As New Panel With {.Dock = DockStyle.Top, .Height = 8, .BackColor = Color.Red}
        Public ReadOnly Tabs As TabControl
        Public ReadOnly Home As TabPage
        Public ReadOnly Other As New TabPage("Updates")
        Public ReadOnly EntranceTimer As New System.Windows.Forms.Timer()
        Public ReadOnly HomeBaseColor As Color
        Public ReadOnly StatusCardBaseColor As Color
        Public ReadOnly StatusLabelBaseColor As Color

        Public Sub New()
            ' Borrow initialized base-Control storage, including the existing owned
            ' Controls collection, from a vanilla host. Form1's constructor, profile
            ' loading and automation startup never run; the native window belongs to Host.
            Dim ownedChildren = Host.Controls
            For Each controlField As FieldInfo In GetType(Control).GetFields(InstanceFlags Or BindingFlags.DeclaredOnly)
                controlField.SetValue(Owner, controlField.GetValue(Host))
            Next
            SetField(Owner, "_fullEngine", Full)
            SetField(Owner, "_liteEngine", Lite)
            SetField(Owner, "_fullStatus", New BotStatus())
            SetField(Owner, "_liteStatus", New BotStatus())
            SetField(Owner, "_dashboardEntranceTimer", EntranceTimer)
            SetField(Owner, "_dashboardExpRateHistory", New List(Of Double)())
            SetField(Owner, "_dashboardRupiahRateHistory", New List(Of Double)())
            SetField(Owner, "_dashboardRecentEvents", New Queue(Of String)())
            For Each name In {"_itemAwardReads", "_mainTabVisualState", "_baseBackColors", "_gridThemeSnapshots"}
                Dim field = GetType(Form1).GetField(name, InstanceFlags)
                field.SetValue(Owner, Activator.CreateInstance(field.FieldType))
            Next
            SetField(Owner, "_applicationSessionStartedAtUtc", DateTime.UtcNow.AddSeconds(-12))
            SetField(Owner, "_dashboardRunBaseRupiahs", -1L)
            SetField(Owner, "_dashboardRunBaseExpBasisPoints", -1)
            SetField(Owner, "_dashboardRunLastExpBasisPoints", -1)
            SetField(Owner, "_dashboardTargetStartHpPercent", -1.0R)
            SetField(Owner, "_dashboardTargetSignature", "")
            SetField(Owner, "_dashboardLastRecordedAction", "")
            SetField(Owner, "_activeProfileName", "")
            SetField(Owner, "_taskbarUnavailable", True)
            SetField(Owner, "_themeSnapshotCaptured", False)
            SetField(Owner, "_surfaceRefreshQueued", True)
            SetField(Owner, "pnlWindowFrame", Frame)
            SetField(Owner, "pnlHealthBanner", Banner)
            SetField(Owner, "_edition", BotEdition.Lite)

            Home = DirectCast(Invoke(Owner, "BuildDashboardTab"), TabPage)
            SetField(Owner, "_dashboardTab", Home)
            SetField(Owner, "_updateTab", Other)
            Dim tabsType = GetType(Form1).GetField("_mainTabs", InstanceFlags).FieldType
            Tabs = DirectCast(Activator.CreateInstance(tabsType, nonPublic:=True), TabControl)
            Tabs.Dock = DockStyle.Fill
            Tabs.TabPages.AddRange({Home, Other})
            Tabs.SelectedTab = Home
            SetField(Owner, "_mainTabs", Tabs)
            Frame.Controls.Add(Tabs)
            Frame.Controls.Add(Banner)
            Host.Controls.Add(Frame)
            Host.PerformLayout()
            Frame.PerformLayout()
            Tabs.PerformLayout()
            Home.PerformLayout()
            Check(DirectCast(Owner, Control).Controls.Contains(Frame), "theme traversal must reach the actual owned Home tree")
            Host.Show()
            HomeBaseColor = Home.BackColor
            Dim statusCard = Field(Of Control)(Owner, "cardDashStatus")
            StatusCardBaseColor = statusCard.BackColor
            StatusLabelBaseColor = StatusLabel(Owner).BackColor
            Invoke(Owner, "CaptureThemeSnapshot", DirectCast(Owner, Control))
            SetField(Owner, "_themeSnapshotCaptured", True)
            SetField(Owner, "_lastSurfaceAlert", -1)
            Invoke(Owner, "RefreshSurfaceAlerts", False, True)
            Check(Home.BackColor.ToArgb() <> HomeBaseColor.ToArgb() AndAlso statusCard.BackColor.ToArgb() <> StatusCardBaseColor.ToArgb(),
                  "actual stopped surface reconciliation must tint the owned Home page and card")
            Check(StatusLabel(Owner).BackColor.ToArgb() <> StatusLabelBaseColor.ToArgb(), "actual stopped surface reconciliation must tint owned card labels")
        End Sub

        Public Function FullState() As BotStatus
            Return DirectCast(GetType(BotEngine).GetField("_status", InstanceFlags).GetValue(Full), BotStatus)
        End Function

        Public Function LiteState() As BotStatus
            Return DirectCast(GetType(BotEngine).GetField("_status", InstanceFlags).GetValue(Lite), BotStatus)
        End Function

        Public Function IsPlaying() As Boolean
            Dim button = Field(Of Control)(Owner, "btnDashPlayPause")
            Return CBool(button.GetType().GetProperty("IsPlaying").GetValue(button))
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            ' No task was started. Clear fake Running flags before releasing owned UI.
            FullState().Running = False
            LiteState().Running = False
            EntranceTimer.Dispose()
            Host.Dispose()
        End Sub
    End Class

    Private Sub TestInitialRunningBeforeOcr(fixture As OwnedHomeFixture)
        Dim state = fixture.FullState()
        state.Running = True
        state.WindowFound = False
        state.HpPercent = -1
        state.MpPercent = -1
        state.ExpPercent = -1
        state.ExpPerHour = -1
        state.RupiahsTotal = -1
        state.RupiahsPerHour = -1
        state.SessionKilledMobs = 0
        SetField(fixture.Owner, "_fullStatus", New BotStatus With {.Running = False, .HpPercent = 0})
        fixture.Frame.BackColor = Color.Red
        fixture.Banner.BackColor = Color.Red
        Invoke(fixture.Owner, "RefreshDashboardFromEngine")

        Dim displayed = Field(Of BotStatus)(fixture.Owner, "_fullStatus")
        Check(displayed.Running AndAlso Not displayed.WindowFound AndAlso displayed.HpPercent = -1, "first Home refresh must fetch Running before a window or HP reading is available")
        Check(fixture.IsPlaying(), "first Home refresh must show the Stop icon without a tab change")
        Check(CardValue(fixture.Owner, "cardDashStatus").StartsWith("Active", StringComparison.Ordinal), "complete Home status card must replace initial Ready immediately")
        Check(CardValue(fixture.Owner, "cardDashTarget") = "Searching", "target card must reflect running search before target OCR")
        Check(CardValue(fixture.Owner, "cardDashRate") = "Confirming EXP", "unknown EXP must remain a waiting reading while the bot is running")
        Check(CardValue(fixture.Owner, "cardDashSession") = "Reading wallet", "unknown wallet must remain a waiting reading while the bot is running")
        Check(fixture.Frame.BackColor.ToArgb() = StaticColor("BotRunningColor").ToArgb() AndAlso fixture.Banner.BackColor.ToArgb() = StaticColor("BotRunningColor").ToArgb(),
              "initial running frame/banner must turn green even before HP/window OCR")
        Check(fixture.Home.BackColor.ToArgb() = fixture.HomeBaseColor.ToArgb(), "first running refresh must restore the stopped Home page tint without a tab change")
        Check(Field(Of Control)(fixture.Owner, "cardDashStatus").BackColor.ToArgb() = fixture.StatusCardBaseColor.ToArgb(), "first running refresh must restore the stopped card tint")
        Check(StatusLabel(fixture.Owner).BackColor.ToArgb() = fixture.StatusLabelBaseColor.ToArgb(), "first running refresh must restore the stopped card label tint")
        Dim summary = Field(Of Control)(fixture.Owner, "dashboardSummary")
        Dim metrics = DirectCast(summary.GetType().GetField("_valueLabels", InstanceFlags).GetValue(summary), List(Of Label))
        Check(metrics.Count = 6 AndAlso metrics.All(Function(value) value.Text.Any(AddressOf Char.IsDigit)), "first running Home refresh must populate all six summary metrics, including valid zero counts")
        Check(Field(Of Label)(fixture.Owner, "lblDashSubtitle").Text.Contains("Full mode", StringComparison.Ordinal), "retired Lite UI selection must not choose the initial Home telemetry edition")
    End Sub

    Private Sub TestStaleLiteTelemetry(fixture As OwnedHomeFixture)
        Invoke(fixture.Owner, "OnEngineStatusUpdated", BotEdition.Lite, New BotStatus With {.Running = False, .HpPercent = 0})
        Check(fixture.IsPlaying() AndAlso CardValue(fixture.Owner, "cardDashStatus").StartsWith("Active", StringComparison.Ordinal), "queued stopped Lite telemetry must not turn running Full Home into Ready/play")
        Check(fixture.Frame.BackColor.ToArgb() = StaticColor("BotRunningColor").ToArgb(), "stale Lite telemetry must not recolor running Full chrome red")
        SetField(fixture.Owner, "_fullStatus", New BotStatus With {.Running = False, .HpPercent = 0})
        Invoke(fixture.Owner, "UpdateAttackButtonAppearance", False)
        Check(fixture.IsPlaying() AndAlso CardValue(fixture.Owner, "cardDashStatus").StartsWith("Active", StringComparison.Ordinal), "Start/Stop surface refresh must use real engine state rather than cached stopped Full telemetry")
    End Sub

    Private Sub TestTaskbarUsesFreshState(fixture As OwnedHomeFixture)
        SetField(fixture.Owner, "_fullStatus", New BotStatus With {.Running = False, .HpPercent = 0})
        fixture.Frame.BackColor = Color.Red
        Invoke(fixture.Owner, "UpdateTaskbarStatusIndicator")
        Check(fixture.Frame.BackColor.ToArgb() = StaticColor("BotRunningColor").ToArgb(), "taskbar refresh must keep a live running Full border green despite stale cache")
        Check(Field(Of Object)(fixture.Owner, "_taskbarList") Is Nothing AndAlso Field(Of Boolean)(fixture.Owner, "_taskbarUnavailable"), "owned Home test must bypass all taskbar COM creation")
    End Sub

    Private Sub TestHiddenHomeAndStop(fixture As OwnedHomeFixture)
        fixture.Tabs.SelectedTab = fixture.Other
        Dim state = fixture.FullState()
        state.WindowFound = True
        state.HpPercent = 84
        state.MpPercent = 62
        state.CharacterName = "OwnedCharacter"
        state.SessionKilledMobs = 7
        state.ExpPercent = 35.5
        state.RupiahsTotal = 1000
        Invoke(fixture.Owner, "RefreshDashboardFromEngine")
        Check(CardValue(fixture.Owner, "cardDashStatus").Contains("7 killed", StringComparison.Ordinal), "Home cards must keep receiving fresh state while another page is selected")
        fixture.Tabs.SelectedTab = fixture.Home
        Check(CardValue(fixture.Owner, "cardDashStatus").Contains("7 killed", StringComparison.Ordinal) AndAlso fixture.IsPlaying(), "returning to Home must reveal already current cards without requiring another engine event")
        state.Running = False
        SetField(fixture.Owner, "_fullStatus", New BotStatus With {.Running = True, .HpPercent = 84})
        Invoke(fixture.Owner, "RefreshDashboardFromEngine")
        Check(Not fixture.IsPlaying() AndAlso CardValue(fixture.Owner, "cardDashStatus").StartsWith("Ready", StringComparison.Ordinal), "real engine stop must immediately show Ready/play despite stale running cache")
        Check(fixture.Frame.BackColor.ToArgb() = StaticColor("StatusStoppedOrDeadColor").ToArgb() AndAlso fixture.Banner.BackColor.ToArgb() = StaticColor("StatusStoppedOrDeadColor").ToArgb(),
              "real stop must immediately turn frame/banner red")
    End Sub

    Private Sub TestUnknownHealthAlertState()
        Dim method = GetType(Form1).GetMethod("GetSurfaceAlertState", StaticFlags)
        For Each hp In {-1.0R, Double.NaN, Double.PositiveInfinity}
            Dim status As New BotStatus With {.Running = True, .WindowFound = True, .HpPercent = hp}
            Check(CInt(method.Invoke(Nothing, {status, True, True, True})) = 0, "unknown HP must not mark a running Home as dead")
        Next
        Dim initial As New BotStatus With {.Running = True, .WindowFound = False, .HpPercent = 0}
        Check(CInt(method.Invoke(Nothing, {initial, True, True, True})) = 0, "default zero before window/HP OCR must not trigger death tint")
        Dim confirmed As New BotStatus With {.Running = True, .WindowFound = True, .HpPercent = 0}
        Check(CInt(method.Invoke(Nothing, {confirmed, True, True, True})) = 2, "confirmed zero HP on visible Home must retain its death alert")
        Check(CInt(method.Invoke(Nothing, {confirmed, True, False, True})) = 0, "hidden Home must not apply its death pulse to another page")
        Check(CInt(method.Invoke(Nothing, {initial, False, True, True})) = 1, "a stopped engine must retain paused tint independently of unknown readings")
    End Sub

    Private Sub SaveOwnedHomePreview(fixture As OwnedHomeFixture)
        fixture.FullState().Running = True
        fixture.Tabs.SelectedTab = fixture.Home
        Invoke(fixture.Owner, "RefreshDashboardFromEngine")
        fixture.Host.PerformLayout()
        fixture.Frame.PerformLayout()
        fixture.Tabs.PerformLayout()
        fixture.Home.PerformLayout()
        Dim imagePath = Path.Combine(AppContext.BaseDirectory, "home-startup-owned.png")
        Using bitmap As New Bitmap(fixture.Frame.ClientSize.Width, fixture.Frame.ClientSize.Height)
            fixture.Frame.DrawToBitmap(bitmap, New Rectangle(Point.Empty, bitmap.Size))
            Check(bitmap.GetPixel(1, 1).ToArgb() = StaticColor("BotRunningColor").ToArgb(), "owned preview must actually render the green running frame")
            Dim darkSamples As Integer
            For y = 20 To bitmap.Height - 1 Step 40
                For x = 20 To bitmap.Width - 1 Step 40
                    Dim pixel = bitmap.GetPixel(x, y)
                    If pixel.R < 80 AndAlso pixel.G < 80 AndAlso pixel.B < 100 Then darkSamples += 1
                Next
            Next
            Check(darkSamples > 20, "owned preview must render dark Home content rather than a blank host")
            bitmap.Save(imagePath, Imaging.ImageFormat.Png)
        End Using
        Check(File.Exists(imagePath) AndAlso New FileInfo(imagePath).Length > 1000, "owned Home layout preview must be available for visual review")
    End Sub

    Private Function CardValue(owner As Object, fieldName As String) As String
        Dim card = Field(Of Control)(owner, fieldName)
        Return DirectCast(card.GetType().GetField("_lblValue", InstanceFlags).GetValue(card), Label).Text
    End Function

    Private Function StatusLabel(owner As Object) As Label
        Dim card = Field(Of Control)(owner, "cardDashStatus")
        Return DirectCast(card.GetType().GetField("_lblValue", InstanceFlags).GetValue(card), Label)
    End Function

    Private Function StaticColor(name As String) As Color
        Return DirectCast(GetType(Form1).GetField(name, StaticFlags).GetValue(Nothing), Color)
    End Function

    Private Sub SetField(owner As Object, name As String, value As Object)
        GetType(Form1).GetField(name, InstanceFlags).SetValue(owner, value)
    End Sub

    Private Function Field(Of T)(owner As Object, name As String) As T
        Return DirectCast(GetType(Form1).GetField(name, InstanceFlags).GetValue(owner), T)
    End Function

    Private Function Invoke(owner As Object, name As String, ParamArray arguments As Object()) As Object
        Try
            Return GetType(Form1).GetMethod(name, InstanceFlags).Invoke(owner, arguments)
        Catch ex As TargetInvocationException When ex.InnerException IsNot Nothing
            Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw()
            Throw
        End Try
    End Function
End Module
