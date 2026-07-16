namespace HostRouteRefresher;

public sealed class MainForm : Form
{
    private readonly ComboBox _adapters = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox _hosts = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 10f),
        AcceptsReturn = true,
    };
    private readonly NumericUpDown _interval = new()
    {
        Minimum = 5,
        Maximum = 3600,
        Value = 30,
        Dock = DockStyle.Left,
        Width = 80,
    };
    private readonly Button _refreshAdapters = new() { Text = "Refresh NICs", AutoSize = true };
    private readonly Button _start = new() { Text = "Start", AutoSize = true };
    private readonly Button _stop = new() { Text = "Stop", AutoSize = true, Enabled = false };
    private readonly Button _save = new() { Text = "Save config", AutoSize = true };
    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 9f),
        WordWrap = false,
    };
    private readonly Label _status = new() { Text = "Stopped", AutoSize = true, Padding = new Padding(8, 8, 8, 8) };
    private readonly NotifyIcon _tray;
    private readonly HostRouteService _service = new();

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _exitAfterStop;

    public MainForm()
    {
        Text = "Host Route Refresher";
        Width = 780;
        Height = 640;
        MinimumSize = new Size(640, 480);
        StartPosition = FormStartPosition.CenterScreen;

        _tray = new NotifyIcon
        {
            Text = "Host Route Refresher",
            Visible = true,
            Icon = SystemIcons.Shield,
        };
        _tray.DoubleClick += (_, _) =>
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        };

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
        trayMenu.Items.Add("Stop & Exit", null, async (_, _) =>
        {
            await StopAsync();
            Close();
        });
        _tray.ContextMenuStrip = trayMenu;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 35f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 65f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var adapterRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        adapterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        adapterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        adapterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        adapterRow.Controls.Add(new Label { Text = "Egress adapter", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        adapterRow.Controls.Add(_adapters, 1, 0);
        adapterRow.Controls.Add(_refreshAdapters, 2, 0);

        var hostsLabel = new Label
        {
            Text = "Hostnames (one per line) — their current A records get /32 routes via the adapter above",
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 4),
        };

        var hostsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 4) };
        hostsPanel.Controls.Add(_hosts);

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 4),
        };
        controls.Controls.Add(new Label { Text = "Refresh every (sec)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        controls.Controls.Add(_interval);
        controls.Controls.Add(_start);
        controls.Controls.Add(_stop);
        controls.Controls.Add(_save);
        controls.Controls.Add(_status);

        var logLabel = new Label { Text = "Log", AutoSize = true, Padding = new Padding(0, 4, 0, 2) };

        root.Controls.Add(adapterRow, 0, 0);

        var mid = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        mid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        mid.Controls.Add(hostsLabel, 0, 0);
        mid.Controls.Add(hostsPanel, 0, 1);
        root.Controls.Add(mid, 0, 1);

        root.Controls.Add(controls, 0, 2);

        var logPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        logPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        logPanel.Controls.Add(logLabel, 0, 0);
        logPanel.Controls.Add(_log, 0, 1);
        root.Controls.Add(logPanel, 0, 3);

        root.Controls.Add(new Label
        {
            Text = "Requires Administrator. Only manages routes it creates. Stop clears them. No DLL injection.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Padding = new Padding(0, 6, 0, 0),
        }, 0, 4);

        Controls.Add(root);

        _service.Log += msg =>
        {
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                _log.AppendText(msg + Environment.NewLine);
            });
        };

        _refreshAdapters.Click += (_, _) => LoadAdapters();
        _start.Click += async (_, _) => await StartAsync();
        _stop.Click += async (_, _) => await StopAsync();
        _save.Click += (_, _) => SaveConfig();

        Load += (_, _) =>
        {
            LoadAdapters();
            ApplyConfig(AppConfig.LoadOrDefault());
            AppendLog("Ready. Pick your phone-tether adapter, add hostnames, Start.");
        };

        FormClosing += async (_, e) =>
        {
            if (_loopCts is not null && !_exitAfterStop)
            {
                e.Cancel = true;
                _exitAfterStop = true;
                await StopAsync();
                _tray.Visible = false;
                Close();
                return;
            }

            _tray.Visible = false;
        };

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                _tray.ShowBalloonTip(1500, Text, "Still running in the tray.", ToolTipIcon.Info);
            }
        };
    }

    private void LoadAdapters()
    {
        var selectedName = (_adapters.SelectedItem as NetworkAdapterChoice)?.Name;
        var items = NetworkAdapterEnumerator.GetChoices();
        _adapters.Items.Clear();
        foreach (var item in items)
            _adapters.Items.Add(item);

        if (_adapters.Items.Count == 0)
        {
            AppendLog("No adapters with an IPv4 gateway found. Enable tether and Refresh NICs.");
            return;
        }

        if (selectedName is not null)
        {
            for (var i = 0; i < _adapters.Items.Count; i++)
            {
                if (_adapters.Items[i] is NetworkAdapterChoice c &&
                    string.Equals(c.Name, selectedName, StringComparison.OrdinalIgnoreCase))
                {
                    _adapters.SelectedIndex = i;
                    return;
                }
            }
        }

        _adapters.SelectedIndex = 0;
    }

    private void ApplyConfig(AppConfig config)
    {
        _hosts.Text = string.Join(Environment.NewLine, config.Hostnames);
        _interval.Value = Math.Clamp(config.RefreshSeconds, 5, 3600);

        if (!string.IsNullOrWhiteSpace(config.AdapterName))
        {
            for (var i = 0; i < _adapters.Items.Count; i++)
            {
                if (_adapters.Items[i] is NetworkAdapterChoice c &&
                    string.Equals(c.Name, config.AdapterName, StringComparison.OrdinalIgnoreCase))
                {
                    _adapters.SelectedIndex = i;
                    break;
                }
            }
        }
    }

    private AppConfig CaptureConfig()
    {
        var adapter = _adapters.SelectedItem as NetworkAdapterChoice;
        return new AppConfig
        {
            Hostnames = _hosts.Lines
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList(),
            AdapterName = adapter?.Name,
            Gateway = adapter?.Gateway.ToString(),
            RefreshSeconds = (int)_interval.Value,
        };
    }

    private void SaveConfig()
    {
        var cfg = CaptureConfig();
        cfg.Save();
        AppendLog($"Saved {AppConfig.DefaultPath}");
    }

    private async Task StartAsync()
    {
        if (_loopCts is not null)
            return;

        if (_adapters.SelectedItem is not NetworkAdapterChoice)
        {
            MessageBox.Show(this, "Select an egress adapter (phone tether).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var hosts = CaptureConfig().Hostnames.Where(h => !h.StartsWith('#')).ToList();
        if (hosts.Count == 0)
        {
            MessageBox.Show(this, "Add at least one hostname.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SaveConfig();
        _loopCts = new CancellationTokenSource();
        _start.Enabled = false;
        _stop.Enabled = true;
        _hosts.ReadOnly = true;
        _adapters.Enabled = false;
        _interval.Enabled = false;
        _status.Text = "Running";
        _tray.Text = "Host Route Refresher (running)";

        var token = _loopCts.Token;
        var seconds = (int)_interval.Value;
        _loopTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Re-read adapter each cycle — tether IF/gateway can change.
                    NetworkAdapterChoice? adapter = null;
                    Invoke(() =>
                    {
                        LoadAdapters(preserveSelectionOnly: true);
                        adapter = _adapters.SelectedItem as NetworkAdapterChoice;
                    });

                    if (adapter is null)
                    {
                        _service.WriteLog("WARN  selected adapter missing; waiting…");
                    }
                    else
                    {
                        var hostList = Array.Empty<string>();
                        Invoke(() => hostList = CaptureConfig().Hostnames.ToArray());
                        await _service.RefreshAsync(hostList, adapter, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _service.WriteLog("ERROR " + ex.Message);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(seconds), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);

        AppendLog("Started.");
        await Task.CompletedTask;
    }

    private void LoadAdapters(bool preserveSelectionOnly)
    {
        if (!preserveSelectionOnly)
        {
            LoadAdapters();
            return;
        }

        var selectedName = (_adapters.SelectedItem as NetworkAdapterChoice)?.Name
                           ?? CaptureConfig().AdapterName;
        var gateway = (_adapters.SelectedItem as NetworkAdapterChoice)?.Gateway.ToString();

        var items = NetworkAdapterEnumerator.GetChoices();
        _adapters.Items.Clear();
        foreach (var item in items)
            _adapters.Items.Add(item);

        if (selectedName is null || _adapters.Items.Count == 0)
            return;

        for (var i = 0; i < _adapters.Items.Count; i++)
        {
            if (_adapters.Items[i] is NetworkAdapterChoice c &&
                string.Equals(c.Name, selectedName, StringComparison.OrdinalIgnoreCase) &&
                (gateway is null || c.Gateway.ToString() == gateway))
            {
                _adapters.SelectedIndex = i;
                return;
            }
        }

        for (var i = 0; i < _adapters.Items.Count; i++)
        {
            if (_adapters.Items[i] is NetworkAdapterChoice c &&
                string.Equals(c.Name, selectedName, StringComparison.OrdinalIgnoreCase))
            {
                _adapters.SelectedIndex = i;
                return;
            }
        }
    }

    private async Task StopAsync()
    {
        if (_loopCts is null)
            return;

        _loopCts.Cancel();
        try
        {
            if (_loopTask is not null)
                await _loopTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        try
        {
            _service.StopAndClear();
        }
        catch (Exception ex)
        {
            AppendLog("ERROR clearing routes: " + ex.Message);
        }

        _loopCts.Dispose();
        _loopCts = null;
        _loopTask = null;

        _start.Enabled = true;
        _stop.Enabled = false;
        _hosts.ReadOnly = false;
        _adapters.Enabled = true;
        _interval.Enabled = true;
        _status.Text = "Stopped";
        _tray.Text = "Host Route Refresher";
        AppendLog("Stopped.");
    }

    private void AppendLog(string message)
    {
        _log.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _loopCts?.Dispose();
        }
        base.Dispose(disposing);
    }
}
