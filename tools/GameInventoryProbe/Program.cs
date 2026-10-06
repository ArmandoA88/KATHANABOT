using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace GameInventoryProbe;

internal static class Program
{
    internal const string ConfirmedGamePath = @"C:\Program Files (x86)\Steam\steamapps\common\Kathana\KathanaGame.exe";
    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };

    [STAThread]
    private static int Main(string[] args)
    {
        Options options;
        try { options = Options.Parse(args); }
        catch (Exception error) { Console.Error.WriteLine(error.Message); PrintHelp(); return 2; }
        if (!options.Run) { PrintHelp(); return 0; }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N")[..8];
        var directory = options.ReportDirectory ?? Path.GetDirectoryName(options.LaunchReport)!;
        var prefix = options.ReportPrefix is null ? Path.Combine(directory, "game-inventory-probe") : Path.GetFullPath(options.ReportPrefix);
        if (Path.HasExtension(prefix)) prefix = Path.Combine(Path.GetDirectoryName(prefix)!, Path.GetFileNameWithoutExtension(prefix));
        var reportPath = prefix + "_" + stamp + ".json";
        var report = new ProbeReport { StartedUtc = DateTime.UtcNow, ReportPath = reportPath, LaunchReportPath = options.LaunchReport, ProbePid = Environment.ProcessId };
        try
        {
            using var self = Process.GetCurrentProcess();
            report.ProbeSessionId = self.SessionId;
            Native.ValidateLayouts();
            if (!File.Exists(options.LaunchReport))
                throw new InvalidOperationException("Required raw-keyboard launch receipt is missing: " + options.LaunchReport);
            var game = GameTarget.Find(options.GamePid);
            report.Game = game.Snapshot;
            ValidateLaunchReceipt(options.LaunchReport, game, report);
            report.InitialForegroundHwnd = Native.Hex(Native.GetForegroundWindow());
            report.GameInitiallyForeground = Native.GetForegroundWindow() == game.Window;
            if (report.GameInitiallyForeground) throw new InvalidOperationException("Leave the logged-in game in the background before launching this tester.");
            report.PreflightInventoryKeyWasDown = Native.KeyIsDown();
            if (report.PreflightInventoryKeyWasDown) throw new InvalidOperationException("I is already held; no input will be sent.");
            report.PreflightModifierWasDown = Native.ModifiersAreDown();
            if (report.PreflightModifierWasDown) throw new InvalidOperationException("Shift/Ctrl/Alt/Windows is held; no input will be sent.");
            report.ScanCodeMapping = Native.MapVirtualKey(0x49, 4);
            if (report.ScanCodeMapping != 0x17) throw new InvalidOperationException($"I did not map to required scan code 0x17 (actual 0x{report.ScanCodeMapping:X}); no input will be sent.");
            report.PreflightPassed = true;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using var form = new ProbeForm(game, report, prefix + "_" + stamp);
            Application.Run(form);
        }
        catch (Exception error)
        {
            report.Aborted = true;
            report.AbortReason ??= error.Message;
            report.Errors.Add(error.Message);
        }
        report.EndedUtc = DateTime.UtcNow;
        report.Scope.GameAcceptance = "unverified";
        report.VisualConfirmationNeeded = true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            using var stream = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(stream, report, JsonOptions);
        }
        catch (Exception error) { Console.Error.WriteLine("Report write failed: " + error.Message); return 2; }
        Console.WriteLine("Report: " + reportPath);
        Console.WriteLine($"Actual game PID/HWND: {report.Game?.Pid}/{report.Game?.Hwnd}; path: {report.Game?.Path}");
        Console.WriteLine($"Accepted I down/up: {report.AcceptedDownCount}/{report.AcceptedUpCount}; owned hook down/up: {report.Filter?.DownCount}/{report.Filter?.UpCount}; pending release: {report.OwnedInventoryReleasePending}.");
        Console.WriteLine($"Before: {report.BeforeCapture?.Path} ({report.BeforeCapture?.Status}); after: {report.AfterCapture?.Path} ({report.AfterCapture?.Status}).");
        Console.WriteLine(report.Aborted ? "Aborted: " + report.AbortReason : "Controlled pair finished. Compare the images and game inventory visually; game acceptance remains unverified.");
        Console.WriteLine("This is a quiescent reception test: the raw-input game may also receive physical typing from other apps. Close this test game normally before typing elsewhere; requested launch variables remain in that game until it exits.");
        return report.Aborted ? 2 : 0;
    }

    private static void PrintHelp() => Console.WriteLine("GameInventoryProbe --run [--game-pid PID] [--launch-report PATH] [--report-dir DIRECTORY] [--report PREFIX]\nNo arguments or --help creates no windows and sends no input. --run requires the raw-keyboard launch receipt, then one manual click within 45 seconds. Only one scan-code I press is attempted. --report adds a unique timestamp suffix.");

    private static void ValidateLaunchReceipt(string path, GameTarget game, ProbeReport report)
    {
        if (!File.Exists(path)) throw new InvalidOperationException("Required raw-keyboard launch receipt is missing: " + path);
        var receipt = JsonSerializer.Deserialize<LaunchReceipt>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidOperationException("Launch receipt is empty.");
        if (receipt.Mode != "launch" || receipt.Status != "started-process-retained" || receipt.LaunchedProcessStillRunning != true || receipt.ReplacementDetected != false ||
            receipt.EnvironmentPropagation != "passed-to-started-process" || receipt.LaunchedProcessId != game.Snapshot.Pid ||
            receipt.LaunchedProcessStartUtc?.UtcDateTime != game.Snapshot.ProcessStartUtc || receipt.WindowsSessionId != game.Snapshot.SessionId ||
            game.Snapshot.SessionId != report.ProbeSessionId || !string.Equals(receipt.GamePath, ConfirmedGamePath, StringComparison.OrdinalIgnoreCase) ||
            receipt.RequestedEnvironment?.GetValueOrDefault("SDL_WINDOWS_RAW_KEYBOARD") != "1" ||
            receipt.RequestedEnvironment?.GetValueOrDefault("SDL_WINDOWS_RAW_KEYBOARD_INPUTSINK") != "1")
            throw new InvalidOperationException("Launch receipt does not match this retained game PID/start/session/path and requested raw-keyboard settings.");
        using var image = new FileStream(ConfirmedGamePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        report.CurrentGameSha256 = Convert.ToHexString(SHA256.HashData(image));
        if (!string.Equals(report.CurrentGameSha256, receipt.GameSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Current game image SHA256 does not match the launch receipt.");
        report.RequestedEnvironment = new() { ["SDL_WINDOWS_RAW_KEYBOARD"] = "1", ["SDL_WINDOWS_RAW_KEYBOARD_INPUTSINK"] = "1" };
        report.EnvironmentPropagation = receipt.EnvironmentPropagation;
        report.EffectiveSdlSettings = "unverified";
        report.LaunchReceiptValidated = true;
    }
}

internal sealed record Options(bool Run, int? GamePid, string LaunchReport, string? ReportDirectory, string? ReportPrefix)
{
    public static Options Parse(string[] args)
    {
        var defaultReceipt = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "input-diagnostics", "kathana-raw-keyboard-launch.json"));
        if (args.Length == 0 || args is ["--help"]) return new(false, null, defaultReceipt, null, null);
        bool run = false;
        int? pid = null;
        string receipt = defaultReceipt;
        string? directory = null, prefix = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var option = args[i];
            if (!seen.Add(option)) throw new ArgumentException("Duplicate option: " + option);
            if (option == "--run") { run = true; continue; }
            if (option is not ("--game-pid" or "--launch-report" or "--report-dir" or "--report")) throw new ArgumentException("Unknown option: " + option);
            if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException(option + " requires a value.");
            switch (option)
            {
                case "--game-pid":
                    if (!int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0) throw new ArgumentException("--game-pid requires a positive process ID.");
                    pid = parsed; break;
                case "--launch-report": receipt = Path.GetFullPath(args[i]); break;
                case "--report-dir": directory = Path.GetFullPath(args[i]); break;
                case "--report": prefix = Path.GetFullPath(args[i]); break;
            }
        }
        if (!run) throw new ArgumentException("Explicit --run is required; argument checking creates no fixture.");
        return new(run, pid, receipt, directory, prefix);
    }
}

