using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace KathanaBotReloaded;

internal sealed record ReloadedRelease(Version Version, string ExeUrl, string HashUrl, string Notes);
internal static class ReloadedUpdater
{
    internal const string Repository = "ArmandoA88/KATHANABOT";
    internal const string Asset = "KathanaBotReloaded-win-x64-standalone.exe";
    internal static Version Current => new(typeof(Program).Assembly.GetName().Version!.ToString(3));
    private static HttpClient Client()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KathanaBotReloaded/" + Current);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
    internal static ReloadedRelease? ParseRelease(string json, Version current)
    {
        using var document = JsonDocument.Parse(json);
        var releases = new List<ReloadedRelease>();
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) continue;
            string tag = release.GetProperty("tag_name").GetString() ?? "";
            if (!tag.StartsWith("reloaded-v", StringComparison.Ordinal) || !Version.TryParse(tag[10..], out var version) || version <= current) continue;
            string exe = "", hash = "";
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                var url = asset.GetProperty("browser_download_url").GetString() ?? "";
                if (!url.StartsWith($"https://github.com/{Repository}/releases/download/{tag}/", StringComparison.Ordinal)) continue;
                if (name == Asset) exe = url;
                if (name == Asset + ".sha256") hash = url;
            }
            if (exe.Length > 0 && hash.Length > 0) releases.Add(new(version, exe, hash, release.TryGetProperty("body", out var body) ? body.GetString() ?? "" : ""));
        }
        return releases.OrderByDescending(r => r.Version).FirstOrDefault();
    }
    internal static async Task<ReloadedRelease?> Check()
    {
        using var client = Client();
        ReloadedRelease? newest = null;
        for (int page = 1; page <= 3; page++)
        {
            var json = await client.GetStringAsync($"https://api.github.com/repos/{Repository}/releases?per_page=100&page={page}");
            var release = ParseRelease(json, Current);
            if (release is not null && (newest is null || release.Version > newest.Version)) newest = release;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetArrayLength() < 100) break;
        }
        return newest;
    }
    internal static bool Verify(string file, string expected)
    {
        if (expected.Length != 64 || !expected.All(Uri.IsHexDigit)) return false;
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
    internal static async Task<(string File, string Hash)> Download(ReloadedRelease release)
    {
        using var client = Client();
        string checksum = (await client.GetStringAsync(release.HashUrl)).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (checksum.Length != 64 || !checksum.All(Uri.IsHexDigit)) throw new InvalidDataException("Release checksum is invalid.");
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KathanaBotReloaded", "updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, Asset);
        using (var response = await client.GetAsync(release.ExeUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = File.Create(path);
            await input.CopyToAsync(output);
        }
        if (!Verify(path, checksum)) throw new InvalidDataException("Downloaded EXE failed SHA-256 verification.");
        return (path, checksum);
    }
    internal static void StartInstaller(string file, string hash, string destination)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        info.ArgumentList.Add("--apply-update"); info.ArgumentList.Add(Environment.ProcessId.ToString());
        info.ArgumentList.Add(destination); info.ArgumentList.Add(hash);
        using var process = Process.Start(info) ?? throw new IOException("Could not launch the update installer.");
    }
    internal static void ReplaceVerified(string source, string destination, string hash)
    {
        if (!Verify(source, hash)) throw new InvalidDataException("Source checksum mismatch.");
        string staged = destination + ".update-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(source, staged);
            if (!Verify(staged, hash)) throw new IOException("Staged checksum mismatch.");
            File.Replace(staged, destination, destination + ".previous", true);
        }
        finally { if (File.Exists(staged)) File.Delete(staged); }
    }
    internal static int Apply(string[] args)
    {
        string? destination = null;
        string backup = "";
        bool replaced = false;
        try
        {
            if (args.Length != 4 || !int.TryParse(args[1], out int pid)) throw new InvalidDataException("Invalid update arguments.");
            string source = Environment.ProcessPath!;
            destination = Path.GetFullPath(args[2]);
            if (!destination.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || source.Equals(destination, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid update destination.");
            if (!Verify(source, args[3])) throw new InvalidDataException("Installer checksum mismatch.");
            try { using var parent = Process.GetProcessById(pid); if (!parent.WaitForExit(30000)) throw new IOException("Bot did not exit; update canceled."); }
            catch (ArgumentException) { }
            backup = destination + ".previous";
            for (int attempt = 0; ; attempt++)
            {
                try { ReplaceVerified(source, destination, args[3]); replaced = true; break; }
                catch (IOException) when (attempt < 20) { Thread.Sleep(500); }
            }
            if (!Verify(destination, args[3])) throw new IOException("Installed checksum mismatch.");
            using var restarted = Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true }) ?? throw new IOException("Could not restart the updated bot.");
            return 0;
        }
        catch (Exception ex)
        {
            if (replaced && destination is not null && File.Exists(backup))
            {
                try { File.Copy(backup, destination, true); } catch { }
            }
            var errorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KathanaBotReloaded", "update-error.txt");
            try { Directory.CreateDirectory(Path.GetDirectoryName(errorPath)!); File.WriteAllText(errorPath, ex.ToString()); } catch { }
            MessageBox.Show("Update failed. Your prior EXE was retained where possible.\n" + ex.Message + "\n" + errorPath, "Reloaded update");
            return 1;
        }
    }
    internal static void SelfTest()
    {
        string Release(string tag, bool checksum = true, bool draft = false) => JsonSerializer.Serialize(new[] { new {
            tag_name = tag, draft, prerelease = false, body = "Test",
            assets = new[] { new { name = Asset, browser_download_url = $"https://github.com/{Repository}/releases/download/{tag}/{Asset}" },
            new { name = checksum ? Asset + ".sha256" : "other", browser_download_url = $"https://github.com/{Repository}/releases/download/{tag}/{Asset}.sha256" } }
        }});
        if (ParseRelease(Release("v9.0.0"), new Version(1, 0, 0)) is not null ||
            ParseRelease(Release("reloaded-v2.0.0", false), new Version(1, 0, 0)) is not null ||
            ParseRelease(Release("reloaded-v2.0.0", true, true), new Version(1, 0, 0)) is not null ||
            ParseRelease(Release("reloaded-v2.0.0"), new Version(2, 0, 0)) is not null ||
            ParseRelease(Release("reloaded-v2.0.0"), new Version(1, 0, 0))?.Version != new Version(2, 0, 0))
            throw new Exception("Reloaded update channel filtering failed.");
        string temp = Path.GetTempFileName();
        string target = Path.GetTempFileName();
        try {
            File.WriteAllText(temp, "test");
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temp)));
            if (!Verify(temp, hash) || Verify(temp, new string('0', 64))) throw new Exception("Checksum verification failed");
            File.WriteAllText(target, "previous version");
            ReplaceVerified(temp, target, hash);
            if (File.ReadAllText(target) != "test" || File.ReadAllText(target + ".previous") != "previous version")
                throw new Exception("Atomic update or prior-version backup failed");
            File.AppendAllText(temp, "changed"); if (Verify(temp, hash)) throw new Exception("Changed download accepted");
            try { ReplaceVerified(temp, target, hash); throw new Exception("Corrupt update replaced EXE"); }
            catch (InvalidDataException) { }
            if (File.ReadAllText(target) != "test") throw new Exception("Corrupt update altered prior EXE");
        } finally { File.Delete(temp); File.Delete(target); File.Delete(target + ".previous"); }
    }
}

