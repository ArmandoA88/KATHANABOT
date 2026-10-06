using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace RawKeyboardProbe;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static int Main(string[] args)
    {
        if (args.Contains("--sentinel", StringComparer.Ordinal)) return Sentinel.Run(args);
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            Console.WriteLine("RawKeyboardProbe --run|--capture-only [--report PATH] [--wait-for-focus SECONDS] [--filter-owned-legacy|--filter-owned-queue|--filter-owned-message]");
            Console.WriteLine("--run sends F24 only while a verified owned sentinel process is foreground.");
            Console.WriteLine("--capture-only records raw F24 for two seconds and sends no input or activation request.");
            Console.WriteLine("--wait-for-focus (1-60 seconds) shows an owned test window without activation; click it once to begin.");
            Console.WriteLine("--filter-owned-legacy tests a bounded hook matching only this probe's injected F24 marker; --run required.");
            Console.WriteLine("--filter-owned-queue compares baseline and queue suppression in the owned sentinel thread; --run required.");
            Console.WriteLine("--filter-owned-message compares baseline and message-retrieval suppression in the owned sentinel thread; --run required.");
            return 0;
        }
        Options options;
        try { options = Options.Parse(args); }
        catch (ArgumentException error) { Console.Error.WriteLine(error.Message); return 2; }
        var report = new ProbeReport { Mode = options.FilterOwnedMessage ? "run-filter-owned-message" : options.FilterOwnedQueue ? "run-filter-owned-queue" : options.FilterOwnedLegacy ? "run-filter-owned-legacy" : options.Run ? "run" : "capture-only", FilterOwnedLegacyRequested = options.FilterOwnedLegacy, FilterOwnedQueueRequested = options.FilterOwnedQueue, FilterOwnedMessageRequested = options.FilterOwnedMessage, StartedUtc = DateTimeOffset.UtcNow };
        try
        {
            if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) throw new InvalidOperationException("This probe requires x64 Windows.");
            Native.ValidateLayouts();
            using var probe = new Receiver(options.Run, options.WaitForFocusSeconds, options.FilterOwnedLegacy, options.FilterOwnedQueue, options.FilterOwnedMessage, report);
            probe.Run();
        }
        catch (Exception error)
        {
            report.Aborted = true;
            report.AbortReason ??= error.Message;
            report.Errors.Add(error.Message);
        }
        report.FinishedUtc = DateTimeOffset.UtcNow;
        report.PopulateCounters();
        if (options.FilterOwnedLegacy && !report.Aborted && !report.FilterObservationValid)
        {
            report.Aborted = true;
            report.AbortReason = "Filtered fixture did not establish complete owned hook observations and cleanup.";
            report.Errors.Add(report.AbortReason);
        }
        if (options.FilterOwnedQueue && !report.Aborted && !report.QueueObservationValid)
        {
            report.Aborted = true;
            report.AbortReason = "Queue fixture did not establish a complete paired baseline, suppression observations and cleanup.";
            report.Errors.Add(report.AbortReason);
        }
        if (options.FilterOwnedMessage && !report.Aborted && !report.MessageObservationValid)
        {
            report.Aborted = true;
            report.AbortReason = "Message fixture did not establish full marker attribution, a complete paired baseline and clean suppression observations.";
            report.Errors.Add(report.AbortReason);
        }
        try
        {
            var path = Path.GetFullPath(options.ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions), new UTF8Encoding(false));
            Console.WriteLine($"Report: {path}");
        }
        catch (Exception error) { Console.Error.WriteLine($"Report write failed: {error.Message}"); return 2; }
        Console.WriteLine($"Foreground app known: {report.ForegroundAppKnown}; injected down/up: {report.SenderInjectedDownCount}/{report.SenderInjectedUpCount}.");
        Console.WriteLine($"Receiver raw F24: {report.ReceiverRawF24Count}, INPUTSINK: {report.RawInputSinkCount}, INPUT: {report.RawInputForegroundCount}, zero/nonzero device: {report.RawZeroDeviceCount}/{report.RawNonZeroDeviceCount}.");
        Console.WriteLine($"Sentinel owned F24 legacy down/up: {report.SentinelLegacyDownCount}/{report.SentinelLegacyUpCount}; status: {(report.Aborted ? report.AbortReason : "completed")}.");
        if (options.Run) Console.WriteLine($"Input sequence complete: {report.InjectionSequenceCompleted}; observation complete: {report.ObservationCompleted}; foreground changed after release: {report.ForegroundChangedDuringFinalObservation}.");
        if (options.FilterOwnedLegacy) Console.WriteLine($"Owned F24 filter installed/removed: {report.FilterHookInstalled}/{report.FilterHookRemoved}; consumed down/up: {report.FilterHookDownCount}/{report.FilterHookUpCount}; errors/expired: {report.FilterHookCallbackErrors}/{report.FilterHookExpiredMatchCount}; observation valid: {report.FilterObservationValid}.");
        if (options.FilterOwnedQueue || options.FilterOwnedMessage)
        {
            var valid = options.FilterOwnedMessage ? report.MessageObservationValid : report.QueueObservationValid;
            Console.WriteLine($"Owned {report.QueueReport?.HookKind} baseline down/up: {report.QueueReport?.BaselineDownCount}/{report.QueueReport?.BaselineUpCount}; suppressed down/up: {report.QueueReport?.SuppressedDownCount}/{report.QueueReport?.SuppressedUpCount}; removed: {report.QueueReport?.Removed}; observation valid: {valid}.");
            foreach (var method in report.Methods)
            {
                Console.WriteLine(method.Skipped ? $"{method.Method}: skipped ({method.SkipReason})." : $"{method.Method}: marker-matched raw={method.RawMatchingMarkerCount}, INPUTSINK={method.RawInputSinkCount}, sentinel legacy={method.SentinelLegacyCount}.");
                if (!valid && method.WindowStartTick.HasValue && report.QueueReport is not null)
                {
                    var end = method.WindowEndTick ?? report.MeasurementEndedTick;
                    var samples = report.QueueReport.MarkerSamples.Where(sample => sample.Tick >= method.WindowStartTick && sample.Tick <= end).ToArray();
                    if (samples.Length > 0) Console.WriteLine($"{method.Method} full hook markers: {string.Join(", ", samples.Select(sample => $"{(sample.Up ? "up" : "down")}={sample.ActualFullExtraInfo} (match={sample.MatchesOwnedMarker})"))}.");
                    var dispatched = report.SentinelLegacyF24.Where(value => value.Tick >= method.WindowStartTick && value.Tick <= end).Take(16).ToArray();
                    if (dispatched.Length > 0) Console.WriteLine($"{method.Method} full dispatch markers: {string.Join(", ", dispatched.Select(value => $"0x{value.FullExtraInformation:X16} (match={value.MatchesOwnedInjectionMarker})"))}.");
                }
            }
            Console.WriteLine($"Filtered-stage background raw delivery observed: {(options.FilterOwnedMessage ? report.MessageFilteredDeliveryObserved : report.QueueFilteredDeliveryObserved)}. This result applies only to the owned fixture.");
        }
        Console.WriteLine("Controlled delivery evidence does not establish background game routing or enforcement compatibility.");
        return report.Aborted ? 2 : 0;
    }
}

internal sealed record Options(bool Run, string ReportPath, int WaitForFocusSeconds, bool FilterOwnedLegacy, bool FilterOwnedQueue, bool FilterOwnedMessage)
{
    public static Options Parse(string[] args)
    {
        bool run = false, capture = false, filterOwnedLegacy = false, filterOwnedQueue = false, filterOwnedMessage = false;
        int waitForFocusSeconds = 0;
        string report = Path.Combine(Environment.CurrentDirectory, "input-diagnostics", $"raw-keyboard-probe_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json");
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--run": run = true; break;
                case "--capture-only": capture = true; break;
                case "--filter-owned-legacy": filterOwnedLegacy = true; break;
                case "--filter-owned-queue": filterOwnedQueue = true; break;
                case "--filter-owned-message": filterOwnedMessage = true; break;
                case "--report":
                    if (++index == args.Length || args[index].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("--report requires a local file path.");
                    report = args[index]; break;
                case "--wait-for-focus":
                    if (++index == args.Length || !int.TryParse(args[index], out waitForFocusSeconds) || waitForFocusSeconds is < 1 or > 60)
                        throw new ArgumentException("--wait-for-focus requires 1 through 60 seconds.");
                    break;
                default: throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }
        if (run == capture) throw new ArgumentException("Select exactly one of --run or --capture-only.");
        if (!run && waitForFocusSeconds != 0) throw new ArgumentException("--wait-for-focus is available only with --run.");
        if (!run && filterOwnedLegacy) throw new ArgumentException("--filter-owned-legacy is available only with --run.");
        if (!run && filterOwnedQueue) throw new ArgumentException("--filter-owned-queue is available only with --run.");
        if (!run && filterOwnedMessage) throw new ArgumentException("--filter-owned-message is available only with --run.");
        if (new[] { filterOwnedLegacy, filterOwnedQueue, filterOwnedMessage }.Count(value => value) > 1)
            throw new ArgumentException("Choose only one of --filter-owned-legacy, --filter-owned-queue or --filter-owned-message.");
        return new Options(run, report, waitForFocusSeconds, filterOwnedLegacy, filterOwnedQueue, filterOwnedMessage);
    }
}

