using Debugging.Shared;

namespace Debugging.Tray;

public sealed class LogViewerForm : Form
{
    private readonly TextBox _logTextBox = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 3000 };

    public LogViewerForm()
    {
        Text = "Debugging Event Log";
        Width = 900;
        Height = 600;
        StartPosition = FormStartPosition.CenterScreen;

        _logTextBox.Multiline = true;
        _logTextBox.ReadOnly = true;
        _logTextBox.ScrollBars = ScrollBars.Both;
        _logTextBox.WordWrap = false;
        _logTextBox.Dock = DockStyle.Fill;
        _logTextBox.Font = new Font("Consolas", 9.5f);

        Controls.Add(_logTextBox);

        _refreshTimer.Tick += async (_, _) => await RefreshLogAsync();
        _refreshTimer.Start();

        Shown += async (_, _) => await RefreshLogAsync();
        FormClosed += (_, _) => _refreshTimer.Stop();
    }

    private async Task RefreshLogAsync()
    {
        var remote = await PipeClient.SendAsync(PipeMessages.RefreshLog);
        _logTextBox.Text = string.IsNullOrWhiteSpace(remote)
            ? EventLogWriter.ReadAll()
            : remote;
        _logTextBox.SelectionStart = _logTextBox.TextLength;
        _logTextBox.ScrollToCaret();
    }
}
