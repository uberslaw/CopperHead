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
    private readonly ListView _routesTable = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        MultiSelect = true,
        HideSelection = false,
    };
    private int _routesSortColumn; // Host
    private bool _routesSortAsc = true;
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

    // Processes tab
    private readonly TextBox _watchProcesses = new() { Dock = DockStyle.Fill, PlaceholderText = "Cursor, Cursor*" };
    private readonly Button _applyProcess = new() { Text = "Apply tracked", AutoSize = true };
    private readonly TextBox _processFilter = new() { Dock = DockStyle.Fill, PlaceholderText = "Filter by name or path…" };
    private readonly ListView _processList = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        MultiSelect = true,
        HideSelection = false,
    };
    private readonly Button _processDetectStart = new() { Text = "Start detect", AutoSize = true };
    private readonly Button _processDetectStop = new() { Text = "Stop detect", AutoSize = true, Enabled = false };
    private readonly Button _processRefreshOnce = new() { Text = "Refresh once", AutoSize = true };
    private readonly Button _processTrackSelected = new() { Text = "Track selected", AutoSize = true };
    private readonly Label _processDetectStatus = new() { Text = "Idle", AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
    private readonly Label _trackedBanner = new() { Text = "Tracking: (none)", AutoSize = true, Padding = new Padding(0, 4, 0, 4) };
    private List<RunningProcessInfo> _processSnapshot = [];
    private int _processSortColumn; // Name
    private bool _processSortAsc = true;

    // Discover tab
    private readonly ListView _newDiscoveries = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        MultiSelect = true,
        HideSelection = false,
    };
    private readonly ListView _prevDiscoveries = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        MultiSelect = true,
        HideSelection = false,
    };
    private readonly Button _scanNow = new() { Text = "Scan now", AutoSize = true };
    private readonly Button _watchDiscover = new() { Text = "Watch", AutoSize = true };
    private readonly Button _stopWatchDiscover = new() { Text = "Stop watch", AutoSize = true, Enabled = false };
    private readonly Button _addSelected = new() { Text = "Add selected to hosts", AutoSize = true };
    private readonly Button _addAll = new() { Text = "Add all new to hosts", AutoSize = true };
    private readonly CheckBox _autoAdd = new() { Text = "Auto-add new discoveries", AutoSize = true };
    private readonly CheckBox _includePrivate = new()
    {
        Text = "Include private/LAN (corp proxy)",
        AutoSize = true,
        Checked = true,
    };
    private readonly NumericUpDown _discoverInterval = new() { Minimum = 5, Maximum = 600, Value = 15, Width = 70 };
    private readonly TextBox _hostListUrl = new() { Dock = DockStyle.Fill, PlaceholderText = "https://raw.githubusercontent.com/.../hosts.txt" };
    private readonly Button _fetchList = new() { Text = "Fetch list", AutoSize = true };
    private readonly Label _discoverStatus = new() { Text = "Idle", AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
    private int _discoverSortColumn; // Host
    private bool _discoverSortAsc = true;

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
    private int _trafficSortColumn = 10; // All time TX (after Country/ASN cols)
    private bool _trafficSortAsc; // false = descending
    private bool _couplingMonitors;
    private string _activeProcessKey = "";

    private CancellationTokenSource? _loopCts;
    private CancellationTokenSource? _refreshNowCts;
    private CancellationTokenSource? _traceCts;
    private CancellationTokenSource? _discoverCts;
    private CancellationTokenSource? _trafficCts;
    private CancellationTokenSource? _processDetectCts;
    private Task? _loopTask;
    private Task? _discoverTask;
    private Task? _trafficTask;
    private Task? _processDetectTask;
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
            await StopProcessDetectAsync();
            await StopBothMonitorsAsync();
            await StopAsync();
            Close();
        });
        _tray.ContextMenuStrip = trayMenu;

        InitTrafficList();

        InitDiscoverLists();

        InitLogSessionsList();

        InitProcessList();

        var tabs = new TabControl { Dock = DockStyle.Fill };
        InitRoutesTable();

        tabs.TabPages.Add(BuildTrafficTab());
        tabs.TabPages.Add(BuildRoutesTab());
        tabs.TabPages.Add(BuildDiscoverTab());
        tabs.TabPages.Add(BuildLogsTab());
        tabs.TabPages.Add(BuildProcessesTab());

        var footer = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            Text = "CopperHead · Admin required · TCP table / ESTATS / Team Cymru geo (no injection) · Stop clears managed routes",
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
            RefreshProcessSnapshot();
            RefreshLogsList();
            _ = RefreshRoutesTableAsync();
            AppendLog("CopperHead ready. Traffic first; Processes on the right. Discover/Traffic start together.");
        };

        FormClosing += async (_, e) =>
        {
            if ((_loopCts is not null || _traceCts is not null || _discoverCts is not null || _trafficCts is not null || _processDetectCts is not null) && !_exitAfterStop)
            {
                e.Cancel = true;
                _exitAfterStop = true;
                _traceCts?.Cancel();
                await StopProcessDetectAsync();
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

    private void InitRoutesTable()
    {
        _routesTable.Columns.Add("Host", 160);
        _routesTable.Columns.Add("IP", 110);
        _routesTable.Columns.Add("Country", 70);
        _routesTable.Columns.Add("ASN", 200);
        _routesTable.Columns.Add("Routed", 60);
        _routesTable.ColumnClick += (_, e) =>
        {
            if (_routesSortColumn == e.Column)
                _routesSortAsc = !_routesSortAsc;
            else
            {
                _routesSortColumn = e.Column;
                _routesSortAsc = true;
            }
            // Re-sort currently displayed items in place
            var rows = _routesTable.Items.Cast<ListViewItem>()
                .Select(i => i.Tag as RouteTableRow)
                .Where(r => r is not null)
                .Select(r => r!)
                .ToList();
            FillRoutesTable(rows);
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
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));

        var adapterRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        adapterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        adapterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        adapterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        adapterRow.Controls.Add(new Label { Text = "Egress adapter", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        adapterRow.Controls.Add(_adapters, 1, 0);
        adapterRow.Controls.Add(_refreshAdapters, 2, 0);

        var mid = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        mid.RowStyles.Add(new RowStyle(SizeType.Percent, 40f));
        mid.RowStyles.Add(new RowStyle(SizeType.Percent, 60f));

        var hostsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        hostsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        hostsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        hostsPanel.Controls.Add(new Label
        {
            Text = "Hostnames / IPs (one per line) — editable anytime; Apply now or wait for refresh",
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 4),
        }, 0, 0);
        hostsPanel.Controls.Add(_hosts, 0, 1);

        var tablePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        tablePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tablePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        tablePanel.Controls.Add(new Label
        {
            Text = "Resolved routes — Country/ASN (Team Cymru). Click headers to sort.",
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 4),
        }, 0, 0);
        tablePanel.Controls.Add(_routesTable, 0, 1);

        mid.Controls.Add(hostsPanel, 0, 0);
        mid.Controls.Add(tablePanel, 0, 1);

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

    private void InitProcessList()
    {
        _processList.Columns.Add("Name", 140);
        _processList.Columns.Add("PID", 60);
        _processList.Columns.Add("Path", 420);
        _processList.Columns.Add("Company", 160);
        _processList.ColumnClick += (_, e) =>
        {
            if (_processSortColumn == e.Column)
                _processSortAsc = !_processSortAsc;
            else
            {
                _processSortColumn = e.Column;
                _processSortAsc = e.Column != 1; // PID defaults descending; text ascending
            }
            RefreshProcessListView();
        };
        _processList.DoubleClick += (_, _) => TrackSelectedProcesses();
    }

    private TabPage BuildProcessesTab()
    {
        var page = new TabPage("Processes");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        root.Controls.Add(new Label
        {
            Text = "Detect running processes, filter by name/path, then Track selected for Discover/Traffic/Logs.",
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 4),
        }, 0, 0);

        var trackedRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        trackedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        trackedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        trackedRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        trackedRow.Controls.Add(new Label { Text = "Tracked", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        trackedRow.Controls.Add(_watchProcesses, 1, 0);
        trackedRow.Controls.Add(_applyProcess, 2, 0);
        root.Controls.Add(trackedRow, 0, 1);

        var filterRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        filterRow.Controls.Add(new Label { Text = "Filter", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        filterRow.Controls.Add(_processFilter, 1, 0);
        root.Controls.Add(filterRow, 0, 2);

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 4, 0, 6) };
        btnRow.Controls.Add(_processDetectStart);
        btnRow.Controls.Add(_processDetectStop);
        btnRow.Controls.Add(_processRefreshOnce);
        btnRow.Controls.Add(_processTrackSelected);
        btnRow.Controls.Add(_processDetectStatus);
        root.Controls.Add(btnRow, 0, 3);

        root.Controls.Add(_processList, 0, 4);
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
            RowCount = 5,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(_trackedBanner, 0, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
        };
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
        root.Controls.Add(split, 0, 1);
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
        btnRow.Controls.Add(_includePrivate);
        btnRow.Controls.Add(_discoverStatus);
        root.Controls.Add(btnRow, 0, 2);

        root.Controls.Add(new Label
        {
            Text = "Tracked process is set on the Processes tab (e.g. Cursor or Cursor*). Office proxies often need Include private/LAN. Host list URL merges into Routes.",
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 4),
        }, 0, 3);

        var urlRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        urlRow.Controls.Add(new Label { Text = "Host list URL", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        urlRow.Controls.Add(_hostListUrl, 1, 0);
        urlRow.Controls.Add(_fetchList, 2, 0);
        root.Controls.Add(urlRow, 0, 4);

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

    private void InitDiscoverLists()
    {
        void addCols(ListView lv)
        {
            lv.Columns.Add("Host", 150);
            lv.Columns.Add("IP", 105);
            lv.Columns.Add("Port", 48);
            lv.Columns.Add("Country", 70);
            lv.Columns.Add("ASN", 180);
            lv.Columns.Add("Process", 90);
            lv.Columns.Add("PID", 55);
            lv.ColumnClick += (_, e) =>
            {
                if (_discoverSortColumn == e.Column)
                    _discoverSortAsc = !_discoverSortAsc;
                else
                {
                    _discoverSortColumn = e.Column;
                    _discoverSortAsc = e.Column is not 2 and not 6; // Port/PID default desc
                }
                RefreshDiscoveryList();
            };
        }

        addCols(_newDiscoveries);
        addCols(_prevDiscoveries);
    }

    private void InitTrafficList()
    {
        _trafficList.OwnerDraw = true;
        _trafficList.Columns.Add(CenterCol("★", 36));
        _trafficList.Columns.Add(CenterCol("IP", 105));
        _trafficList.Columns.Add(CenterCol("Port", 48));
        _trafficList.Columns.Add(CenterCol("Host", 120));
        _trafficList.Columns.Add(CenterCol("Country", 70));
        _trafficList.Columns.Add(CenterCol("ASN", 160));
        _trafficList.Columns.Add(CenterCol("TX/s", 75));
        _trafficList.Columns.Add(CenterCol("RX/s", 75));
        _trafficList.Columns.Add(CenterCol("Session TX", 85));
        _trafficList.Columns.Add(CenterCol("Session RX", 85));
        _trafficList.Columns.Add(CenterCol("All time TX", 85));
        _trafficList.Columns.Add(CenterCol("All time RX", 85));

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
                _trafficSortAsc = e.Column is 1 or 3 or 4 or 5;
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
            Text = "Live TCP byte rates for processes listed on Discover (ESTATS). Country/ASN via Team Cymru DNS. Click headers to sort. Double-click or Pin favourites.",
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
        _includePrivate.CheckedChanged += (_, _) => SaveConfig();
        _fetchList.Click += async (_, _) => await FetchHostListAsync();
        _applyProcess.Click += (_, _) =>
        {
            ApplyProcessContext(force: true);
            SaveConfig();
        };
        _watchProcesses.Leave += (_, _) => ApplyProcessContext(force: false);
        _processDetectStart.Click += async (_, _) => await StartProcessDetectAsync();
        _processDetectStop.Click += async (_, _) => await StopProcessDetectAsync();
        _processRefreshOnce.Click += (_, _) => RefreshProcessSnapshot();
        _processTrackSelected.Click += (_, _) => TrackSelectedProcesses();
        _processFilter.TextChanged += (_, _) => RefreshProcessListView();
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
        _includePrivate.Checked = config.IncludePrivateRemotes ?? true;
        _discoverInterval.Value = Math.Clamp(config.DiscoverSeconds <= 0 ? 15 : config.DiscoverSeconds, 5, 600);
        _traffic.SetPinned(config.PinnedTrafficKeys ?? []);
        var sortCol = config.TrafficSortColumn;
        // Pre-geo layout had 10 columns; Country/ASN inserted at indexes 4–5.
        if (config.TrafficColumnSchema < 1 && sortCol is >= 4 and <= 9)
            sortCol += 2;
        _trafficSortColumn = sortCol is >= 0 and <= 11 ? sortCol : 10;
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
            IncludePrivateRemotes = _includePrivate.Checked,
            DiscoverSeconds = (int)_discoverInterval.Value,
            PinnedTrafficKeys = _traffic.PinnedKeys.ToList(),
            TrafficSortColumn = _trafficSortColumn,
            TrafficSortAsc = _trafficSortAsc,
            TrafficColumnSchema = 1,
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
        _trackedBanner.Text = string.IsNullOrWhiteSpace(text)
            ? "Tracking: (none) — set on Processes tab"
            : $"Tracking: {text}  →  logs\\{_sessionLog.ProcessKey}\\";

        _knownBeforeSession.Clear();
        foreach (var k in _sessionLog.LoadKnownEndpoints())
            _knownBeforeSession.Add(k);

        _sessionNewKeys.Clear();
        _discovered.Clear();
        // Seed previously discovered from known memory
        var seedIps = new List<System.Net.IPAddress>();
        foreach (var k in _knownBeforeSession)
        {
            if (System.Net.IPAddress.TryParse(k, out var ip) &&
                ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                seedIps.Add(ip);
        }
        IpGeoLookup.LookupMany(seedIps);

        foreach (var k in _knownBeforeSession)
        {
            var isIp = System.Net.IPAddress.TryParse(k, out var ip);
            var geo = isIp ? IpGeoLookup.Lookup(ip!) : IpGeoInfo.Empty;
            _discovered[k] = new DiscoveredEndpoint(
                "stored",
                0,
                isIp ? ip! : System.Net.IPAddress.Any,
                0,
                isIp ? null : k,
                geo.CountryDisplay,
                geo.AsnDisplay);
        }

        RefreshDiscoveryList();
        AppendLog($"PROCESS context → {_sessionLog.DisplayName} (log: logs\\{_sessionLog.ProcessKey}\\)");
        RefreshLogsList();
    }

    private void RefreshProcessSnapshot()
    {
        try
        {
            _processSnapshot = ProcessEnumerator.Snapshot().ToList();
            RefreshProcessListView();
            _processDetectStatus.Text = $"{_processList.Items.Count} shown / {_processSnapshot.Count} running";
        }
        catch (Exception ex)
        {
            AppendLog("PROCESS ERROR " + ex.Message);
        }
    }

    private void RefreshProcessListView()
    {
        var filter = _processFilter.Text.Trim();
        IEnumerable<RunningProcessInfo> rows = _processSnapshot;
        if (filter.Length > 0)
        {
            rows = rows.Where(p =>
                p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Path.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Company.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Pid.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        rows = SortProcesses(rows);

        var tracked = GetTrackedNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedPids = _processList.SelectedItems
            .Cast<ListViewItem>()
            .Select(i => i.Tag as int?)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToHashSet();

        _processList.BeginUpdate();
        _processList.Items.Clear();
        foreach (var p in rows)
        {
            var pathDisplay = string.IsNullOrEmpty(p.Path) ? "(path unavailable)" : p.Path;
            var item = new ListViewItem(p.Name) { Tag = p.Pid };
            item.SubItems.Add(p.Pid.ToString());
            item.SubItems.Add(pathDisplay);
            item.SubItems.Add(p.Company);
            if (tracked.Contains(p.Name))
                item.Font = new Font(_processList.Font, FontStyle.Bold);
            _processList.Items.Add(item);
            if (selectedPids.Contains(p.Pid))
                item.Selected = true;
        }
        _processList.EndUpdate();
    }

    private IEnumerable<RunningProcessInfo> SortProcesses(IEnumerable<RunningProcessInfo> rows)
    {
        Func<RunningProcessInfo, IComparable> key = _processSortColumn switch
        {
            1 => p => p.Pid,
            2 => p => p.Path ?? "",
            3 => p => p.Company ?? "",
            _ => p => p.Name ?? "",
        };

        return _processSortAsc
            ? rows.OrderBy(key).ThenBy(p => p.Pid)
            : rows.OrderByDescending(key).ThenByDescending(p => p.Pid);
    }

    private string[] GetTrackedNames() =>
        _watchProcesses.Text
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void TrackSelectedProcesses()
    {
        if (_processList.SelectedItems.Count == 0)
        {
            MessageBox.Show(this, "Select one or more processes in the list.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var names = _processList.SelectedItems
            .Cast<ListViewItem>()
            .Select(i => i.Text.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _watchProcesses.Text = string.Join(", ", names);
        ApplyProcessContext(force: true);
        SaveConfig();
        RefreshProcessListView();
        AppendLog($"PROCESS tracking set from list: {_watchProcesses.Text}");
    }

    private async Task StartProcessDetectAsync()
    {
        if (_processDetectCts is not null)
            return;

        _processDetectCts = new CancellationTokenSource();
        _processDetectStart.Enabled = false;
        _processDetectStop.Enabled = true;
        _processDetectStatus.Text = "Detecting…";
        var token = _processDetectCts.Token;

        _processDetectTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var snap = ProcessEnumerator.Snapshot();
                    Invoke(() =>
                    {
                        _processSnapshot = snap.ToList();
                        RefreshProcessListView();
                        _processDetectStatus.Text = $"{_processList.Items.Count} shown / {_processSnapshot.Count} running";
                    });
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    try { Invoke(() => AppendLog("PROCESS ERROR " + ex.Message)); }
                    catch { /* ignore */ }
                }

                try
                {
                    await Task.Delay(2000, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);

        AppendLog("PROCESS detect started.");
        await Task.CompletedTask;
    }

    private async Task StopProcessDetectAsync()
    {
        if (_processDetectCts is null)
            return;

        _processDetectCts.Cancel();
        try
        {
            if (_processDetectTask is not null)
                await _processDetectTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        _processDetectCts.Dispose();
        _processDetectCts = null;
        _processDetectTask = null;
        _processDetectStart.Enabled = true;
        _processDetectStop.Enabled = false;
        _processDetectStatus.Text = "Idle";
        AppendLog("PROCESS detect stopped.");
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
            var scan = ConnectionDiscovery.Scan(names, includePrivateRemotes: _includePrivate.Checked);
            var found = scan.Endpoints;
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
                        country = item.CountryCode,
                        asn = item.AsnLabel,
                        process = item.ProcessName,
                    });
                }
            }

            RefreshDiscoveryList();
            _discoverStatus.Text = $"{_sessionNewKeys.Count} new / {_knownBeforeSession.Count} previous";
            AppendLog($"DISCOVER {found.Count} live · {brandNew.Count} new · {scan.Detail}");

            if (found.Count == 0 && scan.SkippedPrivate > 0 && !_includePrivate.Checked)
                AppendLog("DISCOVER tip: enable Include private/LAN — office proxies often only show private remotes.");

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
        var newItems = SortDiscoveries(
            _discovered.Values.Where(v => _sessionNewKeys.Contains(v.DisplayKey))).ToList();

        var prevItems = SortDiscoveries(
            _discovered.Values.Where(v =>
                _knownBeforeSession.Contains(v.DisplayKey) && !_sessionNewKeys.Contains(v.DisplayKey))).ToList();

        FillDiscoveryList(_newDiscoveries, newItems);
        FillDiscoveryList(_prevDiscoveries, prevItems);
    }

    private static void FillDiscoveryList(ListView lv, IEnumerable<DiscoveredEndpoint> items)
    {
        var selected = lv.SelectedItems
            .Cast<ListViewItem>()
            .Select(i => (i.Tag as DiscoveredEndpoint)?.DisplayKey)
            .Where(k => k is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        lv.BeginUpdate();
        lv.Items.Clear();
        foreach (var ep in items)
        {
            var host = ep.Hostname ?? "";
            var ipText = ep.RemoteAddress.Equals(System.Net.IPAddress.Any)
                ? ""
                : ep.RemoteAddress.ToString();
            var item = new ListViewItem(host.Length > 0 ? host : ipText) { Tag = ep };
            item.SubItems.Add(ipText);
            item.SubItems.Add(ep.RemotePort > 0 ? ep.RemotePort.ToString() : "");
            item.SubItems.Add(ep.CountryCode);
            item.SubItems.Add(ep.AsnLabel);
            item.SubItems.Add(ep.ProcessId == 0 ? "" : ep.ProcessName);
            item.SubItems.Add(ep.ProcessId > 0 ? ep.ProcessId.ToString() : "");
            if (ep.ProcessId == 0)
                item.ForeColor = Color.Gray;
            lv.Items.Add(item);
            if (selected.Contains(ep.DisplayKey))
                item.Selected = true;
        }
        lv.EndUpdate();
    }

    private IEnumerable<DiscoveredEndpoint> SortDiscoveries(IEnumerable<DiscoveredEndpoint> rows)
    {
        Func<DiscoveredEndpoint, IComparable> key = _discoverSortColumn switch
        {
            0 => r => r.Hostname ?? r.RemoteAddress.ToString(),
            1 => r => r.RemoteAddress.ToString(),
            2 => r => r.RemotePort,
            3 => r => r.CountryCode,
            4 => r => r.AsnLabel,
            5 => r => r.ProcessName,
            6 => r => r.ProcessId,
            _ => r => r.Hostname ?? r.RemoteAddress.ToString(),
        };

        return _discoverSortAsc
            ? rows.OrderBy(key)
            : rows.OrderByDescending(key);
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
            items = _newDiscoveries.SelectedItems.Cast<ListViewItem>()
                .Concat(_prevDiscoveries.SelectedItems.Cast<ListViewItem>())
                .Select(i => i.Tag as DiscoveredEndpoint)
                .Where(e => e is not null)
                .Select(e => e!);
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
                    var includePrivate = true;
                    Invoke(() =>
                    {
                        ApplyProcessContext(force: false);
                        procs = _watchProcesses.Text
                            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        includePrivate = _includePrivate.Checked;
                    });

                    var rows = _traffic.Sample(procs, includePrivateRemotes: includePrivate);
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
            item.SubItems.Add(row.CountryCode);
            item.SubItems.Add(row.AsnLabel);
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
                ? ordered.ThenBy(r => r.CountryCode, StringComparer.OrdinalIgnoreCase)
                : ordered.ThenByDescending(r => r.CountryCode, StringComparer.OrdinalIgnoreCase),
            5 => _trafficSortAsc
                ? ordered.ThenBy(r => r.AsnLabel, StringComparer.OrdinalIgnoreCase)
                : ordered.ThenByDescending(r => r.AsnLabel, StringComparer.OrdinalIgnoreCase),
            6 => _trafficSortAsc
                ? ordered.ThenBy(r => r.TxPerSec)
                : ordered.ThenByDescending(r => r.TxPerSec),
            7 => _trafficSortAsc
                ? ordered.ThenBy(r => r.RxPerSec)
                : ordered.ThenByDescending(r => r.RxPerSec),
            8 => _trafficSortAsc
                ? ordered.ThenBy(r => r.SessionTx)
                : ordered.ThenByDescending(r => r.SessionTx),
            9 => _trafficSortAsc
                ? ordered.ThenBy(r => r.SessionRx)
                : ordered.ThenByDescending(r => r.SessionRx),
            10 => _trafficSortAsc
                ? ordered.ThenBy(r => r.AllTimeTx)
                : ordered.ThenByDescending(r => r.AllTimeTx),
            11 => _trafficSortAsc
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

    private sealed record RouteTableRow(string Host, string Ip, string Country, string Asn, bool Routed);

    private async Task RefreshRoutesTableAsync()
    {
        var hosts = _hosts.Lines
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mapped = _service.HostToIps;
        var managed = _service.ManagedDestinations.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<RouteTableRow>();
        var ipsToGeo = new List<System.Net.IPAddress>();

        foreach (var host in hosts)
        {
            IReadOnlyCollection<string> ipTexts;
            if (mapped.TryGetValue(host, out var known) && known.Count > 0)
            {
                ipTexts = known;
            }
            else if (System.Net.IPAddress.TryParse(host, out var literal) &&
                     literal.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                ipTexts = [literal.ToString()];
            }
            else
            {
                try
                {
                    var resolved = await HostRouteService.ResolveIpv4Async(host, CancellationToken.None)
                        .ConfigureAwait(true);
                    ipTexts = resolved.Select(i => i.ToString()).ToList();
                }
                catch
                {
                    ipTexts = [];
                }
            }

            if (ipTexts.Count == 0)
            {
                rows.Add(new RouteTableRow(host, "", "", "", false));
                continue;
            }

            foreach (var ipText in ipTexts)
            {
                if (System.Net.IPAddress.TryParse(ipText, out var ip))
                    ipsToGeo.Add(ip);
                rows.Add(new RouteTableRow(host, ipText, "", "", managed.Contains(ipText)));
            }
        }

        await Task.Run(() => IpGeoLookup.LookupMany(ipsToGeo)).ConfigureAwait(true);

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r.Ip.Length == 0)
                continue;
            var geo = IpGeoLookup.Lookup(r.Ip);
            rows[i] = r with { Country = geo.CountryDisplay, Asn = geo.AsnDisplay };
        }

        FillRoutesTable(rows);
    }

    private void FillRoutesTable(IEnumerable<RouteTableRow> rows)
    {
        IEnumerable<RouteTableRow> ordered = _routesSortColumn switch
        {
            0 => _routesSortAsc
                ? rows.OrderBy(r => r.Host, StringComparer.OrdinalIgnoreCase)
                : rows.OrderByDescending(r => r.Host, StringComparer.OrdinalIgnoreCase),
            1 => _routesSortAsc
                ? rows.OrderBy(r => r.Ip, StringComparer.OrdinalIgnoreCase)
                : rows.OrderByDescending(r => r.Ip, StringComparer.OrdinalIgnoreCase),
            2 => _routesSortAsc
                ? rows.OrderBy(r => r.Country, StringComparer.OrdinalIgnoreCase)
                : rows.OrderByDescending(r => r.Country, StringComparer.OrdinalIgnoreCase),
            3 => _routesSortAsc
                ? rows.OrderBy(r => r.Asn, StringComparer.OrdinalIgnoreCase)
                : rows.OrderByDescending(r => r.Asn, StringComparer.OrdinalIgnoreCase),
            4 => _routesSortAsc
                ? rows.OrderBy(r => r.Routed)
                : rows.OrderByDescending(r => r.Routed),
            _ => rows.OrderBy(r => r.Host, StringComparer.OrdinalIgnoreCase),
        };

        var selected = _routesTable.SelectedItems
            .Cast<ListViewItem>()
            .Select(i => i.Tag as RouteTableRow)
            .Where(r => r is not null)
            .Select(r => $"{r!.Host}|{r.Ip}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _routesTable.BeginUpdate();
        _routesTable.Items.Clear();
        foreach (var r in ordered)
        {
            var item = new ListViewItem(r.Host) { Tag = r };
            item.SubItems.Add(r.Ip);
            item.SubItems.Add(r.Country);
            item.SubItems.Add(r.Asn);
            item.SubItems.Add(r.Routed ? "yes" : "");
            if (!r.Routed)
                item.ForeColor = Color.Gray;
            _routesTable.Items.Add(item);
            if (selected.Contains($"{r.Host}|{r.Ip}"))
                item.Selected = true;
        }
        _routesTable.EndUpdate();
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
            await RefreshRoutesTableAsync().ConfigureAwait(true);
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
                    {
                        await _service.RefreshAsync(hostList, adapter, token).ConfigureAwait(false);
                        try
                        {
                            Task refreshUi = Task.CompletedTask;
                            Invoke(() => { refreshUi = RefreshRoutesTableAsync(); });
                            await refreshUi.ConfigureAwait(false);
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
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
            _processDetectCts?.Dispose();
        }
        base.Dispose(disposing);
    }
}