internal sealed class GameTarget
{
    internal nint Window { get; }
    internal GameSnapshot Snapshot { get; }
    private GameTarget(nint window, GameSnapshot snapshot) { Window = window; Snapshot = snapshot; }

    internal static GameTarget Find(int? requestedPid)
    {
        var processes = Process.GetProcessesByName("KathanaGame");
        try
        {
            if (processes.Length != 1) throw new InvalidOperationException($"Exactly one KathanaGame process is required; found {processes.Length}.");
            var process = processes[0];
            if (requestedPid.HasValue && process.Id != requestedPid.Value) throw new InvalidOperationException("Requested game PID does not match the one running game.");
            var handle = Native.OpenProcess(0x1000, false, (uint)process.Id);
            if (handle == nint.Zero) throw new InvalidOperationException("Cannot validate the game image and architecture.");
            string path;
            try
            {
                var buffer = new StringBuilder(32768); uint length = (uint)buffer.Capacity;
                if (!Native.QueryFullProcessImageName(handle, 0, buffer, ref length)) throw new InvalidOperationException("Game image path is unknown.");
                path = buffer.ToString();
                if (!string.Equals(path, Program.ConfirmedGamePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Running game executable is outside the confirmed Steam installation.");
                if (!Native.IsWow64Process2(handle, out var machine, out var native) || machine != 0 || native != 0x8664) throw new InvalidOperationException("Game must be confirmed native x64.");
            }
            finally { Native.CloseHandle(handle); }
            var windows = new List<nint>();
            Native.EnumWindowProcedure callback = (window, _) =>
            {
                if (Native.WindowPid(window) == (uint)process.Id && Native.IsWindowVisible(window) && Native.GetWindow(window, 4) == nint.Zero) windows.Add(window);
                return true;
            };
            if (!Native.EnumWindows(callback, nint.Zero) || windows.Count != 1) throw new InvalidOperationException("Cannot identify exactly one visible game top-level window.");
            var target = new GameTarget(windows[0], new GameSnapshot
            {
                Pid = process.Id, Hwnd = Native.Hex(windows[0]), Path = path, NativeX64 = true,
                ProcessStartUtc = process.StartTime.ToUniversalTime(), SessionId = process.SessionId,
                WindowThreadId = Native.GetWindowThreadProcessId(windows[0], out _)
            });
            target.Guard();
            return target;
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    internal void Guard()
    {
        var processes = Process.GetProcessesByName("KathanaGame");
        try
        {
            if (processes.Length != 1 || processes[0].Id != Snapshot.Pid || processes[0].HasExited ||
                processes[0].StartTime.ToUniversalTime() != Snapshot.ProcessStartUtc || processes[0].SessionId != Snapshot.SessionId ||
                !Native.IsWindow(Window) || Native.GetWindowThreadProcessId(Window, out var pid) != Snapshot.WindowThreadId || pid != (uint)Snapshot.Pid ||
                !Native.IsWindowVisible(Window) || Native.IsIconic(Window))
                throw new InvalidOperationException("Game exited, was replaced, duplicated, minimized, or changed its validated window.");
            var handle = Native.OpenProcess(0x1000, false, (uint)Snapshot.Pid);
            if (handle == nint.Zero) throw new InvalidOperationException("Game identity could not be revalidated.");
            try
            {
                var buffer = new StringBuilder(32768); uint length = (uint)buffer.Capacity;
                if (!Native.QueryFullProcessImageName(handle, 0, buffer, ref length) || !string.Equals(buffer.ToString(), Snapshot.Path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Game path changed or became unknown.");
            }
            finally { Native.CloseHandle(handle); }
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }
}

internal sealed class ProbeForm : Form
{
    private readonly GameTarget game;
    private readonly ProbeReport report;
    private readonly string imagePrefix;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    private readonly Label status;
    private readonly ulong marker;
    private OwnedInventoryMessageFilter? filter;
    private long waitStarted, phaseStarted, measureStarted;
    private int phase;
    private bool ownedDown, finished, capturing;
    protected override bool ShowWithoutActivation => true;

    internal ProbeForm(GameTarget game, ProbeReport report, string imagePrefix)
    {
        this.game = game; this.report = report; this.imagePrefix = imagePrefix;
        Span<byte> random = stackalloc byte[8]; RandomNumberGenerator.Fill(random);
        marker = BitConverter.ToUInt64(random) | 0x494E000000000000UL;
        report.InjectionMarker = $"0x{marker:X16}";
        Text = "CONTROLLED INVENTORY PROBE — one I press";
        ClientSize = new Size(650, 150);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(120, 120);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        status = new Label { Dock = DockStyle.Fill, Padding = new Padding(18), Font = new Font("Segoe UI", 11), Text = "Click this test window once within 45 seconds.\nKeep it active until it closes. One I press will test the background game.\nNo extra inventory presses will be sent." };
        Controls.Add(status);
        timer.Tick += Tick;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        report.OwnedFormHwnd = Native.Hex(Handle);
        report.OwnedFormThreadId = Native.GetCurrentThreadId();
        report.WindowsCreated = 1;
        waitStarted = Environment.TickCount64;
        if (!Native.SetWindowPos(Handle, new nint(-1), 0, 0, 0, 0, 0x13)) { Finish("Could not show the owned form on top without activation."); return; }
        report.OwnedFormShownWithoutActivation = true;
        timer.Start();
    }

    private bool OwnForeground()
    {
        report.ForegroundGuardChecks++;
        var valid = IsHandleCreated && Native.IsWindow(Handle) && Native.GetForegroundWindow() == Handle &&
            Native.GetWindowThreadProcessId(Handle, out var pid) == report.OwnedFormThreadId && pid == (uint)Environment.ProcessId;
        if (!valid) report.ForegroundGuardFailures++;
        return valid;
    }

    private async void Tick(object? sender, EventArgs e)
    {
        if (finished || capturing) return;
        try
        {
            var tick = Environment.TickCount64;
            if (phase == 0)
            {
                if (Native.GetForegroundWindow() != Handle)
                {
                    if (tick - waitStarted >= 45000) Finish("Manual focus wait expired; no input sent.");
                    return;
                }
                if (!OwnForeground()) { Finish("Owned form foreground identity failed."); return; }
                report.ManualFocusAchieved = true;
                report.ManualFocusWaitMs = tick - waitStarted;
                report.OwnedTopmostRemoved = Native.SetWindowPos(Handle, new nint(-2), 0, 0, 0, 0, 0x13);
                if (!report.OwnedTopmostRemoved) { Finish("Could not remove topmost from the owned form."); return; }
                game.Guard();
                report.BeforeCapture = new() { Path = imagePrefix + "_before.png", Status = "pending-inconclusive", WorkerStillPending = true };
                capturing = true;
                try { report.BeforeCapture = await GameWindowCapture.TryAsync(game, imagePrefix + "_before.png"); }
                finally { capturing = false; }
                if (finished || IsDisposed || Disposing) return;
                if (report.BeforeCapture.WorkerStillPending) { Finish("Before capture timed out; no input sent."); return; }
                if (!OwnForeground()) { Finish("Focus changed before injection; no input sent."); return; }
                game.Guard();
                if (Native.KeyIsDown()) { Finish("I became held before injection; no input sent."); return; }
                if (Native.ModifiersAreDown()) { report.ModifierWasDownBeforePress = true; Finish("A modifier became held before injection; no input sent."); return; }
                filter = new OwnedInventoryMessageFilter(marker, Handle);
                measureStarted = Environment.TickCount64;
                if (!Native.PostMessage(Handle, 0, new nint(unchecked((long)marker)), new nint(OwnedInventoryMessageFilter.ReadyCookie)))
                { Finish("Could not post owned callback readiness message."); return; }
                status.Text = "Keep this test window active. Measuring one background inventory press…";
                phase = 1; phaseStarted = Environment.TickCount64; return;
            }
            if (tick - measureStarted >= 8000) { Finish("Bounded measurement expired."); return; }
            if (phase is 1 or 2)
            {
                if (!OwnForeground()) { Finish("Focus left the exact owned form; further input stopped."); return; }
                game.Guard();
                if (filter is { MarkerMismatchCount: > 0 } or { CallbackErrors: > 0 } or { ExpiredMatchCount: > 0 })
                { Finish("Owned message attribution/callback checks failed."); return; }
            }
            if (phase == 1)
            {
                if (filter!.ReadinessCount == 0)
                {
                    if (tick - phaseStarted >= 600) Finish("Owned hook callback readiness was not observed; no input sent.");
                    return;
                }
                if (filter.CallbackProcessId != (uint)Environment.ProcessId || filter.CallbackThreadId != report.OwnedFormThreadId)
                { Finish("Readiness callback ran outside the exact owned process/thread."); return; }
                report.CallbackReadinessVerified = true;
                if (Native.KeyIsDown()) { Finish("I became held before the guarded press; no input sent."); return; }
                if (!SendKey(up: false, cleanup: false)) { Finish("Inventory down was not accepted."); return; }
                phase = 2; phaseStarted = Environment.TickCount64; return;
            }
            if (phase == 2 && tick - phaseStarted >= 150)
            {
                if (!SendKey(up: true, cleanup: false)) { Finish("Inventory up was not accepted."); return; }
                report.InjectionPairCompleted = true;
                report.ActualHoldMs = report.UpTick - report.DownTick;
                phase = 3; phaseStarted = Environment.TickCount64; return;
            }
            if (phase == 3 && tick - phaseStarted >= 200)
            {
                report.ForegroundChangedAfterRelease = Native.GetForegroundWindow() != Handle;
                game.Guard();
                report.AfterCapture = new() { Path = imagePrefix + "_after.png", Status = "pending-inconclusive", WorkerStillPending = true };
                capturing = true;
                try { report.AfterCapture = await GameWindowCapture.TryAsync(game, imagePrefix + "_after.png"); }
                finally { capturing = false; }
                if (finished || IsDisposed || Disposing) return;
                game.Guard();
                report.ForegroundChangedAfterRelease |= Native.GetForegroundWindow() != Handle;
                report.ObservationCompleted = true;
                Finish(null);
            }
        }
        catch (Exception error) { report.Errors.Add(error.Message); Finish(error.Message); }
    }

    private bool SendKey(bool up, bool cleanup)
    {
        if (!OwnForeground()) return false;
        if (!cleanup) game.Guard();
        if ((!up && (ownedDown || report.AcceptedDownCount != 0)) || (up && !ownedDown)) return false;
        var input = new[] { new Native.InputPacket { Type = 1, Value = new Native.InputUnion { Keyboard = new Native.KeyboardInput { Scan = 0x17, Flags = 8U | (up ? 2U : 0U), ExtraInfo = (nuint)marker } } } };
        if (!up && (Native.KeyIsDown() || Native.ModifiersAreDown()))
        {
            report.ModifierWasDownBeforePress = Native.ModifiersAreDown();
            report.Errors.Add("I or a modifier became held immediately before down; no press sent.");
            return false;
        }
        // The exact foreground/ownership check is the final operation before
        // global SendInput, after game identity and pre-down state queries.
        if (!OwnForeground()) return false;
        var sent = Native.SendInput(1, input, Marshal.SizeOf<Native.InputPacket>());
        var error = sent == 1 ? 0 : Marshal.GetLastWin32Error();
        var tick = Environment.TickCount64;
        report.Injections.Add(new InjectionObservation { Tick = tick, Up = up, Cleanup = cleanup, Accepted = sent == 1, Error = error, ForegroundHwnd = Native.Hex(Native.GetForegroundWindow()) });
        if (sent != 1) return false;
        ownedDown = !up;
        if (up) { report.AcceptedUpCount++; report.UpTick = tick; }
        else { report.AcceptedDownCount++; report.DownTick = tick; }
        return true;
    }

    private void Finish(string? reason, bool close = true)
    {
        if (finished) return;
        finished = true; timer.Stop();
        if (reason is not null) { report.Aborted = true; report.AbortReason ??= reason; }
        if (ownedDown)
        {
            report.CleanupReleaseAttempted = true;
            try { if (!SendKey(up: true, cleanup: true)) report.Errors.Add("Owned I release was not accepted under the exact foreground guard."); }
            catch (Exception error) { report.Errors.Add("Owned release cleanup: " + error.Message); }
        }
        report.OwnedInventoryReleasePending = ownedDown;
        // Allow an accepted cleanup up to reach the owned retrieval callback
        // before unhooking. No new presses or focus changes occur here.
        var drainEnd = Environment.TickCount64 + 250;
        while (filter is not null && !ownedDown && filter.UpCount < report.AcceptedUpCount && Environment.TickCount64 < drainEnd)
        { Application.DoEvents(); Thread.Sleep(5); }
        if (filter is not null)
        {
            filter.Dispose();
            report.Filter = filter.Snapshot();
            if (!filter.Removed || filter.CallbackErrors != 0 || filter.MarkerMismatchCount != 0 || filter.ExpiredMatchCount != 0)
                report.Errors.Add("Owned filter removal or attribution checks failed.");
        }
        if (IsHandleCreated && Native.IsWindow(Handle))
            report.OwnedTopmostRemoved |= Native.SetWindowPos(Handle, new nint(-2), 0, 0, 0, 0, 0x13);
        var samples = report.Filter?.MarkerSamples;
        report.ControlledObservationValid = report.InjectionPairCompleted && report.ObservationCompleted && report.CallbackReadinessVerified &&
            report.AcceptedDownCount == 1 && report.AcceptedUpCount == 1 && report.LegacyOwnedMarkerCount == 0 && !ownedDown &&
            report.Filter is { Installed: true, Removed: true, RemovalError: 0, DownCount: 1, UpCount: 1, MarkerMismatchCount: 0, CallbackErrors: 0, ExpiredMatchCount: 0 } &&
            samples is { Length: 2 } && samples.All(value => value.MatchesOwnedMarker) && samples.Count(value => value.Up) == 1 &&
            samples.Count(value => !value.Up) == 1 && report.Errors.Count == 0 && !report.Aborted;
        if (ownedDown || (report.AcceptedDownCount > 0 && !report.ControlledObservationValid))
        { report.Aborted = true; report.AbortReason ??= "Controlled injection/filter/cleanup observation was incomplete; game acceptance is unverified."; }
        if (close && !IsDisposed) Close();
    }

    protected override void WndProc(ref System.Windows.Forms.Message message)
    {
        if (Native.IsKeyMessage(unchecked((uint)message.Msg)) && message.HWnd == Handle && message.WParam.ToInt64() == 0x49 &&
            unchecked((ulong)Native.GetMessageExtraInfo().ToInt64()) == marker) report.LegacyOwnedMarkerCount++;
        base.WndProc(ref message);
    }
    protected override void OnFormClosing(FormClosingEventArgs e) { if (!finished) Finish("Owned probe closed before completion.", close: false); base.OnFormClosing(e); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { if (!finished) Finish("Owned probe disposed before completion.", close: false); timer.Dispose(); filter?.Dispose(); }
        base.Dispose(disposing);
    }
}

internal static class GameWindowCapture
{
    internal static async Task<CaptureResult> TryAsync(GameTarget game, string path)
    {
        using var cancellation = new CancellationTokenSource();
        var task = Task.Run(() => Work(game, path, cancellation.Token));
        if (await Task.WhenAny(task, Task.Delay(650)) == task) return await task;
        cancellation.Cancel();
        return new() { Path = path, Status = "timeout-inconclusive", WorkerStillPending = true, Error = "PrintWindow capture did not complete within 650 ms; game effect needs visual confirmation." };
    }
    private static CaptureResult Work(GameTarget game, string path, CancellationToken cancellation)
    {
        var result = new CaptureResult { Path = path };
        try
        {
            game.Guard();
            var window = game.Window;
            if (!Native.IsWindow(window) || !Native.GetWindowRect(window, out var rect)) throw new InvalidOperationException("Game capture window is unavailable.");
            var width = rect.Right - rect.Left; var height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0 || width > 10000 || height > 10000 || (long)width * height > 50_000_000) throw new InvalidOperationException("Game capture dimensions are invalid.");
            using var bitmap = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Black);
                var dc = graphics.GetHdc();
                try
                {
                    game.Guard();
                    result.PrintWindowAccepted = Native.PrintWindow(window, dc, 2);
                    result.NativeError = result.PrintWindowAccepted ? 0 : Marshal.GetLastWin32Error();
                }
                finally { graphics.ReleaseHdc(dc); }
            }
            cancellation.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            bitmap.Save(path, ImageFormat.Png);
            result.Width = width; result.Height = height;
            var dark = 0; var total = 0;
            for (var y = 0; y < 20; y++) for (var x = 0; x < 20; x++)
            {
                var pixel = bitmap.GetPixel(x * (width - 1) / 19, y * (height - 1) / 19);
                if (pixel.R <= 4 && pixel.G <= 4 && pixel.B <= 4) dark++;
                total++;
            }
            result.BlackOrBlankSuspected = dark >= total * 0.99;
            result.Status = !result.PrintWindowAccepted ? "capture-failed-inconclusive" : result.BlackOrBlankSuspected ? "black-inconclusive" : "captured-unreviewed";
        }
        catch (Exception error) { result.Status = "capture-failed-inconclusive"; result.Error = error.Message; }
        return result;
    }
}

internal sealed class LaunchReceipt
{
    public string? Mode { get; set; }
    public string? Status { get; set; }
    public string? GamePath { get; set; }
    public string? GameSha256 { get; set; }
    public Dictionary<string, string>? RequestedEnvironment { get; set; }
    public int? LaunchedProcessId { get; set; }
    public DateTimeOffset? LaunchedProcessStartUtc { get; set; }
    public int? WindowsSessionId { get; set; }
    public bool? LaunchedProcessStillRunning { get; set; }
    public bool? ReplacementDetected { get; set; }
    public string? EnvironmentPropagation { get; set; }
}
internal sealed record GameSnapshot
{
    public int Pid { get; init; }
    public string Hwnd { get; init; } = "";
    public string Path { get; init; } = "";
    public bool NativeX64 { get; init; }
    public DateTime ProcessStartUtc { get; init; }
    public int SessionId { get; init; }
    public uint WindowThreadId { get; init; }
}
internal sealed class ProbeScope
{
    public string Purpose { get; set; } = "One guarded inventory I press to measure an existing background game's reception after raw-keyboard launch; only this probe's foreground window/thread is filtered.";
    public string GameAcceptance { get; set; } = "unverified";
    public bool ForeignAppOrGameHookInstalled { get; set; }
    public bool TargetWindowInputMessagesSent { get; set; }
}
internal sealed class ProbeReport
{
    public ProbeScope Scope { get; set; } = new();
    public DateTime StartedUtc { get; set; }
    public DateTime EndedUtc { get; set; }
    public string ReportPath { get; set; } = "";
    public string LaunchReportPath { get; set; } = "";
    public bool LaunchReceiptValidated { get; set; }
    public Dictionary<string, string> RequestedEnvironment { get; set; } = [];
    public string EnvironmentPropagation { get; set; } = "unverified";
    public string EffectiveSdlSettings { get; set; } = "unverified";
    public string CurrentGameSha256 { get; set; } = "";
    public GameSnapshot? Game { get; set; }
    public int ProbePid { get; set; }
    public int ProbeSessionId { get; set; }
    public string InitialForegroundHwnd { get; set; } = "";
    public bool GameInitiallyForeground { get; set; }
    public bool PreflightInventoryKeyWasDown { get; set; }
    public bool PreflightModifierWasDown { get; set; }
    public bool ModifierWasDownBeforePress { get; set; }
    public uint ScanCodeMapping { get; set; }
    public bool PreflightPassed { get; set; }
    public int WindowsCreated { get; set; }
    public string OwnedFormHwnd { get; set; } = "";
    public uint OwnedFormThreadId { get; set; }
    public bool OwnedFormShownWithoutActivation { get; set; }
    public bool OwnedTopmostRemoved { get; set; }
    public bool ManualFocusAchieved { get; set; }
    public long ManualFocusWaitMs { get; set; }
    public string InjectionMarker { get; set; } = "";
    public bool CallbackReadinessVerified { get; set; }
    public int ForegroundGuardChecks { get; set; }
    public int ForegroundGuardFailures { get; set; }
    public int AcceptedDownCount { get; set; }
    public int AcceptedUpCount { get; set; }
    public long? DownTick { get; set; }
    public long? UpTick { get; set; }
    public long? ActualHoldMs { get; set; }
    public bool InjectionPairCompleted { get; set; }
    public bool ObservationCompleted { get; set; }
    public bool ForegroundChangedAfterRelease { get; set; }
    public int LegacyOwnedMarkerCount { get; set; }
    public bool CleanupReleaseAttempted { get; set; }
    public bool OwnedInventoryReleasePending { get; set; }
    public FilterSnapshot? Filter { get; set; }
    public CaptureResult? BeforeCapture { get; set; }
    public CaptureResult? AfterCapture { get; set; }
    public bool ControlledObservationValid { get; set; }
    public bool VisualConfirmationNeeded { get; set; } = true;
    public bool Aborted { get; set; }
    public string? AbortReason { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<InjectionObservation> Injections { get; set; } = [];
}
internal sealed record InjectionObservation
{
    public long Tick { get; init; }
    public bool Up { get; init; }
    public bool Cleanup { get; init; }
    public bool Accepted { get; init; }
    public int Error { get; init; }
    public string ForegroundHwnd { get; init; } = "";
}
internal sealed class CaptureResult
{
    public string Path { get; set; } = "";
    public string Method { get; set; } = "PrintWindow PW_RENDERFULLCONTENT (full game window; read-only)";
    public string Status { get; set; } = "inconclusive";
    public bool PrintWindowAccepted { get; set; }
    public bool BlackOrBlankSuspected { get; set; }
    public bool WorkerStillPending { get; set; }
    public int NativeError { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Error { get; set; }
}
