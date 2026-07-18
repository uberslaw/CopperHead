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
    private readonly NumericUpDown _interval = new() { Minimum = 5, Maximum = 3600, Value = 30, Width = 80 };
    private readonly Button _refreshAdapters = new() { Text = "Refresh NICs", AutoSize = true };
    private readonly Button _start = new() { Text = "Start", AutoSize = true };
    private readonly Button _stop = new() { Text = "Stop", AutoSize = true, Enabled = false };
    private readonly Button _applyNow = new() { Text = "Apply now", AutoSize = true };
    private readonly Button _save = new() { Text = "Save config", AutoSize = true };
    private readonly TextBox _traceTarget = new() { Width = 220, PlaceholderText = "hostname or IP" };
    private readonly Button _tracert = new() { Text = "Tracert", AutoSize = true };
    private readonly Button _cancelTrace = new() { Text = "Stop tracert", AutoSize = true, Enabled = false };
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

    // Discover tab
    private readonly TextBox _watchProcesses = new() { Dock = DockStyle.Fill, PlaceholderText = "Cursor, MyLicenseApp" };
    private readonly Button _applyProcess = new() { Text = "Apply process", AutoSize = true };
    private readonly ListBox _newDiscoveries = new() { Dock = DockStyle.Fill, IntegralHeight = false, SelectionMode = SelectionMode.MultiExtended };
    private readonly ListBox _prevDiscoveries = new() { Dock = DockStyle.Fill, IntegralHeight = false, SelectionMode = SelectionMode.MultiExtended };
    private readonly Button _scanNow = new() { Text = "Scan now", AutoSize = true };
    private readonly Button _watchDiscover = new() { Text = "Watch", AutoSize = true };
    private readonly Button _stopWatchDiscover = new() { Text = "Stop watch", AutoSize = true, Enabled = false };
    private readonly Button _addSelected = new() { Text = "Add selected to hosts", AutoSize = true };
    private readonly Button _addAll = new() { Text = "Add all new to hosts", AutoSize = true };
    private readonly CheckBox _autoAdd = new() { Text = "Auto-add new discoveries", AutoSize = true };
    private readonly NumericUpDown _discoverInterval = new() { Minimum = 5, Maximum = 600, Value = 15, Width = 70 };
    private readonly TextBox _hostListUrl = new() { Dock = DockStyle.Fill, PlaceholderText = "https://raw.githubusercontent.com/.../hosts.txt" };
    private readonly Button _fetchList = new() { Text = "Fetch list", AutoSize = true };
    private readonly Label _discoverStatus = new() { Text = "Idle", AutoSize = true, Padding = new Padding(8, 6, 0, 0) };

    // Logs tab
    private readonly ListView _logSessions = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        HideSelection = false,
        MultiSelect = false,
    };
    private readonly Button _logsRefresh = new() { Text = "Refresh", AutoSize = true };
    private readonly Button _logsOpenHtml = new() { Text = "Open HTML report", AutoSize = true };
    private readonly Button _logsOpenFolder = new() { Text = "Open folder", AutoSize = true };
    private readonly WebBrowser _logBrowser = new() { Dock = DockStyle.Fill, ScriptErrorsSuppressed = true };

    // Traffic tab
    private readonly ListView _trafficList = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        MultiSelect = true,
        HideSelection = false,
    };
    private readonly Button _trafficStart = new() { Text = "Start monitor", AutoSize = true };
    private readonly Button _trafficStop = new() { Text = "Stop monitor", AutoSize = true, Enabled = false };
    private readonly Button _trafficPin = new() { Text = "Pin / Unpin", AutoSize = true };
    private readonly Button _trafficResetSession = new() { Text = "Reset session", AutoSize = true };
    private readonly Label _trafficStatus = new() { Text = "Idle", AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
    private readonly CheckBox _trafficActiveOnly = new() { Text = "Active only", AutoSize = true, Checked = true };

    private readonly NotifyIcon _tray;
    private readonly HostRouteService _service = new();
    private readonly TraceRouteRunner _tracer;
    private readonly TrafficMonitor _traffic;
    private readonly SessionLogStore _sessionLog = new();
    private readonly Dictionary<string, DiscoveredEndpoint> _discovered = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownBeforeSession = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _sessionNewKeys = new(StringComparer.OrdinalIgnoreCase);
    private List<TrafficRow> _trafficRows = [];
    private int _trafficSortColumn = 8; // All time TX
    private bool _trafficSortAsc; // false = descending
    private bool _couplingMonitors;
    private string _activeProcessKey = "";

    private CancellationTokenSource? _loopCts;
    private CancellationTokenSource? _refreshNowCts;
    private CancellationTokenSource? _traceCts;
    private CancellationTokenSource? _discoverCts;
    private CancellationTokenSource? _trafficCts;
    private Task? _loopTask;
    private Task? _discoverTask;
    private Task? _trafficTask;
    private bool _exitAfterStop;

    public MainForm()
    {
        _tracer = new TraceRouteRunner(_service.Routes);
        _traffic = new TrafficMonitor(new TrafficStatsStore());

        Text = "CopperHead";
        Width = 980;
        Height = 780;
        MinimumSize = new Size(800, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = AppIcons.AppIcon;

        _tray = new NotifyIcon
        {
            Text = "CopperHead",
            Visible = true,
            Icon = AppIcons.AppIcon,
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
            await StopBothMonitorsAsync();
            Close();
        });
        _tray.ContextMenuStrip = trayMenu;

        InitTrafficList();

        InitLogSessionsList();

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildRoutesTab());
        tabs.TabPages.Add(BuildDiscoverTab());
        tabs.TabPages.Add(BuildTrafficTab());
        tabs.TabPages.Add(BuildLogsTab());

        var footer = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            Text = "CopperHead · Admin required · TCP table / ESTATS (no injection) · Stop clears managed routes",
            ForeColor = Color.DimGray,
            Padding = new Padding(10, 6, 10, 0),
        };

        Controls.Add(tabs);
        Controls.Add(footer);

        _service.Log += msg =>
        {
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                _log.AppendText(msg + Environment.NewLine);
                try { _sessionLog.Write("route", msg); }
                catch { /* ignore */ }
            });
        };

        WireEvents();

        Load += (_, _) =>
        {
            LoadAdapters();
            ApplyConfig(AppConfig.LoadOrDefault());
            ApplyProcessContext(force: true);
            RefreshLogsList();
            AppendLog("CopperHead ready. Discover/Traffic start together; logs are per process under logs\\.");
        };

        FormClosing += async (_, e) =>
        {
            if ((_loopCts is not null || _traceCts is not null || _discoverCts is not null || _trafficCts is not null) && !_exitAfterStop)
            {
                e.Cancel = true;
                _exitAfterStop = true;
                _traceCts?.Cancel();
                await StopBothMonitorsAsync();
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

    private TabPage BuildRoutesTab()
    {
        var page = new TabPage("Routes");
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
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 65f));

        var adapterRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        adapterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        adapterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        adapterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        adapterRow.Controls.Add(new Label { Text = "Egress adapter", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        adapterRow.Controls.Add(_adapters, 1, 0);
        adapterRow.Controls.Add(_refreshAdapters, 2, 0);

        var mid = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        mid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        mid.Controls.Add(new Label
        {
            Text = "Hostnames / IPs (one per line) — editable anytime; Apply now or wait for refresh",
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 4),
        }, 0, 0);
        mid.Controls.Add(_hosts, 0, 1);

        var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 4, 0, 4) };
        controls.Controls.Add(new Label { Text = "Refresh every (sec)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        controls.Controls.Add(_interval);
        controls.Controls.Add(_start);
        controls.Controls.Add(_stop);
        controls.Controls.Add(_applyNow);
        controls.Controls.Add(_save);
        controls.Controls.Add(_status);

        var traceRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Padding = new Padding(0, 2, 0, 6) };
        traceRow.Controls.Add(new Label { Text = "Tracert target", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        traceRow.Controls.Add(_traceTarget);
        traceRow.Controls.Add(_tracert);
        traceRow.Controls.Add(_cancelTrace);

        var logPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        logPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        logPanel.Controls.Add(new Label { Text = "Log / tracert", AutoSize = true, Padding = new Padding(0, 4, 0, 2) }, 0, 0);
        logPanel.Controls.Add(_log, 0, 1);

        root.Controls.Add(adapterRow, 0, 0);
        root.Controls.Add(mid, 0, 1);
        root.Controls.Add(controls, 0, 2);
        root.Controls.Add(traceRow, 0, 3);
        root.Controls.Add(logPanel, 0, 4);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildDiscoverTab()
    {
        var page = new TabPage("Discover");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            Text = "Process to track (comma-separated OK). Apply process switches the log file. Watch also starts Traffic.",
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 4),
        }, 0, 0);

        var procRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        procRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        procRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        procRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        procRow.Controls.Add(new Label { Text = "Processes", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        procRow.Controls.Add(_watchProcesses, 1, 0);
        procRow.Controls.Add(_applyProcess, 2, 0);
        root.Controls.Add(procRow, 0, 1);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
        };
        // Avoid tiny panels before layout
        split.Panel1MinSize = 80;
        split.Panel2MinSize = 80;

        var newPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        newPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        newPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        newPanel.Controls.Add(new Label
        {
            Text = "Newly discovered",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 4),
        }, 0, 0);
        newPanel.Controls.Add(_newDiscoveries, 0, 1);

        var prevPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        prevPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        prevPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        prevPanel.Controls.Add(new Label
        {
            Text = "Previously discovered",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 4),
        }, 0, 0);
        prevPanel.Controls.Add(_prevDiscoveries, 0, 1);

        split.Panel1.Controls.Add(newPanel);
        split.Panel2.Controls.Add(prevPanel);
        root.Controls.Add(split, 0, 2);
        page.HandleCreated += (_, _) =>
        {
            try { split.SplitterDistance = Math.Max(120, split.Height / 2); }
            catch { /* ignore */ }
        };

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 6, 0, 4) };
        btnRow.Controls.Add(_scanNow);
        btnRow.Controls.Add(_watchDiscover);
        btnRow.Controls.Add(_stopWatchDiscover);
        btnRow.Controls.Add(new Label { Text = "every (sec)", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        btnRow.Controls.Add(_discoverInterval);
        btnRow.Controls.Add(_addSelected);
        btnRow.Controls.Add(_addAll);
        btnRow.Controls.Add(_autoAdd);
        btnRow.Controls.Add(_discoverStatus);
        root.Controls.Add(btnRow, 0, 3);

        root.Controls.Add(new Label
        {
            Text = "Optional: pull a shared hostname list from git (raw URL). Merges into the Routes list.",
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 4),
        }, 0, 4);

        var urlRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        urlRow.Controls.Add(new Label { Text = "Host list URL", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        urlRow.Controls.Add(_hostListUrl, 1, 0);
        urlRow.Controls.Add(_fetchList, 2, 0);
        root.Controls.Add(urlRow, 0, 5);

        page.Controls.Add(root);
        return page;
    }

    private void InitLogSessionsList()
    {
        _logSessions.Columns.Add("Process", 160);
        _logSessions.Columns.Add("Events", 70);
        _logSessions.Columns.Add("Known endpoints", 110);
        _logSessions.Columns.Add("Last activity", 160);
        _logSessions.SelectedIndexChanged += (_, _) => PreviewSelectedLog();
    }

    private TabPage BuildLogsTab()
    {
        var page = new TabPage("Logs");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 35f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 65f));

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        btnRow.Controls.Add(new Label
        {
            Text = "Per-process history (HTML report). Changing the tracked process switches the active log.",
            AutoSize = true,
            Padding = new Padding(0, 6, 12, 0),
        });
        btnRow.Controls.Add(_logsRefresh);
        btnRow.Controls.Add(_logsOpenHtml);
        btnRow.Controls.Add(_logsOpenFolder);
        root.Controls.Add(btnRow, 0, 0);
        root.Controls.Add(_logSessions, 0, 1);
        root.Controls.Add(_logBrowser, 0, 2);
        page.Controls.Add(root);
        return page;
    }

    private void InitTrafficList()
    {
        _trafficList.OwnerDraw = true;
        _trafficList.Columns.Add(CenterCol("★", 36));
        _trafficList.Columns.Add(CenterCol("IP", 120));
        _trafficList.Columns.Add(CenterCol("Port", 55));
        _trafficList.Columns.Add(CenterCol("Host", 140));
        _trafficList.Columns.Add(CenterCol("TX/s", 80));
        _trafficList.Columns.Add(CenterCol("RX/s", 80));
        _trafficList.Columns.Add(CenterCol("Session TX", 90));
        _trafficList.Columns.Add(CenterCol("Session RX", 90));
        _trafficList.Columns.Add(CenterCol("All time TX", 90));
        _trafficList.Columns.Add(CenterCol("All time RX", 90));

        _trafficList.DrawColumnHeader += (_, e) =>
        {
            e.DrawBackground();
            TextRenderer.DrawText(
                e.Graphics,
                e.Header!.Text,
                e.Font,
                e.Bounds,
                e.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawDefault = false;
        };
        _trafficList.DrawItem += (_, e) => { e.DrawDefault = false; };
        _trafficList.DrawSubItem += (_, e) =>
        {
            var selected = e.Item!.Selected;
            var bg = selected ? SystemColors.Highlight : e.Item.BackColor;
            var fg = selected ? SystemColors.HighlightText : e.Item.ForeColor;
            using (var brush = new SolidBrush(bg))
                e.Graphics!.FillRectangle(brush, e.Bounds);

            var font = e.Item.Font ?? _trafficList.Font;
            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem?.Text ?? "",
                font,
                e.Bounds,
                fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        };

        _trafficList.ColumnClick += (_, e) =>
        {
            if (_trafficSortColumn == e.Column)
                _trafficSortAsc = !_trafficSortAsc;
            else
            {
                _trafficSortColumn = e.Column;
                // Text columns start ascending; numeric/rates/totals start descending
                _trafficSortAsc = e.Column is 1 or 2 or 3;
            }
            RefreshTrafficList();
            SaveConfig();
        };
        _trafficList.DoubleClick += (_, _) => TogglePinSelected();
    }

    private static ColumnHeader CenterCol(string text, int width) =>
        new() { Text = text, Width = width, TextAlign = HorizontalAlignment.Center };

    private TabPage BuildTrafficTab()
    {
        var page = new TabPage("Traffic");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        root.Controls.Add(new Label
        {
            Text = "Live TCP byte rates for processes listed on Discover (ESTATS). Click headers to sort. Double-click or Pin to keep favourites on top.",
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 4),
        }, 0, 0);

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 2, 0, 6) };
        btnRow.Controls.Add(_trafficStart);
        btnRow.Controls.Add(_trafficStop);
        btnRow.Controls.Add(_trafficPin);
        btnRow.Controls.Add(_trafficResetSession);
        btnRow.Controls.Add(_trafficActiveOnly);
        btnRow.Controls.Add(_trafficStatus);
        root.Controls.Add(btnRow, 0, 1);
        root.Controls.Add(_trafficList, 0, 2);
        page.Controls.Add(root);
        return page;
    }

    private void WireEvents()
    {
        _refreshAdapters.Click += (_, _) => LoadAdapters();
        _start.Click += async (_, _) => await StartAsync();
        _stop.Click += async (_, _) => await StopAsync();
        _applyNow.Click += async (_, _) => await ApplyNowAsync();
        _save.Click += (_, _) => SaveConfig();
        _tracert.Click += async (_, _) => await RunTraceAsync();
        _cancelTrace.Click += (_, _) => _traceCts?.Cancel();
        _scanNow.Click += (_, _) => RunDiscovery(autoAdd: _autoAdd.Checked);
        _watchDiscover.Click += async (_, _) => await StartDiscoverWatchAsync();
        _stopWatchDiscover.Click += async (_, _) => await StopBothMonitorsAsync();
        _addSelected.Click += (_, _) => AddDiscoveriesToHosts(selectedOnly: true);
        _addAll.Click += (_, _) => AddDiscoveriesToHosts(selectedOnly: false, newOnly: true);
        _fetchList.Click += async (_, _) => await FetchHostListAsync();
        _applyProcess.Click += (_, _) =>
        {
            ApplyProcessContext(force: true);
            SaveConfig();
        };
        _watchProcesses.Leave += (_, _) => ApplyProcessContext(force: false);
        _trafficStart.Click += async (_, _) => await StartTrafficMonitorAsync();
        _trafficStop.Click += async (_, _) => await StopBothMonitorsAsync();
        _trafficPin.Click += (_, _) => TogglePinSelected();
        _trafficResetSession.Click += (_, _) =>
        {
            _traffic.ResetSession();
            RefreshTrafficList();
            AppendLog("TRAFFIC session counters reset.");
        };
        _trafficActiveOnly.CheckedChanged += (_, _) => RefreshTrafficList();
        _logsRefresh.Click += (_, _) => RefreshLogsList();
        _logsOpenHtml.Click += (_, _) => OpenSelectedLogHtml(external: true);
        _logsOpenFolder.Click += (_, _) => OpenSelectedLogFolder();
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
        _watchProcesses.Text = config.WatchProcesses ?? "Cursor";
        _hostListUrl.Text = config.HostListUrl ?? "";
        _autoAdd.Checked = config.AutoAddDiscoveries;
        _discoverInterval.Value = Math.Clamp(config.DiscoverSeconds <= 0 ? 15 : config.DiscoverSeconds, 5, 600);
        _traffic.SetPinned(config.PinnedTrafficKeys ?? []);
        // Migrate old combined-column indexes / default to All time TX desc
        _trafficSortColumn = config.TrafficSortColumn is >= 0 and <= 9
            ? config.TrafficSortColumn
            : 8;
        if (config.TrafficSortColumn is 6 or 7 && config.TrafficSortAsc == false &&
            config.PinnedTrafficKeys is not null)
        {
            // Older builds used 6/7 as combined session/all-time — prefer All time TX
            if (config.TrafficSortColumn == 7)
                _trafficSortColumn = 8;
        }
        _trafficSortAsc = config.TrafficSortAsc;

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
            Hostnames = _hosts.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList(),
            AdapterName = adapter?.Name,
            Gateway = adapter?.Gateway.ToString(),
            RefreshSeconds = (int)_interval.Value,
            LastTraceTarget = _traceTarget.Text.Trim(),
            WatchProcesses = _watchProcesses.Text.Trim(),
            HostListUrl = _hostListUrl.Text.Trim(),
            AutoAddDiscoveries = _autoAdd.Checked,
            DiscoverSeconds = (int)_discoverInterval.Value,
            PinnedTrafficKeys = _traffic.PinnedKeys.ToList(),
            TrafficSortColumn = _trafficSortColumn,
            TrafficSortAsc = _trafficSortAsc,
        };
    }

    private void SaveConfig()
    {
        CaptureConfig().Save();
        AppendLog($"Saved {AppConfig.DefaultPath}");
    }

    private void ApplyProcessContext(bool force)
    {
        var text = _watchProcesses.Text.Trim();
        var key = SessionLogStore.SanitizeKey(text);
        if (!force && string.Equals(key, _activeProcessKey, StringComparison.OrdinalIgnoreCase))
            return;

        // Persist known endpoints for the outgoing process
        if (_activeProcessKey.Length > 0)
            PersistKnownFromUi();

        _sessionLog.SetProcess(text.Length == 0 ? "unknown" : text);
        _activeProcessKey = _sessionLog.ProcessKey;

        _knownBeforeSession.Clear();
        foreach (var k in _sessionLog.LoadKnownEndpoints())
            _knownBeforeSession.Add(k);

        _sessionNewKeys.Clear();
        _discovered.Clear();
        // Seed previously discovered from known memory
        foreach (var k in _knownBeforeSession)
        {
            var isIp = System.Net.IPAddress.TryParse(k, out var ip);
            _discovered[k] = new DiscoveredEndpoint(
                "stored",
                0,
                isIp ? ip! : System.Net.IPAddress.Any,
                0,
                isIp ? null : k);
        }

        RefreshDiscoveryList();
        AppendLog($"PROCESS context → {_sessionLog.DisplayName} (log: logs\\{_sessionLog.ProcessKey}\\)");
        RefreshLogsList();
    }

    private void PersistKnownFromUi()
    {
        var all = _knownBeforeSession
            .Concat(_sessionNewKeys)
            .Concat(_discovered.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        _sessionLog.SaveKnownEndpoints(all);
    }

    private void RunDiscovery(bool autoAdd)
    {
        ApplyProcessContext(force: false);

        var names = _watchProcesses.Text
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length == 0)
        {
            MessageBox.Show(this, "Enter at least one process name (e.g. Cursor).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var found = ConnectionDiscovery.Scan(names);
            var brandNew = new List<string>();
            foreach (var item in found)
            {
                var key = item.DisplayKey;
                _discovered[key] = item;

                if (!_knownBeforeSession.Contains(key) && _sessionNewKeys.Add(key))
                {
                    brandNew.Add(key);
                    _sessionLog.Write("discover", $"New endpoint: {item}", new
                    {
                        ip = item.RemoteAddress.ToString(),
                        port = item.RemotePort,
                        host = item.Hostname,
                        process = item.ProcessName,
                    });
                }
            }

            RefreshDiscoveryList();
            _discoverStatus.Text = $"{_sessionNewKeys.Count} new / {_knownBeforeSession.Count} previous";
            AppendLog($"DISCOVER {found.Count} live connection(s), {brandNew.Count} newly discovered");

            if (autoAdd && brandNew.Count > 0)
            {
                foreach (var key in brandNew)
                    EnsureHostLine(key);
                AppendLog($"DISCOVER auto-added: {string.Join(", ", brandNew)}");
            }

            PersistKnownFromUi();
        }
        catch (Exception ex)
        {
            AppendLog("DISCOVER ERROR " + ex.Message);
        }
    }

    private void RefreshDiscoveryList()
    {
        var newItems = _discovered.Values
            .Where(v => _sessionNewKeys.Contains(v.DisplayKey))
            .OrderBy(v => v.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var prevItems = _discovered.Values
            .Where(v => _knownBeforeSession.Contains(v.DisplayKey) && !_sessionNewKeys.Contains(v.DisplayKey))
            .OrderBy(v => v.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Also show known keys that aren't in _discovered as plain strings via synthetic entries already seeded

        _newDiscoveries.BeginUpdate();
        _newDiscoveries.Items.Clear();
        foreach (var item in newItems)
            _newDiscoveries.Items.Add(item);
        _newDiscoveries.EndUpdate();

        _prevDiscoveries.BeginUpdate();
        _prevDiscoveries.Items.Clear();
        foreach (var item in prevItems)
            _prevDiscoveries.Items.Add(item);
        _prevDiscoveries.EndUpdate();
    }

    private void EnsureHostLine(string hostOrIp)
    {
        var existing = _hosts.Lines
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (existing.Contains(hostOrIp))
            return;
        if (_hosts.Text.Length > 0 && !_hosts.Text.EndsWith('\n'))
            _hosts.AppendText(Environment.NewLine);
        _hosts.AppendText(hostOrIp + Environment.NewLine);
    }

    private void AddDiscoveriesToHosts(bool selectedOnly, bool newOnly = false)
    {
        IEnumerable<DiscoveredEndpoint> items;
        if (selectedOnly)
        {
            items = _newDiscoveries.SelectedItems.Cast<DiscoveredEndpoint>()
                .Concat(_prevDiscoveries.SelectedItems.Cast<DiscoveredEndpoint>());
        }
        else if (newOnly)
        {
            items = _discovered.Values.Where(v => _sessionNewKeys.Contains(v.DisplayKey));
        }
        else
        {
            items = _discovered.Values;
        }

        var added = 0;
        foreach (var item in items)
        {
            var before = _hosts.Text;
            EnsureHostLine(item.DisplayKey);
            if (_hosts.Text != before)
                added++;
        }

        AppendLog(added == 0 ? "DISCOVER nothing new to add." : $"DISCOVER added {added} to host list.");
        SaveConfig();
    }

    private async Task StartDiscoverWatchAsync()
    {
        if (_discoverCts is not null)
        {
            await EnsurePairedTrafficAsync();
            return;
        }

        ApplyProcessContext(force: false);

        var names = _watchProcesses.Text
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length == 0)
        {
            MessageBox.Show(this, "Enter at least one process name (e.g. Cursor).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _discoverCts = new CancellationTokenSource();
        _watchDiscover.Enabled = false;
        _stopWatchDiscover.Enabled = true;
        _discoverStatus.Text = "Watching…";
        var token = _discoverCts.Token;

        _discoverTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                int seconds = 15;
                try
                {
                    Invoke(() =>
                    {
                        ApplyProcessContext(force: false);
                        RunDiscovery(autoAdd: _autoAdd.Checked);
                        seconds = (int)_discoverInterval.Value;
                    });
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    try { Invoke(() => AppendLog("DISCOVER ERROR " + ex.Message)); }
                    catch { /* ignore */ }
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

        AppendLog("DISCOVER watch started.");
        await EnsurePairedTrafficAsync();
    }

    private async Task StartTrafficMonitorAsync()
    {
        if (_trafficCts is not null)
        {
            await EnsurePairedDiscoverAsync();
            return;
        }

        ApplyProcessContext(force: false);

        var names = _watchProcesses.Text
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length == 0)
        {
            MessageBox.Show(this, "Set process names on the Discover tab first (e.g. Cursor).", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _trafficCts = new CancellationTokenSource();
        _trafficStart.Enabled = false;
        _trafficStop.Enabled = true;
        _trafficStatus.Text = "Monitoring…";
        var token = _trafficCts.Token;

        _trafficTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    string[] procs = [];
                    Invoke(() =>
                    {
                        ApplyProcessContext(force: false);
                        procs = _watchProcesses.Text
                            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    });

                    var rows = _traffic.Sample(procs);
                    Invoke(() =>
                    {
                        _trafficRows = rows.ToList();
                        RefreshTrafficList();
                        var active = rows.Count(r => r.Active);
                        _trafficStatus.Text = $"{active} active / {rows.Count} known";
                    });
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    try { Invoke(() => AppendLog("TRAFFIC ERROR " + ex.Message)); }
                    catch { /* ignore */ }
                }

                try
                {
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);

        AppendLog("TRAFFIC monitor started (1s samples).");
        await EnsurePairedDiscoverAsync();
    }

    private async Task EnsurePairedTrafficAsync()
    {
        if (_couplingMonitors || _trafficCts is not null) return;
        _couplingMonitors = true;
        try { await StartTrafficMonitorAsync(); }
        finally { _couplingMonitors = false; }
    }

    private async Task EnsurePairedDiscoverAsync()
    {
        if (_couplingMonitors || _discoverCts is not null) return;
        _couplingMonitors = true;
        try { await StartDiscoverWatchAsync(); }
        finally { _couplingMonitors = false; }
    }

    private async Task StopBothMonitorsAsync()
    {
        await StopDiscoverWatchAsync();
        await StopTrafficMonitorAsync();
    }

    private async Task StopDiscoverWatchAsync()
    {
        if (_discoverCts is null)
            return;

        _discoverCts.Cancel();
        try
        {
            if (_discoverTask is not null)
                await _discoverTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        PersistKnownFromUi();
        _discoverCts.Dispose();
        _discoverCts = null;
        _discoverTask = null;
        _watchDiscover.Enabled = true;
        _stopWatchDiscover.Enabled = false;
        _discoverStatus.Text = "Idle";
        AppendLog("DISCOVER watch stopped.");
    }

    private async Task StopTrafficMonitorAsync()
    {
        if (_trafficCts is null)
            return;

        _trafficCts.Cancel();
        try
        {
            if (_trafficTask is not null)
                await _trafficTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        _trafficCts.Dispose();
        _trafficCts = null;
        _trafficTask = null;
        _trafficStart.Enabled = true;
        _trafficStop.Enabled = false;
        _trafficStatus.Text = "Idle";
        SaveConfig();
        AppendLog("TRAFFIC monitor stopped.");
    }

    private void RefreshLogsList()
    {
        try
        {
            var selected = _logSessions.SelectedItems.Count > 0
                ? _logSessions.SelectedItems[0].Tag as string
                : null;

            _logSessions.BeginUpdate();
            _logSessions.Items.Clear();
            foreach (var info in SessionLogStore.ListProcessLogs())
            {
                var item = new ListViewItem(info.DisplayName) { Tag = info.ProcessKey };
                item.SubItems.Add(info.EventCount.ToString());
                item.SubItems.Add(info.KnownEndpointCount.ToString());
                item.SubItems.Add(info.LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
                _logSessions.Items.Add(item);
                if (string.Equals(selected, info.ProcessKey, StringComparison.OrdinalIgnoreCase) ||
                    (selected is null && string.Equals(info.ProcessKey, _sessionLog.ProcessKey, StringComparison.OrdinalIgnoreCase)))
                {
                    item.Selected = true;
                }
            }
            _logSessions.EndUpdate();
            if (_logSessions.SelectedItems.Count == 0 && _logSessions.Items.Count > 0)
                _logSessions.Items[0].Selected = true;
            else
                PreviewSelectedLog();
        }
        catch (Exception ex)
        {
            // Never block launch/UI on log IO
            try
            {
                _logBrowser.DocumentText =
                    $"<html><body style='font-family:Segoe UI;padding:16px'>Could not refresh logs: {System.Net.WebUtility.HtmlEncode(ex.Message)}</body></html>";
            }
            catch { /* ignore */ }
        }
    }

    private void PreviewSelectedLog()
    {
        if (_logSessions.SelectedItems.Count == 0)
        {
            _logBrowser.DocumentText = "<html><body style='font-family:Segoe UI;padding:16px;color:#666'>Select a process log.</body></html>";
            return;
        }

        var key = _logSessions.SelectedItems[0].Tag as string;
        if (key is null) return;
        try
        {
            _logBrowser.DocumentText = SessionLogStore.BuildHtmlReport(key);
        }
        catch (Exception ex)
        {
            _logBrowser.DocumentText = $"<html><body>Failed to render: {System.Net.WebUtility.HtmlEncode(ex.Message)}</body></html>";
        }
    }

    private void OpenSelectedLogHtml(bool external)
    {
        if (_logSessions.SelectedItems.Count == 0) return;
        var key = _logSessions.SelectedItems[0].Tag as string;
        if (key is null) return;
        try
        {
            var path = SessionLogStore.WriteHtmlReport(key);
            if (external)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            else
                _logBrowser.Navigate(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Open report", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenSelectedLogFolder()
    {
        if (_logSessions.SelectedItems.Count == 0) return;
        var key = _logSessions.SelectedItems[0].Tag as string;
        if (key is null) return;
        var dir = Path.Combine(SessionLogStore.LogsRoot, key);
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
    }

    private void TogglePinSelected()
    {
        if (_trafficList.SelectedItems.Count == 0)
            return;

        foreach (ListViewItem item in _trafficList.SelectedItems)
        {
            if (item.Tag is string key)
                _traffic.TogglePin(key);
        }

        // Update pinned flags on cached rows
        var pinned = _traffic.PinnedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _trafficRows)
            row.Pinned = pinned.Contains(row.Key);

        RefreshTrafficList();
        SaveConfig();
    }

    private void RefreshTrafficList()
    {
        IEnumerable<TrafficRow> rows = _trafficRows;
        if (_trafficActiveOnly.Checked)
            rows = rows.Where(r => r.Active || r.Pinned);

        var sorted = SortTraffic(rows).ToList();
        var selected = _trafficList.SelectedItems
            .Cast<ListViewItem>()
            .Select(i => i.Tag as string)
            .Where(k => k is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        _trafficList.BeginUpdate();
        _trafficList.Items.Clear();
        foreach (var row in sorted)
        {
            var item = new ListViewItem(row.Pinned ? "★" : "")
            {
                Tag = row.Key,
                ForeColor = row.Active ? SystemColors.WindowText : Color.Gray,
                UseItemStyleForSubItems = true,
            };
            item.SubItems.Add(row.Ip);
            item.SubItems.Add(row.Port.ToString());
            item.SubItems.Add(row.Hostname ?? "");
            item.SubItems.Add(FormatRate(row.TxPerSec));
            item.SubItems.Add(FormatRate(row.RxPerSec));
            item.SubItems.Add(FormatBytes(row.SessionTx));
            item.SubItems.Add(FormatBytes(row.SessionRx));
            item.SubItems.Add(FormatBytes(row.AllTimeTx));
            item.SubItems.Add(FormatBytes(row.AllTimeRx));
            if (row.Pinned)
                item.Font = new Font(_trafficList.Font, FontStyle.Bold);
            _trafficList.Items.Add(item);
            if (selected.Contains(row.Key))
                item.Selected = true;
        }
        _trafficList.EndUpdate();
    }

    private IEnumerable<TrafficRow> SortTraffic(IEnumerable<TrafficRow> rows)
    {
        IOrderedEnumerable<TrafficRow> ordered = rows
            .OrderByDescending(r => r.Pinned); // favourites always on top

        ordered = _trafficSortColumn switch
        {
            0 => _trafficSortAsc
                ? ordered.ThenBy(r => r.Pinned)
                : ordered.ThenByDescending(r => r.Pinned),
            1 => _trafficSortAsc
                ? ordered.ThenBy(r => r.Ip, StringComparer.OrdinalIgnoreCase)
                : ordered.ThenByDescending(r => r.Ip, StringComparer.OrdinalIgnoreCase),
            2 => _trafficSortAsc
                ? ordered.ThenBy(r => r.Port)
                : ordered.ThenByDescending(r => r.Port),
            3 => _trafficSortAsc
                ? ordered.ThenBy(r => r.Hostname ?? "", StringComparer.OrdinalIgnoreCase)
                : ordered.ThenByDescending(r => r.Hostname ?? "", StringComparer.OrdinalIgnoreCase),
            4 => _trafficSortAsc
                ? ordered.ThenBy(r => r.TxPerSec)
                : ordered.ThenByDescending(r => r.TxPerSec),
            5 => _trafficSortAsc
                ? ordered.ThenBy(r => r.RxPerSec)
                : ordered.ThenByDescending(r => r.RxPerSec),
            6 => _trafficSortAsc
                ? ordered.ThenBy(r => r.SessionTx)
                : ordered.ThenByDescending(r => r.SessionTx),
            7 => _trafficSortAsc
                ? ordered.ThenBy(r => r.SessionRx)
                : ordered.ThenByDescending(r => r.SessionRx),
            8 => _trafficSortAsc
                ? ordered.ThenBy(r => r.AllTimeTx)
                : ordered.ThenByDescending(r => r.AllTimeTx),
            9 => _trafficSortAsc
                ? ordered.ThenBy(r => r.AllTimeRx)
                : ordered.ThenByDescending(r => r.AllTimeRx),
            _ => ordered.ThenByDescending(r => r.AllTimeTx),
        };

        return ordered;
    }

    private static string FormatRate(double bytesPerSec)
    {
        if (bytesPerSec < 1) return "0";
        return FormatBytes((ulong)bytesPerSec) + "/s";
    }

    private static string FormatBytes(ulong bytes)
    {
        double v = bytes;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var u = 0;
        while (v >= 1024 && u < units.Length - 1)
        {
            v /= 1024;
            u++;
        }
        return u == 0 ? $"{bytes}{units[u]}" : $"{v:0.##}{units[u]}";
    }

    private async Task FetchHostListAsync()
    {
        var url = _hostListUrl.Text.Trim();
        if (url.Length == 0)
        {
            MessageBox.Show(this, "Enter a raw URL to a hosts.txt or hosts.json file.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _fetchList.Enabled = false;
        try
        {
            AppendLog("FETCH " + url);
            var incoming = await HostListSync.FetchAsync(url, CancellationToken.None).ConfigureAwait(true);
            var merged = HostListSync.Merge(CaptureConfig().Hostnames, incoming);
            _hosts.Text = string.Join(Environment.NewLine, merged);
            SaveConfig();
            AppendLog($"FETCH merged {incoming.Count} host(s); list now {merged.Count}.");
        }
        catch (Exception ex)
        {
            AppendLog("FETCH ERROR " + ex.Message);
            MessageBox.Show(this, ex.Message, "Fetch list failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _fetchList.Enabled = true;
        }
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
        try
        {
            // Infer a rough category from message prefix
            var cat = "app";
            if (message.StartsWith("DISCOVER", StringComparison.OrdinalIgnoreCase)) cat = "discover";
            else if (message.StartsWith("TRAFFIC", StringComparison.OrdinalIgnoreCase)) cat = "traffic";
            else if (message.StartsWith("TRACE", StringComparison.OrdinalIgnoreCase)) cat = "trace";
            else if (message.StartsWith("PROCESS", StringComparison.OrdinalIgnoreCase)) cat = "session";
            else if (message.StartsWith("FETCH", StringComparison.OrdinalIgnoreCase)) cat = "fetch";
            else if (message.StartsWith("ROUTE", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("ROUTE ", StringComparison.Ordinal)) cat = "route";
            _sessionLog.Write(cat, message);
        }
        catch
        {
            // logging must never break UI
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { PersistKnownFromUi(); } catch { /* ignore */ }
            _sessionLog.Close();
            _tray.Dispose();
            _loopCts?.Dispose();
            _refreshNowCts?.Dispose();
            _traceCts?.Dispose();
            _discoverCts?.Dispose();
            _trafficCts?.Dispose();
        }
        base.Dispose(disposing);
    }
}
