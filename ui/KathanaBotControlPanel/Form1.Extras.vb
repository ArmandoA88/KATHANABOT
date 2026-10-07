Imports System.Diagnostics
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

Partial Public Class Form1
    Private _extrasTab As TabPage
    Private _extrasInstallationPath As TextBox
    Private _extrasZoomPreset As ComboBox
    Private _extrasActions As Control
    Private _extrasStatus As Label
    Private _extrasCancel As Button
    Private _extrasCancellation As CancellationTokenSource
    Private _extrasBusy As Boolean
    Private _extrasApplyingZoom As Boolean

    Private Function BuildExtrasTab() As TabPage
        Dim page As New TabPage("EXTRAS") With {.BackColor = ThemeBg}
        Dim scroll As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(28)}
        Dim body As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 1}
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        scroll.Controls.Add(body)
        page.Controls.Add(scroll)
        body.Controls.Add(New Label With {.Text = "EXTRAS", .AutoSize = True, .Font = New Font("Segoe UI", 18, FontStyle.Bold), .ForeColor = ThemeAccent})
        body.Controls.Add(New Label With {.Text = "Zoom presets and Remote Desktop Mosaic travel with this standalone app.", .AutoSize = True, .Margin = New Padding(0, 8, 0, 20)})

        Dim actions As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 1}
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        _extrasActions = actions
        body.Controls.Add(actions)

        Dim zoom As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 1, .Padding = New Padding(16), .BackColor = ThemeCard, .Margin = New Padding(0, 0, 0, 18)}
        zoom.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        zoom.Controls.Add(New Label With {.Text = "KATHANA ZOOM", .AutoSize = True, .Font = New Font("Segoe UI", 12, FontStyle.Bold), .ForeColor = ThemeTextPrimary})
        zoom.Controls.Add(New Label With {.Text = "Close all Kathana windows and stop automation before changing zoom. The current engine.cfg is renamed to a unique backup before every replacement.", .AutoSize = True, .MaximumSize = New Size(900, 0), .Margin = New Padding(0, 10, 0, 14)})
        zoom.Controls.Add(New Label With {.Text = "Kathana installation folder", .AutoSize = True})
        Dim folderRow As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 3, .Margin = New Padding(0, 5, 0, 12)}
        folderRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        folderRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 100))
        folderRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 195))
        _extrasInstallationPath = New TextBox With {.Name = "ExtrasInstallationPath", .Dock = DockStyle.Fill, .Text = FindDefaultExtrasInstallation(), .PlaceholderText = "Choose the folder containing KathanaGame.exe"}
        Dim browse = CreateExtrasButton("Browse…")
        browse.Name = "ExtrasBrowse"
        Dim selectedGame = CreateExtrasButton("Use selected game folder")
        selectedGame.Name = "ExtrasSelectedGame"
        folderRow.Controls.Add(_extrasInstallationPath, 0, 0)
        folderRow.Controls.Add(browse, 1, 0)
        folderRow.Controls.Add(selectedGame, 2, 0)
        zoom.Controls.Add(folderRow)
        zoom.Controls.Add(New Label With {.Text = "Changes userdata\engine.cfg inside this installation.", .AutoSize = True, .ForeColor = ThemeTextSecondary})
        Dim presetRow As New FlowLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .WrapContents = True, .Margin = New Padding(0, 12, 0, 8)}
        _extrasZoomPreset = New ComboBox With {.Name = "ExtrasZoomPreset", .DropDownStyle = ComboBoxStyle.DropDownList, .Width = 200}
        _extrasZoomPreset.Items.AddRange({"x2 zoom", "x3 POV / zoom"})
        _extrasZoomPreset.SelectedIndex = 0
        Dim apply = CreateExtrasButton("Apply selected zoom")
        apply.Name = "ExtrasApplyZoom"
        Dim restore = CreateExtrasButton("Restore previous engine.cfg")
        restore.Name = "ExtrasRestoreZoom"
        presetRow.Controls.AddRange({_extrasZoomPreset, apply, restore})
        zoom.Controls.Add(presetRow)
        actions.Controls.Add(zoom)

        Dim mosaic As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 1, .Padding = New Padding(16), .BackColor = ThemeCard}
        mosaic.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        mosaic.Controls.Add(New Label With {.Text = "REMOTE DESKTOP MOSAIC", .AutoSize = True, .Font = New Font("Segoe UI", 12, FontStyle.Bold), .ForeColor = ThemeTextPrimary})
        mosaic.Controls.Add(New Label With {.Text = "Save the bundled build offline, or download the latest Mosaic build from the KathanaBot repository. Existing Mosaic files are backed up before replacement.", .AutoSize = True, .MaximumSize = New Size(900, 0), .Margin = New Padding(0, 10, 0, 14)})
        Dim mosaicButtons As New FlowLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .WrapContents = True}
        Dim download = CreateExtrasButton("Download latest Mosaic")
        download.Name = "ExtrasDownloadMosaic"
        Dim bundled = CreateExtrasButton("Save bundled Mosaic (1.0.4)")
        bundled.Name = "ExtrasBundledMosaic"
        Dim open = CreateExtrasButton("Open Mosaic")
        open.Name = "ExtrasOpenMosaic"
        mosaicButtons.Controls.AddRange({download, bundled, open})
        mosaic.Controls.Add(mosaicButtons)
        Dim mosaicPath As New TextBox With {.Name = "ExtrasMosaicPath", .ReadOnly = True, .Dock = DockStyle.Top, .Text = RemoteDesktopMosaicService.InstalledPath(AppContext.BaseDirectory), .Margin = New Padding(0, 12, 0, 0)}
        mosaic.Controls.Add(mosaicPath)
        actions.Controls.Add(mosaic)

        _extrasStatus = New Label With {.Name = "ExtrasStatus", .AutoSize = True, .MaximumSize = New Size(900, 0), .Text = "Choose a zoom preset or save Remote Desktop Mosaic.", .ForeColor = ThemeTextSecondary, .Margin = New Padding(0, 18, 0, 10)}
        body.Controls.Add(_extrasStatus)
        _extrasCancel = CreateExtrasButton("Cancel download")
        _extrasCancel.Name = "ExtrasCancel"
        _extrasCancel.Enabled = False
        body.Controls.Add(_extrasCancel)

        AddHandler browse.Click, AddressOf BrowseExtrasInstallation
        AddHandler selectedGame.Click, AddressOf UseSelectedGameForExtras
        AddHandler apply.Click, Async Sub()
                                    Dim root = _extrasInstallationPath.Text.Trim()
                                    Dim preset = If(_extrasZoomPreset.SelectedIndex = 1, ExtrasZoomPreset.X3, ExtrasZoomPreset.X2)
                                    Await RunExtrasOperationAsync("Applying zoom and preserving engine.cfg…",
                                        Function(token) Task.Run(Function()
                                                                    Dim result = New ExtrasZoomService().ApplyPreset(root, preset)
                                                                    Return $"Applied {If(preset = ExtrasZoomPreset.X2, "x2", "x3")}: {result.TargetPath}. Previous config: {result.BackupPath}. Start Kathana to use this zoom."
                                                                End Function, token), True)
                                End Sub
        AddHandler restore.Click, Async Sub()
                                      Dim root = _extrasInstallationPath.Text.Trim()
                                      Await RunExtrasOperationAsync("Restoring the previous engine.cfg…",
                                          Function(token) Task.Run(Function()
                                                                      Dim result = New ExtrasZoomService().RestoreLatestBackup(root)
                                                                      Return $"Restored {result.RestoredFromBackupPath}. Replaced config preserved at {result.BackupPath}."
                                                                  End Function, token), True)
                                  End Sub
        AddHandler download.Click, Async Sub()
                                       Await RunExtrasOperationAsync("Downloading the latest Mosaic build…",
                                           Async Function(token)
                                               Dim path = Await RemoteDesktopMosaicService.DownloadLatestAsync(AppContext.BaseDirectory, token)
                                               Return "Latest repository Mosaic saved: " & path & ". Click Open Mosaic to launch it."
                                           End Function, False)
                                   End Sub
        AddHandler bundled.Click, Async Sub()
                                      Await RunExtrasOperationAsync("Saving the bundled Mosaic build…",
                                          Function(token) Task.Run(Function() "Bundled Mosaic 1.0.4 saved: " & RemoteDesktopMosaicService.ExtractBundled(AppContext.BaseDirectory, token) & ". Click Open Mosaic to launch it.", token), False)
                                  End Sub
        AddHandler open.Click, AddressOf OpenExtrasMosaic
        AddHandler _extrasCancel.Click, Sub() _extrasCancellation?.Cancel()
        Return page
    End Function

    Private Shared Function CreateExtrasButton(text As String) As Button
        Return New Button With {.Text = text, .AutoSize = True, .MinimumSize = New Size(90, 32), .Margin = New Padding(4), .BackColor = Color.FromArgb(31, 81, 125), .ForeColor = Color.White}
    End Function

    Private Shared Function FindDefaultExtrasInstallation() As String
        For Each candidate In {AppContext.BaseDirectory, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Kathana")}
            If File.Exists(Path.Combine(candidate, "KathanaGame.exe")) AndAlso Directory.Exists(Path.Combine(candidate, "userdata")) Then Return Path.GetFullPath(candidate)
        Next
        Return ""
    End Function

    Private Sub BrowseExtrasInstallation(sender As Object, e As EventArgs)
        Dim blocked = GetExtrasBlockReason(False)
        If blocked.Length > 0 Then
            _extrasStatus.Text = blocked
            Return
        End If
        Using dialog As New FolderBrowserDialog With {.Description = "Select the Kathana installation folder containing KathanaGame.exe", .UseDescriptionForTitle = True, .SelectedPath = _extrasInstallationPath.Text}
            If dialog.ShowDialog(Me) = DialogResult.OK Then _extrasInstallationPath.Text = dialog.SelectedPath
        End Using
    End Sub

    Private Sub UseSelectedGameForExtras(sender As Object, e As EventArgs)
        Dim blocked = GetExtrasBlockReason(False)
        If blocked.Length > 0 Then
            _extrasStatus.Text = blocked
            Return
        End If
        Try
            Dim selected = GetSelectedProcessWindowForEdition(BotEdition.Full)
            If selected Is Nothing OrElse Not String.Equals(selected.ProcessName, "KathanaGame", StringComparison.OrdinalIgnoreCase) Then Throw New InvalidOperationException("Select Kathana in Full, or use Browse to choose its installation.")
            Using process = Diagnostics.Process.GetProcessById(selected.ProcessId)
                _extrasInstallationPath.Text = Path.GetDirectoryName(process.MainModule.FileName)
            End Using
        Catch ex As Exception
            _extrasStatus.Text = "Could not read the selected game folder. Use Browse to select the installation folder. " & ex.Message
        End Try
    End Sub

    Private Function GetExtrasBlockReason(requiresStoppedGame As Boolean) As String
        If Not _quizUnlocked Then Return "Extras is locked. Enter 126974 on Home to unlock the extra tabs."
        If _extrasBusy Then Return "An Extras operation is already running. Wait or cancel it first."
        If requiresStoppedGame AndAlso ((_fullEngine IsNot Nothing AndAlso _fullEngine.IsRunning()) OrElse
            (_liteEngine IsNot Nothing AndAlso _liteEngine.IsRunning()) OrElse _tradeRunning OrElse _tradeAnalyzing OrElse
            _quizSolveInProgress OrElse _resuRunning OrElse _resuBusy OrElse
            (chkQuizSolverEnabled IsNot Nothing AndAlso chkQuizSolverEnabled.Checked) OrElse
            (_workflowModes IsNot Nothing AndAlso _workflowModes.Current <> OperatingMode.Idle)) Then
            Return "Stop automation before changing the game's zoom config."
        End If
        Return ""
    End Function

    Private Async Function RunExtrasOperationAsync(statusText As String, operation As Func(Of CancellationToken, Task(Of String)), requiresStoppedGame As Boolean) As Task
        Dim blocked = GetExtrasBlockReason(requiresStoppedGame)
        If blocked.Length > 0 Then
            _extrasStatus.Text = blocked
            Return
        End If
        Dim cancellation As New CancellationTokenSource()
        _extrasCancellation = cancellation
        _extrasBusy = True
        _extrasApplyingZoom = requiresStoppedGame
        _extrasActions.Enabled = False
        _extrasCancel.Enabled = Not requiresStoppedGame
        _extrasStatus.Text = statusText
        Try
            Dim result = Await operation(cancellation.Token)
            If Not IsDisposed AndAlso Not Disposing Then _extrasStatus.Text = result
        Catch ex As OperationCanceledException
            If Not IsDisposed AndAlso Not Disposing Then _extrasStatus.Text = "Cancelled. Existing files and backups were kept."
        Catch ex As UnauthorizedAccessException
            If Not IsDisposed AndAlso Not Disposing Then _extrasStatus.Text = "Windows denied access to this folder. Run KathanaBot with permission to write there, or choose a writable installation. " & ex.Message
        Catch ex As Exception
            If Not IsDisposed AndAlso Not Disposing Then _extrasStatus.Text = "Extras failed: " & ex.Message
        Finally
            _extrasCancellation = Nothing
            cancellation.Dispose()
            _extrasBusy = False
            _extrasApplyingZoom = False
            If Not IsDisposed AndAlso Not Disposing Then
                _extrasActions.Enabled = True
                _extrasCancel.Enabled = False
            End If
        End Try
    End Function

    Private Sub OpenExtrasMosaic(sender As Object, e As EventArgs)
        Dim blocked = GetExtrasBlockReason(False)
        If blocked.Length > 0 Then
            _extrasStatus.Text = blocked
            Return
        End If
        Try
            Dim mosaicFile = RemoteDesktopMosaicService.InstalledPath(AppContext.BaseDirectory)
            If Not File.Exists(mosaicFile) Then Throw New FileNotFoundException("Save the bundled Mosaic build or download the latest one first.")
            Dim start As New ProcessStartInfo(mosaicFile) With {.UseShellExecute = True, .WorkingDirectory = Path.GetDirectoryName(mosaicFile)}
            Dim existingSettings = Path.Combine(AppContext.BaseDirectory, "RemoteDesktopMosaic.settings.json")
            If File.Exists(existingSettings) Then start.ArgumentList.Add(existingSettings)
            Process.Start(start)
        Catch ex As Exception
            _extrasStatus.Text = "Could not open Mosaic: " & ex.Message
        End Try
    End Sub

    Private Sub ShutdownExtras()
        _extrasCancellation?.Cancel()
    End Sub
End Class
