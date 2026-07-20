namespace KathanaSecurePakBrowser;

internal sealed class TextEditorForm : Form
{
    private readonly TextBox editor;
    private readonly TextFileEncoding fileEncoding;

    public TextEditorForm(SecurePakEntry entry, string text, TextFileEncoding fileEncoding)
    {
        this.fileEncoding = fileEncoding;
        Text = $"Edit text — {entry.Path}";
        Width = 980;
        Height = 720;
        MinimumSize = new Size(640, 420);
        StartPosition = FormStartPosition.CenterParent;

        editor = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsTab = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font(FontFamily.GenericMonospace, 10),
            Text = text
        };

        Label encodingLabel = new()
        {
            AutoSize = true,
            Text = $"Encoding: {fileEncoding.DisplayName}. Saving preserves this encoding.",
            Padding = new Padding(8, 8, 8, 0)
        };
        Button saveButton = new() { Text = "Apply change", AutoSize = true };
        Button cancelButton = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        saveButton.Click += (_, _) =>
        {
            ResultContent = this.fileEncoding.Encode(editor.Text);
            DialogResult = DialogResult.OK;
            Close();
        };

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(saveButton);
        Controls.Add(editor);
        Controls.Add(encodingLabel);
        Controls.Add(buttons);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    public byte[]? ResultContent { get; private set; }
}