internal sealed class Receiver : IDisposable
{
    private readonly bool inject;
    private readonly int waitForFocusSeconds;
    private readonly bool filterOwnedLegacy;
    private readonly bool filterOwnedQueue;
    private readonly bool filterOwnedMessage;
    private bool PairedFilterRequested => filterOwnedQueue || filterOwnedMessage;
    private readonly ProbeReport report;
    private readonly string session = Guid.NewGuid().ToString("N");
    private readonly uint marker = 0x524B0000U | (uint)Random.Shared.Next(1, 65536);
    private readonly Native.WindowProcedure callback;
    private readonly List<MethodReport> methods = [];
    private Native.WindowClass registration;
    private nint receiver;
    private nint sentinel;
    private uint sentinelPid;
    private Process? child;
    private NamedPipeServerStream? pipe;
    private StreamReader? reader;
    private StreamWriter? writer;
    private Task<SentinelResult?>? childResult;
    private System.Threading.Timer? watchdog;
    private long startedTick;
    private readonly long fixtureStartedTick = Environment.TickCount64;
    private long measurementBudgetTick;
    private int phase;
    private long due;
    private bool rawRegistered;
    private bool focusTaken;
    private bool finished;
    private bool ownedDown;
    private bool heldScan;
    private ushort heldMakeCode;
    private uint heldExtendedFlag;
    private MethodReport? current;
    private OwnedF24LegacyFilter? legacyFilter;

    public Receiver(bool inject, int waitForFocusSeconds, bool filterOwnedLegacy, bool filterOwnedQueue, bool filterOwnedMessage, ProbeReport report)
    {
        this.inject = inject;
        this.waitForFocusSeconds = waitForFocusSeconds;
        this.filterOwnedLegacy = filterOwnedLegacy;
        this.filterOwnedQueue = filterOwnedQueue;
        this.filterOwnedMessage = filterOwnedMessage;
        this.report = report;
        callback = WindowProcedure;
    }

    public void Run()
    {
        report.ReceiverPid = Environment.ProcessId;
        report.ManualFocusWaitSeconds = waitForFocusSeconds;
        report.InjectionMarker = $"0x{marker:X8}";
        report.InitialForeground = WindowSnapshot.Capture();
        report.ForegroundAppKnown = report.InitialForeground.ProcessNameKnown;
        if (inject && (!report.ForegroundAppKnown || report.InitialForeground.IsGame))
        {
            Abort(report.InitialForeground.IsGame ? "Refused: the game is initially foreground." : "Refused: the initial foreground app could not be identified.");
            return;
        }
        report.PreflightF24WasDown = (Native.GetAsyncKeyState(0x87) & 0x8000) != 0;
        if (inject && report.PreflightF24WasDown) { Abort("Refused: F24 was already held before the controlled probe."); return; }
        registration = Native.RegisterOwnedClass($"RawKeyboardProbe.Receiver.{session}", callback);
        receiver = Native.CreateOwnedWindow(registration, "Raw keyboard probe: background receiver", visible: false);
        report.ReceiverHwnd = Native.Hex(receiver);
        var devices = new[] { new Native.RawInputDevice { UsagePage = 1, Usage = 6, Flags = Native.RidevInputSink, Target = receiver } };
        if (!Native.RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<Native.RawInputDevice>())) throw Native.Error("RegisterRawInputDevices(INPUTSINK)");
        rawRegistered = true;
        report.RawInputRegistrationSucceeded = true;
        report.RawInputRegistrationFlags = "RIDEV_INPUTSINK (0x100); keyboard usage page 1, usage 6; legacy delivery retained";