internal sealed partial class BotForm
{
    private readonly Label updateStatus = new() { AutoSize = true, MaximumSize = new Size(650, 0), Text = "Ready to check for Reloaded updates." };
    private readonly Button checkUpdate = new() { Text = "Check now", AutoSize = true };
    private readonly Button installUpdate = new() { Text = "Download, install and restart", AutoSize = true, Enabled = false };
    private readonly TextBox releaseNotes = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 450, Height = 180 };
    private ReloadedRelease? pendingRelease;
    private bool updateChecking, updateInstalling;
    private void BuildUpdateUi()
    {
        var page = new TabPage("Updates") { AutoScroll = true, Padding = new Padding(12) };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        panel.Controls.Add(new Label { Text = $"KATHANA BOT RELOADED {ReloadedUpdater.Current}", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        panel.Controls.Add(new Label { Text = "Source: github.com/" + ReloadedUpdater.Repository + " (reloaded-v releases)", AutoSize = true });
        var automatic = new CheckBox { Text = "Check automatically at startup", Checked = settings.AutoCheckUpdates, AutoSize = true };
        automatic.CheckedChanged += (_, _) => { settings.AutoCheckUpdates = automatic.Checked; SaveSettings(); };
        panel.Controls.Add(automatic);
        checkUpdate.Click += async (_, _) => await CheckUpdate(); panel.Controls.Add(checkUpdate);
        installUpdate.Click += async (_, _) => await InstallUpdate(); panel.Controls.Add(installUpdate);
        panel.Controls.Add(updateStatus); panel.Controls.Add(releaseNotes);
        panel.Controls.Add(new Label { Text = "Updates verify a SHA-256 checksum, stop the bot, retain the prior EXE as .previous, and restart. Results stay on this computer; use Home > Export results to create a shareable file.", AutoSize = true, MaximumSize = new Size(600, 0) });
        page.Controls.Add(panel); tabs.TabPages.Add(page);
    }
    private async Task CheckUpdate()
    {
        if (updateChecking || updateInstalling) return;
        updateChecking = true; checkUpdate.Enabled = false; installUpdate.Enabled = false;
        updateStatus.Text = "Checking for Reloaded releases...";
        try
        {
            pendingRelease = await ReloadedUpdater.Check();
            if (IsDisposed) return;
            updateStatus.Text = pendingRelease is null ? "No newer published Reloaded release is available." : $"Reloaded {pendingRelease.Version} is available.";
            releaseNotes.Text = pendingRelease?.Notes ?? "Only Reloaded releases with an EXE and checksum are accepted.";
        }
        catch (Exception ex) { if (!IsDisposed) updateStatus.Text = "Update check failed: " + ex.Message; }
        finally { updateChecking = false; if (!IsDisposed) { checkUpdate.Enabled = true; installUpdate.Enabled = pendingRelease is not null; } }
    }
    private async Task InstallUpdate()
    {
        if (pendingRelease is null || updateInstalling) return;
        updateInstalling = true; installUpdate.Enabled = checkUpdate.Enabled = false;
        Stop("Stopped for update");
        try
        {
            updateStatus.Text = "Downloading and verifying update...";
            var download = await ReloadedUpdater.Download(pendingRelease);
            if (IsDisposed) return;
            if (!ReleaseKey()) throw new IOException("Windows blocked key release; update canceled.");
            SaveSettings();
            ReloadedUpdater.StartInstaller(download.File, download.Hash, Environment.ProcessPath!);
            Close();
        }
        catch (Exception ex) { if (!IsDisposed) updateStatus.Text = "Update failed: " + ex.Message; }
        finally { updateInstalling = false; if (!IsDisposed) { checkUpdate.Enabled = true; installUpdate.Enabled = pendingRelease is not null; } }
    }
}
