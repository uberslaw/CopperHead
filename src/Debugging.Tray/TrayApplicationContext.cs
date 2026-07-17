using Debugging.Shared;

namespace Debugging.Tray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _statusItem;
    private LogViewerForm? _logViewer;
    private bool _monitoringEnabled = true;

    public TrayApplicationContext()
    {
        _toggleItem = new ToolStripMenuItem("Disable monitoring", null, OnToggleClicked);
        _statusItem = new ToolStripMenuItem("Status: starting...") { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("View log", null, OnViewLogClicked));
        menu.Items.Add(new ToolStripMenuItem("Open log file", null, OnOpenLogFileClicked));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit tray app", null, OnExitClicked));

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Visible = true,
            Text = "Debugging",
            ContextMenuStrip = menu,
        };

        _notifyIcon.DoubleClick += (_, _) => OnViewLogClicked(null, EventArgs.Empty);

        var timer = new System.Windows.Forms.Timer { Interval = 5000 };
        timer.Tick += async (_, _) => await RefreshStateAsync();
        timer.Start();

        _ = RefreshStateAsync();
    }

    private async Task RefreshStateAsync()
    {
        var response = await PipeClient.SendAsync(PipeMessages.GetState);
        if (response is null)
        {
            var local = AppState.Load();
            _monitoringEnabled = local.MonitoringEnabled;
            _statusItem.Text = "Status: service unreachable (using local state file)";
        }
        else if (response.StartsWith("STATE|", StringComparison.OrdinalIgnoreCase))
        {
            var parts = response.Split('|');
            _monitoringEnabled = parts.Length > 1 &&
                                 parts[1].Equals("enabled", StringComparison.OrdinalIgnoreCase);
            _statusItem.Text = _monitoringEnabled ? "Status: monitoring ON" : "Status: monitoring OFF";
        }

        _toggleItem.Text = _monitoringEnabled ? "Disable monitoring" : "Enable monitoring";
        _notifyIcon.Text = _monitoringEnabled ? "Debugging (ON)" : "Debugging (OFF)";
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        var command = _monitoringEnabled ? PipeMessages.Disable : PipeMessages.Enable;
        var response = await PipeClient.SendAsync(command);

        if (response is null)
        {
            var state = AppState.Load();
            state.MonitoringEnabled = !_monitoringEnabled;
            state.LastToggleUtc = DateTime.UtcNow;
            state.ToggledBy = "tray-offline";
            state.Save();
            EventLogWriter.Append(state.MonitoringEnabled
                ? "Monitoring enabled from tray (service offline, state file updated)."
                : "Monitoring disabled from tray (service offline, state file updated).");
        }

        await RefreshStateAsync();
    }

    private void OnViewLogClicked(object? sender, EventArgs e)
    {
        if (_logViewer is null || _logViewer.IsDisposed)
        {
            _logViewer = new LogViewerForm();
            _logViewer.FormClosed += (_, _) => _logViewer = null;
            _logViewer.Show();
            return;
        }

        _logViewer.BringToFront();
        _logViewer.Focus();
    }

    private void OnOpenLogFileClicked(object? sender, EventArgs e)
    {
        Directory.CreateDirectory(Paths.DataRoot);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Paths.EventLogFile,
            UseShellExecute = true,
        });
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        ExitThread();
    }
}