        var pipeName = $"RawKeyboardProbe-{Environment.ProcessId}-{session}";
        pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve the owned helper executable.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--sentinel", "--pipe", pipeName, "--session", session, "--marker", marker.ToString("X8"), inject && waitForFocusSeconds == 0 ? "--activate" : "--no-activate" }) start.ArgumentList.Add(argument);
        if (filterOwnedQueue) start.ArgumentList.Add("--filter-owned-queue");
        if (filterOwnedMessage) start.ArgumentList.Add("--filter-owned-message");
        if (waitForFocusSeconds > 0)
        {
            start.ArgumentList.Add("--wait-for-focus");
            start.ArgumentList.Add(waitForFocusSeconds.ToString());
        }
        child = Process.Start(start) ?? throw new InvalidOperationException("Could not start the owned foreground sentinel.");
        report.SentinelPid = child.Id;
        var connection = pipe.WaitForConnectionAsync();
        if (!connection.Wait(1800)) throw new InvalidOperationException("Owned sentinel pipe handshake timed out.");
        reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
        var readyLine = reader.ReadLineAsync();
        if (!readyLine.Wait(1400)) throw new InvalidOperationException("Owned sentinel ready report timed out.");
        var ready = JsonSerializer.Deserialize<SentinelReady>(readyLine.Result ?? throw new InvalidOperationException("Sentinel pipe closed before readiness."));
        if (ready is null || ready.Type != "ready" || ready.Session != session || ready.Pid != child.Id || !long.TryParse(ready.Hwnd, out var hwnd))
            throw new InvalidOperationException("Owned sentinel identity handshake was invalid.");
        sentinel = new nint(hwnd);
        sentinelPid = (uint)child.Id;
        report.SentinelHwnd = Native.Hex(sentinel);
        report.SentinelOwnershipValidated = OwnSentinelExists();
        if (!report.SentinelOwnershipValidated) throw new InvalidOperationException("Started sentinel HWND does not belong to the started process.");
        report.SentinelVisibleBeforeShow = ready.VisibleBeforeShow;
        report.SentinelVisibleAfterFirstShow = ready.VisibleAfterFirstShow;
        report.SentinelSecondShowNeeded = ready.SecondShowNeeded;
        report.SentinelVisibleAfterShow = ready.VisibleAfterShow;
        report.SentinelWindowRect = ready.WindowRect;
        report.SentinelShowMethod = ready.ShowMethod;
        report.SentinelVisibleAtParentReady = Native.IsWindowVisible(sentinel);
        childResult = ReceiveSentinelResult();
        if (!report.SentinelVisibleAtParentReady) { Abort("Owned sentinel remained hidden after its explicit show requests; no input sent."); return; }
        focusTaken = inject && Native.GetForegroundWindow() == sentinel;
        report.FixtureTookForeground = focusTaken;
        report.ScanCodeMapping = Native.MapVirtualKey(0x87, 4); // MAPVK_VK_TO_VSC_EX
        var pairedPrefix = filterOwnedMessage ? "message-" : "queue-";
        var methodPrefix = PairedFilterRequested ? pairedPrefix + "baseline-" : "";
        methods.Add(new MethodReport { Method = methodPrefix + "virtual-key-F24", VirtualKey = 0x87 });
        var mapped = report.ScanCodeMapping;
        var prefix = (mapped >> 8) & 0xFF;
        if ((mapped & 0xFF) == 0 || (prefix != 0 && prefix != 0xE0))
            methods.Add(new MethodReport { Method = methodPrefix + "scan-code-F24", VirtualKey = 0x87, Skipped = true, SkipReason = "F24 has no supported zero/E0-prefix MAPVK_VK_TO_VSC_EX mapping.", MappedScanCode = mapped });
        else methods.Add(new MethodReport { Method = methodPrefix + "scan-code-F24", VirtualKey = 0x87, MappedScanCode = mapped });
        if (PairedFilterRequested)
        {
            methods.Add(new MethodReport { Method = pairedPrefix + "filtered-virtual-key-F24", VirtualKey = 0x87 });
            methods.Add(new MethodReport { Method = pairedPrefix + "filtered-scan-code-F24", VirtualKey = 0x87, MappedScanCode = mapped, Skipped = methods[1].Skipped, SkipReason = methods[1].SkipReason });
        }
        report.Methods = methods;
        if (inject && waitForFocusSeconds > 0)
        {
            Console.WriteLine($"Click the CONTROLLED F24 PROBE test window once within {waitForFocusSeconds} seconds. This is a self-test; no game inputs.");
            var waitStart = Environment.TickCount64;
            report.ManualFocusAchieved = WaitForOwnedForeground(waitForFocusSeconds * 1000);
            report.ManualFocusElapsedMs = Environment.TickCount64 - waitStart;
            if (!report.ManualFocusAchieved) { Abort("Manual focus wait expired or the owned sentinel closed; no input sent."); return; }
            report.SentinelTopmostRemoved = OwnSentinelExists() && Native.SetWindowPos(sentinel, new nint(-2), 0, 0, 0, 0, 0x13);
            if (!report.SentinelTopmostRemoved) { Abort("Could not remove topmost from the owned test window; no input sent."); return; }
            measurementBudgetTick = Environment.TickCount64;
        }
        else if (inject && !WaitForOwnedForeground(500))
        {
            report.OwnedForegroundActivationHelperAttempted = true;
            if (!ActivateOwnedSentinel() || !WaitForOwnedForeground(500))
            {
                Abort("Owned sentinel did not obtain foreground after the bounded activation attempt; no input sent.");
                return;
            }
        }
        if (inject && !GuardForeground()) { Abort("Owned sentinel foreground guard failed after activation; no input sent."); return; }
        focusTaken = inject && Native.GetForegroundWindow() == sentinel;
        report.FixtureTookForeground |= focusTaken;
        report.ReceiverIsSeparateBackgroundProcess = inject && Native.GetForegroundWindow() == sentinel && sentinelPid != (uint)Environment.ProcessId;
        if (filterOwnedLegacy)
        {
            legacyFilter = new OwnedF24LegacyFilter(marker, sentinel, sentinelPid);
            report.FilterHookInstalled = true;
            report.FilterHookScope = "Bounded WH_KEYBOARD_LL in this probe only; consume VK_F24 + LLKHF_INJECTED + exact owned marker. All unmatched input passes. No hook is loaded into the game. Already-enqueued owned events remain filtered if focus changes.";
        }
        if (PairedFilterRequested && !ConfigureOwnedQueue(suppress: false)) return;
        startedTick = Environment.TickCount64;
        report.MeasurementStartedTick = startedTick;
        if (waitForFocusSeconds == 0) measurementBudgetTick = fixtureStartedTick;
        due = startedTick + (inject ? 200 : 2000);
        if (Native.SetTimer(receiver, 1, 10, nint.Zero) == 0) throw Native.Error("SetTimer(receiver)");
        // Include helper startup in the deadline, retaining time for bounded cleanup.
        var remaining = Math.Max(1, 5800 - (Environment.TickCount64 - measurementBudgetTick));
        watchdog = new System.Threading.Timer(_ => Native.PostMessage(receiver, Native.WmWatchdog, nint.Zero, nint.Zero), null, remaining, Timeout.Infinite);
        Native.PumpMessages();
        report.MeasurementEndedTick = Environment.TickCount64;
    }

    private async Task<SentinelResult?> ReceiveSentinelResult()
    {
        try
        {
            var line = await reader!.ReadLineAsync();
            if (line is null || line.Length > 65536) return null;
            var result = JsonSerializer.Deserialize<SentinelResult>(line);
            return result?.Session == session && result.Pid == child!.Id ? result : null;
        }
        catch (IOException) { return null; }
    }

    private bool OwnSentinelExists() => sentinel != nint.Zero && Native.IsWindow(sentinel) && Native.WindowPid(sentinel) == sentinelPid && child is not null && !child.HasExited;

    private bool WaitForOwnedForeground(int milliseconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (!finished && OwnSentinelExists() && Environment.TickCount64 < deadline)
        {
            if (Native.GetForegroundWindow() == sentinel) return true;
            if (!Native.PumpPendingMessages()) return false;
            Thread.Sleep(10);
        }
        return !finished && OwnSentinelExists() && Native.GetForegroundWindow() == sentinel;
    }

    private bool ActivateOwnedSentinel()
    {
        if (!OwnSentinelExists()) return false;
        var foreground = WindowSnapshot.Capture();
        if (!foreground.ProcessNameKnown || foreground.IsGame)
        {
            report.Errors.Add("Owned-fixture activation refused: current foreground app is unknown or KathanaGame.");
            return false;
        }
        var callerThread = Native.GetCurrentThreadId();
        var foregroundThread = Native.WindowThread(foreground.Handle);
        var attached = false;
        var detached = true;
        try
        {
            if (!OwnSentinelExists() || Native.GetForegroundWindow() != foreground.Handle || Native.WindowPid(foreground.Handle) != foreground.Pid) return false;
            if (foregroundThread != 0 && foregroundThread != callerThread)
            {
                report.ForegroundThreadAttachAttempted = true;
                attached = Native.AttachThreadInput(callerThread, foregroundThread, true);
                report.ForegroundThreadAttachSucceeded = attached;
                if (!attached) { report.Errors.Add($"Owned-fixture AttachThreadInput failed: {Marshal.GetLastWin32Error()}"); return false; }
            }
            // The only activation target is the nonce/PID/HWND-validated owned child.
            // No fake keys, game activation, or game input mechanism is involved.
            if (!OwnSentinelExists() || Native.GetForegroundWindow() != foreground.Handle) return false;
            report.OwnedForegroundActivationCallAccepted = Native.SetForegroundWindow(sentinel);
        }
        finally
        {
            if (attached)
            {
                detached = Native.AttachThreadInput(callerThread, foregroundThread, false);
                report.ForegroundThreadDetached = detached;
                if (!detached) report.Errors.Add($"Owned-fixture thread detach failed: {Marshal.GetLastWin32Error()}");
            }
        }
        return detached && OwnSentinelExists();
    }

    private bool GuardForeground()
    {
        var allowed = OwnSentinelExists() && Native.IsWindowVisible(sentinel) && Native.GetForegroundWindow() == sentinel;
        report.ForegroundGuardChecks++;
        if (!allowed) report.ForegroundGuardFailures++;
        return allowed;
    }

    private bool ConfigureOwnedQueue(bool suppress)
    {
        var expectedPairs = methods[1].Skipped ? 1 : 2;
        var command = suppress ? 1 | (expectedPairs << 8) : 0;
        // Validate the exact started HWND/PID and foreground before every command.
        if (!GuardForeground()) { Abort("Owned sentinel guard rejected queue configuration."); return false; }
        var sent = Native.SendMessageTimeout(sentinel, Native.WmConfigureQueue, new nint(marker), new nint(command), 3, 500, out var result);
        if (sent == nint.Zero || result != 1)
        {
            Abort(suppress ? "Owned sentinel did not verify the baseline and enable queue suppression." : "Owned sentinel did not install its queue filter.");
            return false;
        }
        if (suppress) report.QueueSuppressionEnabled = true;
        else report.QueueInstallCommandAccepted = true;
        return true;
    }

    private nint WindowProcedure(nint hwnd, uint message, nint wParam, nint lParam)
    {
        try
        {
            if (message == Native.WmInput && hwnd == receiver)
            {
                ReadRawInput(wParam, lParam);
                // DefWindowProc performs WM_INPUT cleanup, including RIM_INPUT.
                return Native.DefWindowProc(hwnd, message, wParam, lParam);
            }
            if (message == Native.WmTimer && hwnd == receiver) { OnTimer(); return nint.Zero; }
            if (message == Native.WmWatchdog || message == Native.WmClose) { Abort("Bounded probe stopped before completion."); return nint.Zero; }
            if (Native.IsKeyMessage(message) && wParam.ToInt64() == 0x87 && unchecked((uint)Native.GetMessageExtraInfo().ToInt64()) == marker)
                report.ReceiverOwnedLegacyF24Count++;
        }
        catch (Exception error) { report.Errors.Add(error.Message); Abort("Receiver callback failed: " + error.Message); }
        return Native.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void ReadRawInput(nint messageCode, nint handle)
    {
        if (finished || startedTick == 0 || Environment.TickCount64 - startedTick > 6800) return;
        uint size = 0;
        var headerSize = (uint)Marshal.SizeOf<Native.RawInputHeader>();
        if (Native.GetRawInputData(handle, Native.RidInput, nint.Zero, ref size, headerSize) == uint.MaxValue) { RecordRawError("GetRawInputData(size)"); return; }
        if (size < headerSize + Marshal.SizeOf<Native.RawKeyboard>() || size > 4096) { RecordRawError("Unexpected raw input packet size"); return; }
        var memory = Marshal.AllocHGlobal((int)size);
        try
        {
            var received = Native.GetRawInputData(handle, Native.RidInput, memory, ref size, headerSize);
            if (received == uint.MaxValue || received < headerSize + Marshal.SizeOf<Native.RawKeyboard>()) { RecordRawError("GetRawInputData(payload)"); return; }
            var header = Marshal.PtrToStructure<Native.RawInputHeader>(memory);
            if (header.Type != 1 || header.Size > received) return;
            var keyboard = Marshal.PtrToStructure<Native.RawKeyboard>(nint.Add(memory, (int)headerSize));
            if (keyboard.VirtualKey != 0x87) return; // Never retain other keyboard activity.
            report.RawF24.Add(new RawObservation
            {
                Tick = Environment.TickCount64, DeviceHandle = Native.Hex(header.Device), DeviceIsZero = header.Device == nint.Zero,
                MakeCode = keyboard.MakeCode, Flags = keyboard.Flags, VirtualKey = keyboard.VirtualKey, Message = keyboard.Message,
                ExtraInformation = keyboard.ExtraInformation, MatchesOwnedInjectionMarker = keyboard.ExtraInformation == marker,
                WmInputCode = unchecked((uint)messageCode.ToInt64()) & 0xFF, HeaderInputCode = (uint)(header.WParam & 0xFF),
                ReceiverWindowIsForeground = Native.GetForegroundWindow() == receiver,
                OwnedSentinelIsForeground = OwnSentinelExists() && Native.GetForegroundWindow() == sentinel,
                ReceiverProcessIsForeground = Native.WindowPid(Native.GetForegroundWindow()) == (uint)Environment.ProcessId
            });
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    private void RecordRawError(string operation)
    {
        report.RawReadErrors++;
        if (report.Errors.Count < 32) report.Errors.Add($"{operation}: Win32 error {Marshal.GetLastWin32Error()}");
    }

    private void OnTimer()
    {
        if (finished) return;
        var tick = Environment.TickCount64;
        if (tick - measurementBudgetTick >= 5700) { Abort("Probe reached its bounded measurement deadline."); return; }
        if (inject)
        {
            if (!report.InjectionSequenceCompleted || ownedDown)
            {
                if (!GuardForeground()) { Abort("Foreground changed away from the exact owned sentinel; remaining input aborted."); return; }
            }
            else if (Native.GetForegroundWindow() != sentinel) report.ForegroundChangedDuringFinalObservation = true;
        }
        if (tick < due) return;
        if (!inject) { report.ObservationCompleted = true; Finish(); return; }
        if (PairedFilterRequested) { OnQueueTimer(tick); return; }
        switch (phase)
        {
            case 0:
                current = methods[0];
                if (!SendKey(up: false, scan: false, current)) return;
                due = current.DownTick!.Value + 150; phase = 1; break;
            case 1:
                if (!SendKey(up: true, scan: false, current!)) return;
                report.InjectionSequenceCompleted = methods[1].Skipped;
                due = Environment.TickCount64 + 400; phase = 2; break;
            case 2:
                current!.WindowEndTick = tick - 1;
                current = methods[1];
                if (current.Skipped) { report.ObservationCompleted = true; Finish(); return; }
                if (!SendKey(up: false, scan: true, current)) return;
                due = current.DownTick!.Value + 150; phase = 3; break;
            case 3:
                if (!SendKey(up: true, scan: true, current!)) return;
                report.InjectionSequenceCompleted = true;
                due = Environment.TickCount64 + 400; phase = 4; break;
            default:
                current!.WindowEndTick = tick; report.ObservationCompleted = true; Finish(); break;
        }
    }

    private void OnQueueTimer(long tick)
    {
        switch (phase)
        {
            case 0:
                current = methods[0];
                if (!SendKey(up: false, scan: false, current)) return;
                due = current.DownTick!.Value + 150; phase = 1; break;
            case 1:
                if (!SendKey(up: true, scan: false, current!)) return;
                due = Environment.TickCount64 + 400; phase = 2; break;
            case 2:
                current!.WindowEndTick = tick - 1;
                current = methods[1];
                if (current.Skipped) { StartQueueSuppression(tick); return; }
                if (!SendKey(up: false, scan: true, current)) return;
                due = current.DownTick!.Value + 150; phase = 3; break;
            case 3:
                if (!SendKey(up: true, scan: true, current!)) return;
                due = Environment.TickCount64 + 400; phase = 4; break;
            case 4:
                StartQueueSuppression(tick); break;
            case 5:
                if (!SendKey(up: true, scan: false, current!)) return;
                report.InjectionSequenceCompleted = methods[3].Skipped;
                due = Environment.TickCount64 + 400; phase = 6; break;
            case 6:
                current!.WindowEndTick = tick - 1;
                current = methods[3];
                if (current.Skipped) { report.ObservationCompleted = true; Finish(); return; }
                if (!SendKey(up: false, scan: true, current)) return;
                due = current.DownTick!.Value + 150; phase = 7; break;
            case 7:
                if (!SendKey(up: true, scan: true, current!)) return;
                report.InjectionSequenceCompleted = true;
                due = Environment.TickCount64 + 400; phase = 8; break;
            default:
                current!.WindowEndTick = tick; report.ObservationCompleted = true; Finish(); break;
        }
    }

    private void StartQueueSuppression(long tick)
    {
        if (current!.WindowStartTick.HasValue) current.WindowEndTick = tick - 1;
        var baseline = methods.Take(2).Where(method => !method.Skipped).ToArray();
        if (!baseline.All(method => method.DownAccepted && method.UpAccepted && report.HasBackgroundRawPair(method)))
        {
            Abort("Queue baseline did not establish matching background raw down/up pairs; no filtered input sent.");
            return;
        }
        if (!ConfigureOwnedQueue(suppress: true)) return;
        current = methods[2];
        if (!SendKey(up: false, scan: false, current)) return;
        due = current.DownTick!.Value + 150; phase = 5;
    }

    private bool SendKey(bool up, bool scan, MethodReport method, bool cleanup = false)
    {
        if (!up && (Native.GetAsyncKeyState(0x87) & 0x8000) != 0)
        {
            Abort("F24 was already down before a controlled press; no additional down sent.");
            return false;
        }
        uint mapping = scan ? method.MappedScanCode : 0;
        uint flags = (scan ? 8U : 0U) | (up ? 2U : 0U) | (scan && ((mapping >> 8) & 0xFF) == 0xE0 ? 1U : 0U);
        var packet = new Native.InputPacket
        {
            Type = 1,
            Value = new Native.InputUnion { Keyboard = new Native.KeyboardInput { VirtualKey = scan ? (ushort)0 : (ushort)0x87, Scan = (ushort)(mapping & 0xFF), Flags = flags, ExtraInfo = marker } }
        };
        var tick = Environment.TickCount64;
        // This ownership/focus guard is the last operation before global injection.
        // It does not remove the OS race between a focus check and SendInput.
        if (!GuardForeground()) { if (!cleanup) Abort("Foreground ownership guard rejected SendInput."); return false; }
        var sent = Native.SendInput(1, [packet], Marshal.SizeOf<Native.InputPacket>());
        if (!up) { method.WindowStartTick = tick; method.DownTick = tick; }
        else method.UpTick = tick;
        report.Injections.Add(new InjectionObservation { Tick = tick, Method = method.Method, Up = up, ScanCodeMode = scan, Flags = flags, ScanCode = (ushort)(mapping & 0xFF), Accepted = sent == 1, Error = sent == 1 ? 0 : Marshal.GetLastWin32Error(), Cleanup = cleanup, ForegroundHwnd = Native.Hex(sentinel), ForegroundPid = sentinelPid });
        if (sent != 1) { if (!cleanup) Abort("SendInput did not accept the controlled F24 event."); return false; }
        if (up) ownedDown = false;
        else { ownedDown = true; heldScan = scan; heldMakeCode = (ushort)(mapping & 0xFF); heldExtendedFlag = flags & 1U; }
        method.DownAccepted |= !up;
        method.UpAccepted |= up;
        return true;
    }

    private void Abort(string reason)
    {
        report.Aborted = true;
        report.AbortReason ??= reason;
        Finish();
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;
        if (receiver != nint.Zero) Native.KillTimer(receiver, 1);
        Native.PostQuitMessage(0);
    }

    public void Dispose()
    {
        watchdog?.Dispose();
        if (waitForFocusSeconds > 0 && OwnSentinelExists())
            report.SentinelTopmostRemoved |= Native.SetWindowPos(sentinel, new nint(-2), 0, 0, 0, 0, 0x13);
        if (ownedDown && current is not null)
        {
            // Cleanup can only release through the same still-owned sentinel.
            // No key-up is sent to an unrelated foreground application.
            if (OwnSentinelExists() && Native.GetForegroundWindow() != sentinel)
            {
                report.CleanupSentinelActivationAttempted = true;
                Native.SetForegroundWindow(sentinel);
            }
            current.MappedScanCode = heldScan ? (uint)heldMakeCode | (heldExtendedFlag != 0 ? 0xE000U : 0U) : 0;
            SendKey(up: true, heldScan, current, cleanup: true);
        }
        report.OwnedF24ReleasePending = ownedDown;
        if (ownedDown)
        {
            report.Aborted = true;
            report.AbortReason ??= "Owned F24 cleanup was not safely completed.";
            report.Errors.Add("Owned F24 key-up remains pending; no release was sent to an unrelated foreground app.");
        }
        if (legacyFilter is not null)
        {
            legacyFilter.Dispose();
            report.FilterHookRemoved = legacyFilter.Removed;
            report.FilterHookRemovalError = legacyFilter.RemovalError;
            report.FilterHookDownCount = legacyFilter.DownCount;
            report.FilterHookUpCount = legacyFilter.UpCount;
            report.FilterHookCallbackErrors = legacyFilter.CallbackErrors;
            report.FilterHookExpiredMatchCount = legacyFilter.ExpiredMatchCount;
            report.FilterHookMarkerMatchCount = legacyFilter.InjectedMarkerMatchCount;
            if (!report.FilterHookRemoved || report.FilterHookCallbackErrors != 0 || report.FilterHookExpiredMatchCount != 0)
            {
                report.Aborted = true;
                report.AbortReason ??= "Owned F24 filter failed its cleanup or bounded callback checks.";
                report.Errors.Add($"Owned F24 filter: removed={report.FilterHookRemoved}, removal error={report.FilterHookRemovalError}, callback errors={report.FilterHookCallbackErrors}, expired matches={report.FilterHookExpiredMatchCount}.");
            }
        }
        if (rawRegistered)
        {
            var remove = new[] { new Native.RawInputDevice { UsagePage = 1, Usage = 6, Flags = 1, Target = nint.Zero } };
            report.RawInputUnregistered = Native.RegisterRawInputDevices(remove, 1, (uint)Marshal.SizeOf<Native.RawInputDevice>());
        }
        // Restore only the captured HWND/PID identity. A reused handle is never activated.
        // A child might have taken focus even if readiness failed before its HWND arrived.
        var fixtureCurrentlyForeground = inject && child is not null && Native.WindowPid(Native.GetForegroundWindow()) == (uint)child.Id;
        if (fixtureCurrentlyForeground) focusTaken = true;
        report.FixtureTookForeground |= focusTaken;
        var initial = report.InitialForeground;
        if (fixtureCurrentlyForeground && initial is not null && initial.Handle != nint.Zero && Native.IsWindow(initial.Handle) && Native.WindowPid(initial.Handle) == initial.Pid)
        {
            report.ForegroundRestoreAttempted = true;
            Native.SetForegroundWindow(initial.Handle);
            report.ForegroundRestored = Native.GetForegroundWindow() == initial.Handle;
        }
        else report.ForegroundRestoreReason = !focusTaken ? "Fixture did not take foreground." : !fixtureCurrentlyForeground ? "Foreground changed away from the owned fixture; restoration skipped." : "Original HWND/PID no longer valid; restoration skipped.";
        try { if (writer is not null && pipe?.IsConnected == true) writer.WriteLine("finish " + session); }
        catch (IOException error) { report.Errors.Add("Sentinel finish request: " + error.Message); }
        if (childResult is not null && childResult.Wait(900))
        {
            var result = childResult.Result;
            if (result is not null)
            {
                report.SentinelLegacyF24 = result.LegacyF24;
                report.SentinelUnexpectedRawMessageCount = result.RawMessageCount;
                report.SentinelReportReceived = true;
                report.QueueReport = result.QueueReport;
                report.Errors.AddRange(result.QueueErrors.Select(error => "Owned queue filter: " + error));
            }
        }
        if (child is not null)
        {
            try
            {
                if (!child.WaitForExit(500)) { child.Kill(entireProcessTree: false); report.OwnedHelperForcedTermination = true; child.WaitForExit(300); }
                report.SentinelExitCode = child.HasExited ? child.ExitCode : null;
            }
            catch (InvalidOperationException error) { report.Errors.Add("Sentinel cleanup: " + error.Message); }
            child.Dispose();
        }
        if (childResult is not null && !report.SentinelReportReceived)
        {
            report.Aborted = true;
            report.AbortReason ??= "Owned sentinel observations were not received; zero legacy counts are inconclusive.";
            report.Errors.Add("Owned sentinel report missing; do not interpret zero legacy events as successful filtering.");
        }
        writer?.Dispose(); reader?.Dispose(); pipe?.Dispose();
        if (receiver != nint.Zero) Native.DestroyWindow(receiver);
        if (registration.Atom != 0) Native.UnregisterClass(registration.Name, registration.Instance);
        GC.KeepAlive(callback);
    }
}

internal static class Sentinel
{
    public static int Run(string[] args)
    {
        static string Required(string[] arguments, string key)
        {
            var index = Array.IndexOf(arguments, key);
            if (index < 0 || index + 1 == arguments.Length) throw new ArgumentException("Invalid owned sentinel arguments.");
            return arguments[index + 1];
        }
        try
        {
            var session = Required(args, "--session");
            if (session.Length != 32 || !Guid.TryParseExact(session, "N", out _)) return 2;
            var pipeName = Required(args, "--pipe");
            if (!pipeName.EndsWith(session, StringComparison.Ordinal)) return 2;
            var marker = uint.Parse(Required(args, "--marker"), System.Globalization.NumberStyles.HexNumber);
            var waitIndex = Array.IndexOf(args, "--wait-for-focus");
            var manualWait = waitIndex < 0 ? 0 : int.Parse(Required(args, "--wait-for-focus"));
            if (manualWait is < 0 or > 60) return 2;
            var queueMode = args.Contains("--filter-owned-queue", StringComparer.Ordinal);
            var messageMode = args.Contains("--filter-owned-message", StringComparer.Ordinal);
            if (queueMode && messageMode) return 2;
            using var fixture = new SentinelFixture(session, pipeName, marker, args.Contains("--activate", StringComparer.Ordinal), manualWait, queueMode, messageMode);
            return fixture.Run();
        }
        catch (Exception error) { Console.Error.WriteLine("Owned sentinel failed: " + error.Message); return 2; }
    }
}

internal sealed class SentinelFixture : IDisposable
{
    private readonly string session;
    private readonly string pipeName;
    private readonly uint marker;
    private readonly bool activate;
    private readonly int waitForFocusSeconds;
    private readonly Native.WindowProcedure callback;
    private readonly List<LegacyObservation> legacy = [];
    private readonly bool filterOwnedQueue;
    private readonly bool filterOwnedMessage;
    private readonly List<string> queueErrors = [];
    private IOwnedF24QueueFilter? queueFilter;
    private Native.WindowClass registration;
    private nint window;
    private int rawMessageCount;
    private NamedPipeClientStream? pipe;
    private StreamReader? reader;
    private StreamWriter? writer;
    private bool completed;
    private long startTick;

    public SentinelFixture(string session, string pipeName, uint marker, bool activate, int waitForFocusSeconds, bool filterOwnedQueue, bool filterOwnedMessage)
    {
        this.session = session; this.pipeName = pipeName; this.marker = marker; this.activate = activate; this.waitForFocusSeconds = waitForFocusSeconds;
        this.filterOwnedQueue = filterOwnedQueue;
        this.filterOwnedMessage = filterOwnedMessage;
        callback = WindowProcedure;
    }

    public int Run()
    {
        pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        pipe.Connect(1800);
        reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
        registration = Native.RegisterOwnedClass($"RawKeyboardProbe.Sentinel.{session}", callback);
        window = Native.CreateOwnedWindow(registration, "CONTROLLED F24 PROBE — owned foreground sentinel", visible: false);
        Native.CreateLabel(window, waitForFocusSeconds > 0
            ? $"Click this test window once to run the F24 probe; no game inputs.\r\nYou have {waitForFocusSeconds} seconds to click.\r\nThis is a controlled self-test, not a new bot input method.\r\nAfter clicking, keep focus here for the brief measurement."
            : "Controlled probe: F24 keys only, no game input.\r\nThis owned window may receive legacy key messages.\r\nThe separate background receiver records raw F24 only.\r\nCloses automatically; do not change focus during the short trial.");
        var visibleBeforeShow = Native.IsWindowVisible(window);
        if (waitForFocusSeconds > 0)
        {
            // Unlike a first ShowWindow call, SWP_NOACTIVATE is not overridden by STARTUPINFO.
            if (!Native.SetWindowPos(window, new nint(-1), 0, 0, 0, 0, 0x53)) throw Native.Error("SetWindowPos(owned manual-focus sentinel)");
        }
        else if (!activate)
        {
            if (!Native.SetWindowPos(window, nint.Zero, 0, 0, 0, 0, 0x57)) throw Native.Error("SetWindowPos(owned capture-only sentinel)");
        }
        else Native.ShowWindow(window, 5);
        var visibleAfterFirstShow = Native.IsWindowVisible(window);
        // A hidden STARTUPINFO can override the first ShowWindow command.
        // Only repeat for this owned fixture; the second call uses our explicit mode.
        if (!visibleAfterFirstShow && activate) Native.ShowWindow(window, 5);
        var visibleAfterShow = Native.IsWindowVisible(window);
        if (activate) Native.SetForegroundWindow(window);
        writer.WriteLine(JsonSerializer.Serialize(new SentinelReady
        {
            Type = "ready", Session = session, Pid = Environment.ProcessId, Hwnd = window.ToInt64().ToString(),
            VisibleBeforeShow = visibleBeforeShow, VisibleAfterFirstShow = visibleAfterFirstShow,
            SecondShowNeeded = !visibleAfterFirstShow && activate, VisibleAfterShow = visibleAfterShow, WindowRect = WindowRectSnapshot.Capture(window),
            ShowMethod = waitForFocusSeconds > 0 ? "SetWindowPos TOPMOST + NOACTIVATE + SHOWWINDOW" : activate ? "ShowWindow SW_SHOW" : "SetWindowPos NOACTIVATE + SHOWWINDOW"
        }));
        startTick = Environment.TickCount64;
        if (Native.SetTimer(window, 1, 25, nint.Zero) == 0) throw Native.Error("SetTimer(sentinel)");
        _ = Task.Run(async () =>
        {
            try { var command = await reader.ReadLineAsync(); if (command == "finish " + session || command is null) Native.PostMessage(window, Native.WmFinish, nint.Zero, nint.Zero); }
            catch (IOException) { Native.PostMessage(window, Native.WmFinish, nint.Zero, nint.Zero); }
        });
        Native.PumpMessages();
        return 0;
    }

    private nint WindowProcedure(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == Native.WmConfigureQueue && hwnd == window)
        {
            if ((!filterOwnedQueue && !filterOwnedMessage) || completed || wParam.ToInt64() != marker || Native.GetForegroundWindow() != window) return nint.Zero;
            try
            {
                var command = lParam.ToInt64();
                if (command == 0 && queueFilter is null)
                {
                    queueFilter = filterOwnedMessage ? new OwnedF24MessageFilter(marker, window) : new OwnedF24QueueFilter(marker, window);
                    return new nint(1);
                }
                var expectedPairs = (int)(command >> 8);
                if ((command is 0x101 or 0x201) && queueFilter is not null &&
                    legacy.Count(value => value.MatchesOwnedInjectionMarker && (value.Message is 0x100 or 0x104)) == expectedPairs &&
                    legacy.Count(value => value.MatchesOwnedInjectionMarker && (value.Message is 0x101 or 0x105)) == expectedPairs &&
                    (!filterOwnedMessage || HasFullBaselineMarkers(expectedPairs)) &&
                    queueFilter.EnableSuppression(expectedPairs)) return new nint(1);
                queueErrors.Add("Queue configuration or baseline verification was rejected.");
            }
            catch (Exception error) { queueErrors.Add(error.Message); }
            return nint.Zero;
        }
        if (message == Native.WmInput) rawMessageCount++;
        if (Native.IsKeyMessage(message) && wParam.ToInt64() == 0x87 && !completed)
        {
            var fullExtraInformation = unchecked((ulong)Native.GetMessageExtraInfo().ToInt64());
            var actualExtraInformation = unchecked((uint)fullExtraInformation);
            if (actualExtraInformation == marker)
            {
                var matches = !filterOwnedMessage || fullExtraInformation == marker;
                legacy.Add(new LegacyObservation { Tick = Environment.TickCount64, Message = message, VirtualKey = 0x87, LParam = lParam.ToInt64(), ExtraInformation = actualExtraInformation, FullExtraInformation = fullExtraInformation, MatchesOwnedInjectionMarker = matches });
                if (matches) return nint.Zero;
            }
        }
        if (message == Native.WmFinish || message == Native.WmClose || (message == Native.WmTimer && Environment.TickCount64 - startTick >= waitForFocusSeconds * 1000L + 8000)) { Complete(); return nint.Zero; }
        return Native.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private bool HasFullBaselineMarkers(int expectedPairs)
    {
        var snapshot = queueFilter?.Snapshot();
        if (snapshot is not { HookKind: "WH_GETMESSAGE", Installed: true, MarkerMismatchCount: 0, CallbackErrors: 0, ExpiredMatchCount: 0 }) return false;
        var samples = snapshot.MarkerSamples;
        return samples.Length == expectedPairs * 2 &&
            samples.All(sample => sample.Stage == "baseline" && sample.MatchesOwnedMarker &&
                Native.TryReadHexMarker(sample.ActualFullExtraInfo, out var actualMarker) && actualMarker == marker &&
                sample.Message is 0x100 or 0x101 or 0x104 or 0x105) &&
            samples.Count(sample => !sample.Up) == expectedPairs && samples.Count(sample => sample.Up) == expectedPairs;
    }

    private void Complete()
    {
        if (completed) return;
        completed = true;
        Native.KillTimer(window, 1);
        try { queueFilter?.Dispose(); }
        catch (Exception error) { queueErrors.Add("Queue cleanup: " + error.Message); }
        try { writer?.WriteLine(JsonSerializer.Serialize(new SentinelResult { Session = session, Pid = Environment.ProcessId, LegacyF24 = legacy, RawMessageCount = rawMessageCount, QueueReport = queueFilter?.Snapshot(), QueueErrors = queueErrors })); }
        catch (IOException) { }
        Native.PostQuitMessage(0);
    }

    public void Dispose()
    {
        Complete();
        writer?.Dispose(); reader?.Dispose(); pipe?.Dispose();
        if (window != nint.Zero) Native.DestroyWindow(window);
        if (registration.Atom != 0) Native.UnregisterClass(registration.Name, registration.Instance);
        GC.KeepAlive(callback);
    }
}

internal sealed class ProbeReport
{
    public string Mode { get; set; } = "";
    public string Scope { get; set; } = "Controlled owned-window raw-input feasibility only; no game messages, game hooks, driver, or setting changes.";
    public bool FilterOwnedLegacyRequested { get; set; }
    public bool FilterHookInstalled { get; set; }
    public bool FilterHookRemoved { get; set; }
    public int FilterHookRemovalError { get; set; }
    public string? FilterHookScope { get; set; }
    public int FilterHookDownCount { get; set; }
    public int FilterHookUpCount { get; set; }
    public int FilterHookCallbackErrors { get; set; }
    public int FilterHookExpiredMatchCount { get; set; }
    public int FilterHookMarkerMatchCount { get; set; }
    public bool FilterObservationValid { get; set; }
    public bool FilterOwnedQueueRequested { get; set; }
    public bool QueueInstallCommandAccepted { get; set; }
    public bool QueueSuppressionEnabled { get; set; }
    public OwnedF24QueueFilterSnapshot? QueueReport { get; set; }
    public bool QueueObservationValid { get; set; }
    public bool QueueFilteredDeliveryObserved { get; set; }
    public bool FilterOwnedMessageRequested { get; set; }
    public bool MessageObservationValid { get; set; }
    public bool MessageFilteredDeliveryObserved { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset FinishedUtc { get; set; }
    public bool Aborted { get; set; }
    public string? AbortReason { get; set; }
    public bool InjectionSequenceCompleted { get; set; }
    public bool ObservationCompleted { get; set; }
    public bool ForegroundChangedDuringFinalObservation { get; set; }
    public WindowSnapshot? InitialForeground { get; set; }
    public bool ForegroundAppKnown { get; set; }
    public int ManualFocusWaitSeconds { get; set; }
    public long ManualFocusElapsedMs { get; set; }
    public bool ManualFocusAchieved { get; set; }
    public bool SentinelTopmostRemoved { get; set; }
    public bool PreflightF24WasDown { get; set; }
    public int ReceiverPid { get; set; }
    public string? ReceiverHwnd { get; set; }
    public int SentinelPid { get; set; }
    public string? SentinelHwnd { get; set; }
    public bool SentinelOwnershipValidated { get; set; }
    public bool SentinelVisibleBeforeShow { get; set; }
    public bool SentinelVisibleAfterFirstShow { get; set; }
    public bool SentinelSecondShowNeeded { get; set; }
    public bool SentinelVisibleAfterShow { get; set; }
    public bool SentinelVisibleAtParentReady { get; set; }
    public WindowRectSnapshot? SentinelWindowRect { get; set; }
    public string? SentinelShowMethod { get; set; }
    public bool ReceiverIsSeparateBackgroundProcess { get; set; }
    public bool FixtureTookForeground { get; set; }
    public bool OwnedForegroundActivationHelperAttempted { get; set; }
    public bool OwnedForegroundActivationCallAccepted { get; set; }
    public bool ForegroundThreadAttachAttempted { get; set; }
    public bool ForegroundThreadAttachSucceeded { get; set; }
    public bool ForegroundThreadDetached { get; set; }
    public bool ForegroundRestoreAttempted { get; set; }
    public bool ForegroundRestored { get; set; }
    public string? ForegroundRestoreReason { get; set; }
    public bool CleanupSentinelActivationAttempted { get; set; }
    public bool RawInputRegistrationSucceeded { get; set; }
    public string? RawInputRegistrationFlags { get; set; }
    public bool RawInputUnregistered { get; set; }
    public long MeasurementStartedTick { get; set; }
    public long MeasurementEndedTick { get; set; }
    public string InjectionMarker { get; set; } = "";
    public uint ScanCodeMapping { get; set; }
    public int ForegroundGuardChecks { get; set; }
    public int ForegroundGuardFailures { get; set; }
    public bool OwnedF24ReleasePending { get; set; }
    public bool SentinelReportReceived { get; set; }
    public int? SentinelExitCode { get; set; }
    public bool OwnedHelperForcedTermination { get; set; }
    public int RawReadErrors { get; set; }
    public int ReceiverOwnedLegacyF24Count { get; set; }
    public int SentinelUnexpectedRawMessageCount { get; set; }
    public int SenderInjectedDownCount { get; set; }
    public int SenderInjectedUpCount { get; set; }
    public int ReceiverRawF24Count { get; set; }
    public int RawInputSinkCount { get; set; }
    public int RawInputForegroundCount { get; set; }
    public int RawZeroDeviceCount { get; set; }
    public int RawNonZeroDeviceCount { get; set; }
    public int RawMatchingInjectionMarkerCount { get; set; }
    public int SentinelLegacyDownCount { get; set; }
    public int SentinelLegacyUpCount { get; set; }
    public bool ForegroundLegacyLeakageObserved { get; set; }
    public string AttributionLimit { get; set; } = "Only raw F24 is retained. Extra-info and time windows aid attribution; a zero device handle alone does not prove synthetic origin. SendInput is global and does not target the receiver.";
    public List<MethodReport> Methods { get; set; } = [];
    public List<InjectionObservation> Injections { get; set; } = [];
    public List<RawObservation> RawF24 { get; set; } = [];
    public List<LegacyObservation> SentinelLegacyF24 { get; set; } = [];
    public List<string> Errors { get; set; } = [];

    public void PopulateCounters()
    {
        SenderInjectedDownCount = Injections.Count(value => value.Accepted && !value.Up);
        SenderInjectedUpCount = Injections.Count(value => value.Accepted && value.Up);
        ReceiverRawF24Count = RawF24.Count;
        RawInputSinkCount = RawF24.Count(value => value.WmInputCode == 1);
        RawInputForegroundCount = RawF24.Count(value => value.WmInputCode == 0);
        RawZeroDeviceCount = RawF24.Count(value => value.DeviceIsZero);
        RawNonZeroDeviceCount = RawF24.Count(value => !value.DeviceIsZero);
        RawMatchingInjectionMarkerCount = RawF24.Count(value => value.MatchesOwnedInjectionMarker);
        SentinelLegacyDownCount = SentinelLegacyF24.Count(value => value.MatchesOwnedInjectionMarker && (value.Message is 0x100 or 0x104));
        SentinelLegacyUpCount = SentinelLegacyF24.Count(value => value.MatchesOwnedInjectionMarker && (value.Message is 0x101 or 0x105));
        ForegroundLegacyLeakageObserved = SentinelLegacyF24.Count > 0;
        FilterObservationValid = FilterOwnedLegacyRequested && !Aborted && InjectionSequenceCompleted && ObservationCompleted &&
            ReceiverIsSeparateBackgroundProcess && SentinelOwnershipValidated && RawInputRegistrationSucceeded && RawInputUnregistered &&
            SentinelReportReceived && SentinelExitCode == 0 && !OwnedHelperForcedTermination && !OwnedF24ReleasePending &&
            RawReadErrors == 0 && Errors.Count == 0 && FilterHookInstalled && FilterHookRemoved &&
            FilterHookCallbackErrors == 0 && FilterHookExpiredMatchCount == 0 && SenderInjectedDownCount > 0 &&
            SenderInjectedDownCount == SenderInjectedUpCount && FilterHookDownCount == SenderInjectedDownCount &&
            FilterHookUpCount == SenderInjectedUpCount && FilterHookMarkerMatchCount == SenderInjectedDownCount + SenderInjectedUpCount;
        foreach (var method in Methods.Where(value => value.WindowStartTick.HasValue))
        {
            var end = method.WindowEndTick ?? MeasurementEndedTick;
            method.RawF24Count = RawF24.Count(value => value.Tick >= method.WindowStartTick && value.Tick <= end);
            method.RawMatchingMarkerCount = RawF24.Count(value => value.Tick >= method.WindowStartTick && value.Tick <= end && value.MatchesOwnedInjectionMarker);
            method.RawInputSinkCount = RawF24.Count(value => value.Tick >= method.WindowStartTick && value.Tick <= end && value.WmInputCode == 1);
            method.SentinelLegacyCount = SentinelLegacyF24.Count(value => value.MatchesOwnedInjectionMarker && value.Tick >= method.WindowStartTick && value.Tick <= end);
            method.ActualHoldMs = method.DownTick.HasValue && method.UpTick.HasValue ? method.UpTick - method.DownTick : null;
        }
        QueueObservationValid = FilterOwnedQueueRequested && HasPairedObservation("queue-", "WH_KEYBOARD", requireFullMarkers: false);
        QueueFilteredDeliveryObserved = QueueObservationValid && Methods.Where(method => !method.Skipped && method.Method.StartsWith("queue-filtered-", StringComparison.Ordinal)).All(HasBackgroundRawPair);
        MessageObservationValid = FilterOwnedMessageRequested && HasPairedObservation("message-", "WH_GETMESSAGE", requireFullMarkers: true);
        MessageFilteredDeliveryObserved = MessageObservationValid && Methods.Where(method => !method.Skipped && method.Method.StartsWith("message-filtered-", StringComparison.Ordinal)).All(HasBackgroundRawPair);
    }

    private bool HasPairedObservation(string prefix, string hookKind, bool requireFullMarkers)
    {
        var baseline = Methods.Where(method => !method.Skipped && method.Method.StartsWith(prefix + "baseline-", StringComparison.Ordinal)).ToArray();
        var filtered = Methods.Where(method => !method.Skipped && method.Method.StartsWith(prefix + "filtered-", StringComparison.Ordinal)).ToArray();
        var expectedPairs = baseline.Length;
        return !Aborted && InjectionSequenceCompleted && ObservationCompleted &&
            ReceiverIsSeparateBackgroundProcess && SentinelOwnershipValidated && RawInputRegistrationSucceeded && RawInputUnregistered &&
            SentinelReportReceived && SentinelExitCode == 0 && !OwnedHelperForcedTermination && !OwnedF24ReleasePending &&
            RawReadErrors == 0 && Errors.Count == 0 && QueueInstallCommandAccepted && QueueSuppressionEnabled &&
            QueueReport is { Installed: true, Removed: true, RemovalError: 0, BaselineVerified: true, MarkerMismatchCount: 0, CallbackErrors: 0, ExpiredMatchCount: 0 } &&
            QueueReport.HookKind == hookKind &&
            expectedPairs is 1 or 2 && filtered.Length == expectedPairs && SenderInjectedDownCount == expectedPairs * 2 && SenderInjectedUpCount == expectedPairs * 2 &&
            baseline.All(method => method.DownAccepted && method.UpAccepted && method.SentinelLegacyCount == 2 && HasBackgroundRawPair(method)) &&
            filtered.All(method => method.DownAccepted && method.UpAccepted && method.SentinelLegacyCount == 0) &&
            SentinelLegacyDownCount == expectedPairs && SentinelLegacyUpCount == expectedPairs &&
            QueueReport.BaselineDownCount == expectedPairs && QueueReport.BaselineUpCount == expectedPairs &&
            QueueReport.SuppressedDownCount == expectedPairs && QueueReport.SuppressedUpCount == expectedPairs &&
            (!requireFullMarkers || HasFullMessageMarkers(expectedPairs));
    }

    private bool HasFullMessageMarkers(int expectedPairs)
    {
        if (QueueReport is null || !Native.TryReadHexMarker(InjectionMarker, out var expectedMarker)) return false;
        var samples = QueueReport.MarkerSamples;
        return samples.Length == expectedPairs * 4 &&
            samples.All(sample => sample.MatchesOwnedMarker && Native.TryReadHexMarker(sample.ActualFullExtraInfo, out var actualMarker) && actualMarker == expectedMarker && sample.Message is 0x100 or 0x101 or 0x104 or 0x105) &&
            samples.Count(sample => sample.Stage == "baseline" && !sample.Up) == expectedPairs &&
            samples.Count(sample => sample.Stage == "baseline" && sample.Up) == expectedPairs &&
            samples.Count(sample => sample.Stage == "filtered" && !sample.Up) == expectedPairs &&
            samples.Count(sample => sample.Stage == "filtered" && sample.Up) == expectedPairs &&
            SentinelLegacyF24.Where(value => value.MatchesOwnedInjectionMarker).All(value => value.FullExtraInformation == expectedMarker);
    }

    internal bool HasBackgroundRawPair(MethodReport method)
    {
        if (!method.WindowStartTick.HasValue || !method.WindowEndTick.HasValue) return false;
        var packets = RawF24.Where(value => value.Tick >= method.WindowStartTick && value.Tick <= method.WindowEndTick &&
            value.MatchesOwnedInjectionMarker && value.WmInputCode == 1 && value.HeaderInputCode == 1 &&
            !value.ReceiverWindowIsForeground && !value.ReceiverProcessIsForeground && value.OwnedSentinelIsForeground).ToArray();
        return packets.Length == 2 && packets.Count(value => value.Message is 0x100 or 0x104) == 1 && packets.Count(value => value.Message is 0x101 or 0x105) == 1;
    }
}

internal sealed class WindowSnapshot
{
    [System.Text.Json.Serialization.JsonIgnore] public nint Handle { get; set; }
    public string Hwnd { get; set; } = "";
    public uint Pid { get; set; }
    public string? ProcessName { get; set; }
    public bool ProcessNameKnown { get; set; }
    public bool IsGame { get; set; }
    public static WindowSnapshot Capture()
    {
        var handle = Native.GetForegroundWindow();
        var result = new WindowSnapshot { Handle = handle, Hwnd = Native.Hex(handle), Pid = Native.WindowPid(handle) };
        if (result.Pid == 0) return result;
        try
        {
            using var process = Process.GetProcessById((int)result.Pid);
            result.ProcessName = process.ProcessName;
            result.ProcessNameKnown = !string.IsNullOrWhiteSpace(result.ProcessName);
            result.IsGame = result.ProcessName.Equals("KathanaGame", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        return result;
    }
}

internal sealed class MethodReport
{
    public string Method { get; set; } = "";
    public ushort VirtualKey { get; set; }
    public uint MappedScanCode { get; set; }
    public bool Skipped { get; set; }
    public string? SkipReason { get; set; }
    public long? WindowStartTick { get; set; }
    public long? WindowEndTick { get; set; }
    public long? DownTick { get; set; }
    public long? UpTick { get; set; }
    public long? ActualHoldMs { get; set; }
    public bool DownAccepted { get; set; }
    public bool UpAccepted { get; set; }
    public int RawF24Count { get; set; }
    public int RawMatchingMarkerCount { get; set; }
    public int RawInputSinkCount { get; set; }
    public int SentinelLegacyCount { get; set; }
}
internal sealed class InjectionObservation
{
    public long Tick { get; set; }
    public string Method { get; set; } = "";
    public bool Up { get; set; }
    public bool ScanCodeMode { get; set; }
    public uint Flags { get; set; }
    public ushort ScanCode { get; set; }
    public bool Accepted { get; set; }
    public int Error { get; set; }
    public bool Cleanup { get; set; }
    public string ForegroundHwnd { get; set; } = "";
    public uint ForegroundPid { get; set; }
}
internal sealed class RawObservation
{
    public long Tick { get; set; }
    public string DeviceHandle { get; set; } = "";
    public bool DeviceIsZero { get; set; }
    public ushort MakeCode { get; set; }
    public ushort Flags { get; set; }
    public ushort VirtualKey { get; set; }
    public uint Message { get; set; }
    public uint ExtraInformation { get; set; }
    public bool MatchesOwnedInjectionMarker { get; set; }
    public uint WmInputCode { get; set; }
    public uint HeaderInputCode { get; set; }
    public bool ReceiverWindowIsForeground { get; set; }
    public bool OwnedSentinelIsForeground { get; set; }
    public bool ReceiverProcessIsForeground { get; set; }
}
internal sealed class LegacyObservation
{
    public long Tick { get; set; }
    public uint Message { get; set; }
    public ushort VirtualKey { get; set; }
    public long LParam { get; set; }
    public uint ExtraInformation { get; set; }
    public ulong FullExtraInformation { get; set; }
    public bool MatchesOwnedInjectionMarker { get; set; }
}
internal sealed class SentinelReady
{
    public string Type { get; set; } = "";
    public string Session { get; set; } = "";
    public int Pid { get; set; }
    public string Hwnd { get; set; } = "";
    public bool VisibleBeforeShow { get; set; }
    public bool VisibleAfterFirstShow { get; set; }
    public bool SecondShowNeeded { get; set; }
    public bool VisibleAfterShow { get; set; }
    public WindowRectSnapshot? WindowRect { get; set; }
    public string ShowMethod { get; set; } = "";
}
internal sealed class WindowRectSnapshot
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public static WindowRectSnapshot? Capture(nint window) => Native.GetWindowRect(window, out var rect)
        ? new WindowRectSnapshot { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom } : null;
}
internal sealed class SentinelResult
{
    public string Session { get; set; } = "";
    public int Pid { get; set; }
    public List<LegacyObservation> LegacyF24 { get; set; } = [];
    public int RawMessageCount { get; set; }
    public OwnedF24QueueFilterSnapshot? QueueReport { get; set; }
    public List<string> QueueErrors { get; set; } = [];
}

internal static class Native
{
    internal static bool TryReadHexMarker(string text, out ulong marker)
    {
        var digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text.AsSpan(2) : text.AsSpan();
        return ulong.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out marker);
    }
    internal const uint WmInput = 0xFF, WmTimer = 0x113, WmClose = 0x10, WmFinish = 0x8001, WmWatchdog = 0x8002, WmConfigureQueue = 0x8003;
    internal const uint RidInput = 0x10000003, RidevInputSink = 0x100;
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate nint WindowProcedure(nint hwnd, uint message, nint wParam, nint lParam);
    internal readonly record struct WindowClass(string Name, ushort Atom, nint Instance);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClassEx
    {
        public uint Size, Style;
        public nint Procedure;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public string? MenuName;
        public string ClassName;
        public nint SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct RawInputDevice { public ushort UsagePage, Usage; public uint Flags; public nint Target; }
    [StructLayout(LayoutKind.Sequential)] internal struct RawInputHeader { public uint Type, Size; public nint Device; public nuint WParam; }
    [StructLayout(LayoutKind.Sequential)] internal struct RawKeyboard { public ushort MakeCode, Flags, Reserved, VirtualKey; public uint Message, ExtraInformation; }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput { public ushort VirtualKey, Scan; public uint Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] internal struct InputPacket { public uint Type; public InputUnion Value; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Message { public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public Point Point; public uint Private; }

    internal static string Hex(nint value) => $"0x{unchecked((ulong)value.ToInt64()):X}";
    internal static bool IsKeyMessage(uint value) => value is 0x100 or 0x101 or 0x104 or 0x105;
    internal static uint WindowPid(nint hwnd) { if (hwnd == nint.Zero) return 0; GetWindowThreadProcessId(hwnd, out var pid); return pid; }
    internal static uint WindowThread(nint hwnd) => hwnd == nint.Zero ? 0 : GetWindowThreadProcessId(hwnd, out _);
    internal static Exception Error(string operation) => new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), operation);
    internal static void ValidateLayouts()
    {
        if (Marshal.SizeOf<InputPacket>() != 40 || Marshal.SizeOf<RawInputHeader>() != 24 || Marshal.SizeOf<RawKeyboard>() != 16 || Marshal.SizeOf<RawInputDevice>() != 16)
            throw new InvalidOperationException("Unexpected x64 INPUT or Raw Input native structure layout.");
    }
    internal static WindowClass RegisterOwnedClass(string name, WindowProcedure callback)
    {
        var instance = GetModuleHandle(null);
        var registration = new WindowClassEx { Size = (uint)Marshal.SizeOf<WindowClassEx>(), Procedure = Marshal.GetFunctionPointerForDelegate(callback), Instance = instance, Cursor = LoadCursor(nint.Zero, new nint(32512)), Background = new nint(6), ClassName = name };
        var atom = RegisterClassEx(ref registration);
        if (atom == 0) throw Error("RegisterClassEx(owned fixture)");
        return new WindowClass(name, atom, instance);
    }
    internal static nint CreateOwnedWindow(WindowClass registration, string title, bool visible)
    {
        var hwnd = CreateWindowEx(0x80, registration.Name, title, 0x00C80000U | (visible ? 0x10000000U : 0U), 160, 120, 620, 175, nint.Zero, nint.Zero, registration.Instance, nint.Zero);
        if (hwnd == nint.Zero) throw Error("CreateWindowEx(owned fixture)");
        return hwnd;
    }
    internal static void CreateLabel(nint parent, string text)
    {
        if (CreateWindowEx(0, "STATIC", text, 0x50000000, 15, 14, 585, 125, parent, nint.Zero, GetModuleHandle(null), nint.Zero) == nint.Zero) throw Error("CreateWindowEx(owned label)");
    }
    internal static void PumpMessages()
    {
        while (true)
        {
            var status = GetMessage(out var message, nint.Zero, 0, 0);
            if (status == 0) break;
            if (status < 0) throw Error("GetMessage(owned fixture)");
            TranslateMessage(ref message); DispatchMessage(ref message);
        }
    }
    internal static bool PumpPendingMessages()
    {
        for (var count = 0; count < 64 && PeekMessage(out var message, nint.Zero, 0, 0, 1); count++)
        {
            if (message.Id == 0x12) return false;
            TranslateMessage(ref message); DispatchMessage(ref message);
        }
        return true;
    }
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClassEx registration);
    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", CharSet = CharSet.Unicode)] internal static extern bool UnregisterClass(string name, nint instance);
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint extended, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")] internal static extern nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] internal static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll", EntryPoint = "LoadCursorW")] private static extern nint LoadCursor(nint instance, nint name);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)] private static extern int GetMessage(out Message message, nint hwnd, uint first, uint last);
    [DllImport("user32.dll", EntryPoint = "PeekMessageW")] private static extern bool PeekMessage(out Message message, nint hwnd, uint first, uint last, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")] private static extern nint DispatchMessage(ref Message message);
    [DllImport("user32.dll")] internal static extern void PostQuitMessage(int code);
    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)] internal static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)] internal static extern nint SendMessageTimeout(nint hwnd, uint message, nint wParam, nint lParam, uint flags, uint timeout, out nuint result);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nuint SetTimer(nint hwnd, nuint id, uint interval, nint callback);
    [DllImport("user32.dll")] internal static extern bool KillTimer(nint hwnd, nuint id);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterRawInputDevices([In] RawInputDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint GetRawInputData(nint input, uint command, nint data, ref uint size, uint headerSize);
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")] internal static extern uint MapVirtualKey(uint key, uint mapType);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, [In] InputPacket[] packets, int size);
    [DllImport("user32.dll")] internal static extern nint GetMessageExtraInfo();
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool AttachThreadInput(uint callerThread, uint targetThread, bool attach);
}
