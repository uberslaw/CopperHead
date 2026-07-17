namespace CopperHead;

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
        Width = 80,
    };
    private readonly Button _refreshAdapters = new() { Text = "Refresh NICs", AutoSize = true };
    private readonly Button _start = new() { Text = "Start", AutoSize = true };
    private readonly Button _stop = new() { Text = "Stop", AutoSize = true, Enabled = false };
    private readonly Button _applyNow = new() { Text = "Apply now", AutoSize = true };
    private readonly Button _save = new() { Text = "Save config", AutoSize = true };
    private readonly TextBox _traceTarget = new() { Width = 220, PlaceholderText = "hostname or IP" };
    private readonly Button _tracert = new() { Text = "Tracert", AutoSize = true };
    private readonly Button _cancelTrace = new() { Text = "Cancel trace", AutoSize = true, Enabled = false };
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
    private readonly TraceRouteRunner _tracer;

    private CancellationTokenSource? _loopCts;
    private CancellationTokenSource? _refreshNowCts;
    private CancellationTokenSource? _traceCts;
    private Task? _loopTask;
    private bool _exitAfterStop;

    public MainForm()
    {
        _tracer = new TraceRouteRunner(_service.Routes);

        Text = "CopperHead";
        Width = 860;
        Height = 720;
        MinimumSize = new Size(720, 560);
        StartPosition = FormStartPosition.CenterScreen;

        _tray = new NotifyIcon
        {
            Text = "CopperHead",
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
            RowCount = 6,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 32f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 68f));
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
            Text = "Hostnames (one per line) — editable anytime; Apply now or wait for the next refresh",
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 4),
        };

        var hostsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 4) };
        hostsPanel.Controls.Add(_hosts);

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 4, 0, 4),
        };
        controls.Controls.Add(new Label { Text = "Refresh every (sec)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        controls.Controls.Add(_interval);
        controls.Controls.Add(_start);
        controls.Controls.Add(_stop);
        controls.Controls.Add(_applyNow);
        controls.Controls.Add(_save);
        controls.Controls.Add(_status);

        var traceRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 2, 0, 6),
        };
        traceRow.Controls.Add(new Label { Text = "Tracert target", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        traceRow.Controls.Add(_traceTarget);
        traceRow.Controls.Add(_tracert);
        traceRow.Controls.Add(_cancelTrace);
        traceRow.Controls.Add(new Label
        {
            Text = "(pins target via selected NIC, then streams tracert -d)",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Padding = new Padding(8, 6, 0, 0),
        });

        root.Controls.Add(adapterRow, 0, 0);

        var mid = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        mid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        mid.Controls.Add(hostsLabel, 0, 0);
        mid.Controls.Add(hostsPanel, 0, 1);
        root.Controls.Add(mid, 0, 1);

        root.Controls.Add(controls, 0, 2);
        root.Controls.Add(traceRow, 0, 3);

        var logPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        logPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        logPanel.Controls.Add(new Label { Text = "Log / tracert", AutoSize = true, Padding = new Padding(0, 4, 0, 2) }, 0, 0);
        logPanel.Controls.Add(_log, 0, 1);
        root.Controls.Add(logPanel, 0, 4);

        root.Controls.Add(new Label
        {
            Text = "CopperHead · Admin required · Only manages routes it creates · Stop clears them",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Padding = new Padding(0, 6, 0, 0),
        }, 0, 5);

        Controls.Add(root);

        _service.Log += msg =>
        {
            if (IsDisposed) return;
            BeginInvoke(() => _log.AppendText(msg + Environment.NewLine));
        };

        _refreshAdapters.Click += (_, _) => LoadAdapters();
        _start.Click += async (_, _) => await StartAsync();
        _stop.Click += async (_, _) => await StopAsync();
        _applyNow.Click += async (_, _) => await ApplyNowAsync();
        _save.Click += (_, _) => SaveConfig();
        _tracert.Click += async (_, _) => await RunTraceAsync();
        _cancelTrace.Click += (_, _) => _traceCts?.Cancel();

        Load += (_, _) =>
        {
            LoadAdapters();
            ApplyConfig(AppConfig.LoadOrDefault());
            AppendLog("CopperHead ready. Pick tether adapter, edit hostnames, Start. Use Tracert to verify path.");
        };

        FormClosing += async (_, e) =>
        {
            if ((_loopCts is not null || _traceCts is not null) && !_exitAfterStop)
            {
                e.Cancel = true;
                _exitAfterStop = true;
                _traceCts?.Cancel();
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
        if (!string.IsNullOrWhiteSpace(config.LastTraceTarget))
            _traceTarget.Text = config.LastTraceTarget;

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
            LastTraceTarget = _traceTarget.Text.Trim(),
        };
    }

    private void SaveConfig()
    {
        var cfg = CaptureConfig();
        cfg.Save();
        AppendLog($"Saved {AppConfig.DefaultPath}");
    }

    private async Task ApplyNowAsync()
    {
        if (_adapters.SelectedItem is not NetworkAdapterChoice adapter)
        {
            MessageBox.Show(this, "Select an egress adapter first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var hosts = CaptureConfig().Hostnames.Where(h => !h.StartsWith('#')).ToList();
        if (hosts.Count == 0)
        {
            MessageBox.Show(this, "Add at least one hostname.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _refreshNowCts?.Cancel();
        _refreshNowCts = new CancellationTokenSource();
        var token = _refreshNowCts.Token;
        _applyNow.Enabled = false;
        try
        {
            AppendLog("Applying hostname list now…");
            await _service.RefreshAsync(hosts, adapter, token).ConfigureAwait(true);
            SaveConfig();
        }
        catch (OperationCanceledException)
        {
            AppendLog("Apply cancelled.");
        }
        catch (Exception ex)
        {
            AppendLog("ERROR " + ex.Message);
        }
        finally
        {
            _applyNow.Enabled = true;
        }
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
        // Hostnames stay editable so you can add domains on the fly.
        _status.Text = "Running";
        _tray.Text = "CopperHead (running)";

        var token = _loopCts.Token;
        _loopTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                int seconds = 30;
                try
                {
                    NetworkAdapterChoice? adapter = null;
                    string[] hostList = [];
                    Invoke(() =>
                    {
                        LoadAdapters(preserveSelectionOnly: true);
                        adapter = _adapters.SelectedItem as NetworkAdapterChoice;
                        hostList = CaptureConfig().Hostnames.ToArray();
                        seconds = (int)_interval.Value;
                    });

                    if (adapter is null)
                        _service.WriteLog("WARN  selected adapter missing; waiting…");
                    else
                        await _service.RefreshAsync(hostList, adapter, token).ConfigureAwait(false);
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

        AppendLog("Started — edit hostnames anytime; Apply now for immediate update.");
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
        _refreshNowCts?.Cancel();
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
        _status.Text = "Stopped";
        _tray.Text = "CopperHead";
        AppendLog("Stopped.");
    }

    private async Task RunTraceAsync()
    {
        if (_adapters.SelectedItem is not NetworkAdapterChoice adapter)
        {
            MessageBox.Show(this, "Select an egress adapter first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var target = _traceTarget.Text.Trim();
        if (target.Length == 0)
        {
            // Convenience: use first hostname in the list
            target = CaptureConfig().Hostnames.FirstOrDefault(h => !h.StartsWith('#')) ?? "";
            _traceTarget.Text = target;
        }

        if (target.Length == 0)
        {
            MessageBox.Show(this, "Enter a hostname or IP to trace.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _traceCts?.Cancel();
        _traceCts = new CancellationTokenSource();
        var token = _traceCts.Token;

        _tracert.Enabled = false;
        _cancelTrace.Enabled = true;
        SaveConfig();

        try
        {
            await _tracer.RunAsync(
                target,
                adapter,
                line =>
                {
                    if (IsDisposed) return;
                    BeginInvoke(() => _log.AppendText($"{DateTime.Now:HH:mm:ss}  {line}{Environment.NewLine}"));
                },
                token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Tracert cancelled.");
        }
        catch (Exception ex)
        {
            AppendLog("TRACE ERROR " + ex.Message);
        }
        finally
        {
            _tracert.Enabled = true;
            _cancelTrace.Enabled = false;
        }
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
            _refreshNowCts?.Dispose();
            _traceCts?.Dispose();
        }
        base.Dispose(disposing);
    }
}
