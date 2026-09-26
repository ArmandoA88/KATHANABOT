using System.Text.Json;

// This is the launcher's own window ledger, not Chrome history/profile data.
// Window properties live with the window, so reused HWNDs cannot close another tab.
internal sealed class OwnedSessionWindows
{
    internal sealed record Entry(string Session, long Handle, int Marker);
    const string Property = "RemoteDesktopMosaic.OwnedSession";
    readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RemoteDesktopMosaic", "OwnedSessionWindows.json");

    internal static string SessionKey(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Host != "remotedesktop.google.com" || uri.UserInfo.Length != 0 || !uri.IsDefaultPort) return "";
        var match = System.Text.RegularExpressions.Regex.Match(uri.AbsolutePath, @"^/(?:u/\d+/)?access/session/([a-zA-Z0-9_-]+)/?$");
        return match.Success ? match.Groups[1].Value : "";
    }

    internal static bool CanClose(Entry previous, string session, nint replacement, bool chromeWindow, nint currentMarker, string title)
    {
        return session.Length > 0 && previous.Session == session && previous.Handle != (long)replacement &&
            previous.Marker > 0 && currentMarker == previous.Marker && chromeWindow &&
            (title.Equals("Chrome Remote Desktop", StringComparison.OrdinalIgnoreCase) ||
             title.EndsWith(" - Chrome Remote Desktop", StringComparison.OrdinalIgnoreCase) ||
             title.StartsWith("Chrome Remote Desktop - ", StringComparison.OrdinalIgnoreCase));
    }

    public void RecordAndCloseOlder(string link, nint replacement)
    {
        string session = SessionKey(link);
        if (session.Length == 0 || !Native.IsWindow(replacement)) return;
        List<Entry> entries;
        try { entries = File.Exists(path) ? JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path)) ?? [] : []; }
        catch { entries = []; } // Never guess ownership from a corrupt ledger or window title alone.
        int marker = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue);
        if (!Native.SetProp(replacement, Property, marker)) return;
        var chromeWindows = Native.ChromeWindows().Select(w => w.Handle).ToHashSet();
        var keep = new List<Entry>();
        foreach (var entry in entries)
        {
            nint source = (nint)entry.Handle;
            nint actualMarker = Native.GetProp(source, Property);
            if (!Native.IsWindow(source) || actualMarker != entry.Marker) continue;
            if (source == replacement) continue;
            if (CanClose(entry, session, replacement, chromeWindows.Contains(source), actualMarker, Native.Title(source)))
            {
                // Graceful close of a specifically tracked single-session app window.
                // Never terminate Chrome or close a general multi-tab browser window.
                Native.PostMessage(source, 0x0010, 0, 0);
            }
            keep.Add(entry); // Retain ownership if Chrome asks to confirm disconnection.
        }
        keep.Add(new Entry(session, (long)replacement, marker));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(keep));
        File.Move(temporary, path, true);
    }

    internal static void SelfTest()
    {
        var old = new Entry("computer-a", 100, 123);
        if (!CanClose(old, "computer-a", 200, true, 123, "Chrome Remote Desktop")) throw new Exception("Older matching session was not eligible for cleanup.");
        if (CanClose(old, "computer-b", 200, true, 123, "Chrome Remote Desktop")) throw new Exception("Different computer would be closed.");
        if (CanClose(old, "computer-a", 100, true, 123, "Chrome Remote Desktop")) throw new Exception("Replacement would close itself.");
        if (CanClose(old, "computer-a", 200, true, 456, "Chrome Remote Desktop")) throw new Exception("Reused window handle would be closed.");
        if (CanClose(old, "computer-a", 200, true, 123, "Email - Google Chrome")) throw new Exception("Unrelated browser window would be closed.");
        if (CanClose(old, "computer-a", 200, false, 123, "Chrome Remote Desktop")) throw new Exception("Non-Chrome window would be closed.");
        if (SessionKey("https://remotedesktop.google.com/u/0/access/session/computer-a?x=1") != "computer-a" ||
            SessionKey("https://example.com/access/session/computer-a") != "") throw new Exception("Session matching failed.");
    }
}
