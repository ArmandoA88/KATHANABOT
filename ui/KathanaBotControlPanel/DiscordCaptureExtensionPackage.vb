Imports System.IO
Imports System.Reflection
Imports System.Text.Json

' The Chrome/Edge capture extension is embedded in the app so it travels with the single EXE. Install extension...
' unpacks it to a stable folder that the browser loads as an unpacked extension.
Public NotInheritable Class DiscordCaptureExtensionPackage
    Private Const ResourcePrefix As String = "KathanaBotControlPanel.DiscordCapture."
    Private Shared ReadOnly ManagedExtensions As String() = {".mjs", ".html", ".css", ".json", ".md"}

    Private Sub New()
    End Sub

    Public Shared Function DefaultFolder() As String
        Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KathanaBot", "DiscordCaptureExtension")
    End Function

    Public Shared Function FileNames() As List(Of String)
        Return GetType(DiscordCaptureExtensionPackage).Assembly.GetManifestResourceNames().
            Where(Function(name) name.StartsWith(ResourcePrefix, StringComparison.Ordinal)).
            Select(Function(name) name.Substring(ResourcePrefix.Length)).OrderBy(Function(name) name, StringComparer.Ordinal).ToList()
    End Function

    Public Shared Function ReadFile(name As String) As Byte()
        Using stream = GetType(DiscordCaptureExtensionPackage).Assembly.GetManifestResourceStream(ResourcePrefix & name)
            If stream Is Nothing Then Throw New FileNotFoundException("The embedded extension file is missing: " & name)
            Using memory As New MemoryStream()
                stream.CopyTo(memory)
                Return memory.ToArray()
            End Using
        End Using
    End Function

    Public Shared Function Version() As String
        Using document = JsonDocument.Parse(ReadFile("manifest.json"))
            Return document.RootElement.GetProperty("version").GetString()
        End Using
    End Function

    Private Shared Function IsPlainFileName(name As String) As Boolean
        Return Not String.IsNullOrEmpty(name) AndAlso name = Path.GetFileName(name) AndAlso name <> "." AndAlso name <> ".." AndAlso
            name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
    End Function

    ' Writes every embedded extension file into the folder and returns how many were written. Files of an earlier
    ' extension version that are no longer part of the package are removed, but only when the folder holds this extension.
    Public Shared Function Extract(folder As String) As Integer
        If String.IsNullOrWhiteSpace(folder) Then Throw New ArgumentException("Choose a folder for the extension.", NameOf(folder))
        Dim full = Path.GetFullPath(folder)
        If String.Equals(Path.GetPathRoot(full), full, StringComparison.OrdinalIgnoreCase) Then Throw New ArgumentException("Choose a folder, not a drive root.", NameOf(folder))
        Dim names = FileNames()
        If Not names.Contains("manifest.json") Then Throw New InvalidOperationException("The extension is not embedded in this build.")
        Dim previouslyOurs = File.Exists(Path.Combine(full, "manifest.json")) AndAlso File.ReadAllText(Path.Combine(full, "manifest.json")).Contains("Kathana Discord Capture", StringComparison.Ordinal)
        Directory.CreateDirectory(full)
        For Each name In names
            If Not IsPlainFileName(name) Then Throw New InvalidOperationException("Unexpected embedded extension file name.")
            Dim target = Path.Combine(full, name)
            Dim temporary = target & ".tmp"
            File.WriteAllBytes(temporary, ReadFile(name))
            File.Move(temporary, target, True)
        Next
        If previouslyOurs Then
            For Each existing In Directory.GetFiles(full)
                Dim leaf = Path.GetFileName(existing)
                If ManagedExtensions.Contains(Path.GetExtension(leaf), StringComparer.OrdinalIgnoreCase) AndAlso Not names.Contains(leaf, StringComparer.Ordinal) Then File.Delete(existing)
            Next
        End If
        Return names.Count
    End Function
End Class
