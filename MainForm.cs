using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Configurator.Interfaces.Client;
using Configurator.Interfaces.WebApi.Production;
using Newtonsoft.Json;

namespace ProductionApiTester
{
    public class MainForm : Form
    {
        private AppSettings _settings;
        private string _pass = "";

        // Work center bar (above tabs)
        private ComboBox cboWorkCenter;
        private Button btnRefresh;
        private Label lblStatus;

        // Available tab filter controls
        private TextBox txtOrderNo;
        private TextBox txtBatchId;
        private Button btnQueryAvailable;

        // Tabs & grids
        private DataGridView gridAvailable;
        private DataGridView gridStarted;
        private Button btnStartWork;

        // JSON preview
        private TextBox txtJson;

        private List<ProductionOrderData> _resultsAvailable;
        private List<ProductionOrderData> _resultsStarted;

        private static readonly Dictionary<string, Func<ProductionOrderData, object>> _getters =
            new Dictionary<string, Func<ProductionOrderData, object>>
            {
                ["Barcode"]      = r => r.Barcode,
                ["ShortInfo"]    = r => r.ShortInfo,
                ["LRef"]         = r => r.LRef,
                ["ProductId"]    = r => r.ProductId,
                ["ModelId"]      = r => r.ModelId,
                ["Quantity"]     = r => r.Quantity,
                ["Status"]       = r => r.Status,
                ["OrderNo"]      = r => r.Order?.OrderNo,
                ["Customer"]     = r => r.Order?.Customer,
                ["PlannedStart"] = r => r.PlannedStart?.ToString("yyyy-MM-dd"),
                ["PlannedEnd"]   = r => r.PlannedEnd?.ToString("yyyy-MM-dd"),
                ["RackId"]       = r => r.RackId,
                ["BatchId"]      = r => r.BatchId,
                ["ModelCode"]    = r => r.ModelCode,
                ["FabricCode"]   = r => r.FabricCode,
                ["SteeringCode"] = r => r.SteeringCode,
                ["ProfileColor"] = r => r.ProfileColor,
                ["Dimensions"]   = r => r.Dimensions,
            };

        public MainForm()
        {
            BuildUI();
            _settings = AppSettings.Load();
            cboWorkCenter.Tag = _settings.WorkCenter;

            ApplyColumnSettings(gridAvailable);
            ApplyColumnSettings(gridStarted);

            if (!string.IsNullOrWhiteSpace(_settings.Url))
                LoadWorkCenters();
        }

        private void BuildUI()
        {
            Text        = "Production API Tester";
            Size        = new Size(1200, 800);
            MinimumSize = new Size(800, 600);
            Font        = new Font("Segoe UI", 9f);

            // ── Menu ─────────────────────────────────────────────────────
            var menuStrip   = new MenuStrip();
            var menuFile    = new ToolStripMenuItem("File");
            var menuView    = new ToolStripMenuItem("View");
            menuFile.DropDownItems.Add(new ToolStripMenuItem("Connection Settings...", null, OnConnectionSettings));
            menuFile.DropDownItems.Add(new ToolStripSeparator());
            menuFile.DropDownItems.Add(new ToolStripMenuItem("Exit", null, (s, e) => Close()));
            menuView.DropDownItems.Add(new ToolStripMenuItem("Columns...", null, OnColumnSettings));
            menuStrip.Items.Add(menuFile);
            menuStrip.Items.Add(menuView);
            MainMenuStrip = menuStrip;

            // ── Work center bar ───────────────────────────────────────────
            var grpWc  = new GroupBox { Text = "Work Center", Dock = DockStyle.Top, Height = 60, Padding = new Padding(6, 2, 6, 2) };
            var pnlWc  = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };

            cboWorkCenter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, Margin = new Padding(0, 4, 10, 0) };
            cboWorkCenter.SelectedIndexChanged += OnWorkCenterSelectionChanged;
            pnlWc.Controls.Add(cboWorkCenter);

            btnRefresh = new Button { Text = "Refresh", Width = 80, Height = 26, Margin = new Padding(0, 3, 0, 0) };
            btnRefresh.Click += OnRefresh;
            pnlWc.Controls.Add(btnRefresh);

            grpWc.Controls.Add(pnlWc);

            // ── Status label ──────────────────────────────────────────────
            lblStatus = new Label { Dock = DockStyle.Top, Height = 20, ForeColor = Color.DimGray, Text = "Ready.", Padding = new Padding(4, 2, 0, 0) };

            // ── Splitter: tabs (top) + JSON (bottom) ──────────────────────
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 420 };

            // ── Available tab ─────────────────────────────────────────────
            var pnlFilter = new FlowLayoutPanel
            {
                Dock          = DockStyle.Top,
                Height        = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                Padding       = new Padding(4, 4, 0, 0)
            };
            pnlFilter.Controls.Add(new Label { Text = "Order No:", AutoSize = true, Margin = new Padding(0, 4, 2, 0) });
            txtOrderNo = new TextBox { Width = 120, Margin = new Padding(0, 2, 10, 0) };
            pnlFilter.Controls.Add(txtOrderNo);
            pnlFilter.Controls.Add(new Label { Text = "Batch ID:", AutoSize = true, Margin = new Padding(0, 4, 2, 0) });
            txtBatchId = new TextBox { Width = 80, Margin = new Padding(0, 2, 10, 0) };
            pnlFilter.Controls.Add(txtBatchId);
            btnQueryAvailable = new Button { Text = "Query", Width = 80, Height = 26, Margin = new Padding(0, 1, 0, 0) };
            btnQueryAvailable.Click += OnQueryAvailable;
            pnlFilter.Controls.Add(btnQueryAvailable);

            gridAvailable = CreateGrid();
            gridAvailable.SelectionChanged += OnAvailableSelectionChanged;

            btnStartWork = new Button { Text = "Start Work", Height = 32, Dock = DockStyle.Bottom, Enabled = false };
            btnStartWork.Click += OnStartWork;

            var pnlAvailable = new Panel { Dock = DockStyle.Fill };
            pnlAvailable.Controls.Add(gridAvailable);  // Fill
            pnlAvailable.Controls.Add(btnStartWork);   // Bottom
            pnlAvailable.Controls.Add(pnlFilter);      // Top (added last → sits at top)

            var tabAvailable = new TabPage { Text = "Available" };
            tabAvailable.Controls.Add(pnlAvailable);

            // ── Started tab ───────────────────────────────────────────────
            gridStarted = CreateGrid();
            gridStarted.SelectionChanged += OnStartedSelectionChanged;

            var tabStarted = new TabPage { Text = "Started" };
            tabStarted.Controls.Add(gridStarted);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(tabAvailable);
            tabs.TabPages.Add(tabStarted);
            split.Panel1.Controls.Add(tabs);

            // ── JSON preview ──────────────────────────────────────────────
            txtJson = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, Font = new Font("Consolas", 9f), WordWrap = false };
            split.Panel2.Controls.Add(txtJson);

            // ── Assemble form (bottom-up for Dock=Top) ────────────────────
            Controls.Add(split);
            Controls.Add(lblStatus);
            Controls.Add(grpWc);
            Controls.Add(menuStrip);

            AcceptButton = btnQueryAvailable;
        }

        private DataGridView CreateGrid()
        {
            var g = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.AllCells,
                RowHeadersVisible     = false,
                BackgroundColor       = SystemColors.Window,
                BorderStyle           = BorderStyle.None
            };
            return g;
        }

        private void ApplyColumnSettings(DataGridView g)
        {
            var visible = _settings?.VisibleColumns
                ?? AppSettings.AllColumns.Select(c => c.Name).ToList();

            g.Columns.Clear();
            foreach (var name in visible)
            {
                var col = AppSettings.AllColumns.FirstOrDefault(c => c.Name == name);
                if (col.Name != null)
                    AddCol(g, col.Name, col.Header);
            }
        }

        private static void AddCol(DataGridView g, string name, string header)
        {
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = name,
                HeaderText       = header,
                DataPropertyName = name,
                SortMode         = DataGridViewColumnSortMode.Automatic
            });
        }

        // ── Settings ──────────────────────────────────────────────────────

        private void OnConnectionSettings(object sender, EventArgs e)
        {
            using (var dlg = new ConfigDialog(_settings.Url, _settings.User, ""))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _settings.Url  = dlg.Url;
                _settings.User = dlg.UserName;
                _pass          = dlg.Password;
                _settings.Save();
                LoadWorkCenters();
            }
        }

        private void OnColumnSettings(object sender, EventArgs e)
        {
            using (var dlg = new ColumnSettingsDialog(_settings.VisibleColumns))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _settings.VisibleColumns = dlg.VisibleColumns;
                _settings.Save();

                ApplyColumnSettings(gridAvailable);
                ApplyColumnSettings(gridStarted);

                if (_resultsAvailable != null) PopulateGrid(gridAvailable, _resultsAvailable);
                if (_resultsStarted   != null) PopulateGrid(gridStarted,   _resultsStarted);
            }
        }

        // ── Work center loading ───────────────────────────────────────────

        private void LoadWorkCenters()
        {
            if (string.IsNullOrWhiteSpace(_settings.Url)) return;

            var url  = _settings.Url;
            var user = _settings.User;
            var pass = _pass;
            var previousSelection = cboWorkCenter.SelectedItem?.ToString() ?? cboWorkCenter.Tag?.ToString() ?? "";

            SetQueryingState(true);
            lblStatus.Text = "Loading work centers...";

            Task.Run(() =>
            {
                try
                {
                    var cc    = new ConfiguratorClient { BaseUrl = url, UserName = user, Password = pass };
                    var data  = cc.GetProductionConfigurationData();
                    var names = (data.WorkCenters ?? Enumerable.Empty<WorkCenterInfo>())
                        .Select(w => w.Name).OrderBy(n => n).ToList();

                    Invoke((Action)(() =>
                    {
                        // Suppress SelectedIndexChanged during list rebuild
                        cboWorkCenter.SelectedIndexChanged -= OnWorkCenterSelectionChanged;
                        cboWorkCenter.Items.Clear();
                        foreach (var n in names) cboWorkCenter.Items.Add(n);

                        if (!string.IsNullOrEmpty(previousSelection))
                        {
                            var idx = cboWorkCenter.FindStringExact(previousSelection);
                            cboWorkCenter.SelectedIndex = idx >= 0 ? idx : (names.Count > 0 ? 0 : -1);
                        }
                        else if (names.Count > 0)
                        {
                            cboWorkCenter.SelectedIndex = 0;
                        }

                        cboWorkCenter.Tag = null;
                        cboWorkCenter.SelectedIndexChanged += OnWorkCenterSelectionChanged;

                        SetQueryingState(false);
                        lblStatus.Text      = $"Loaded {names.Count} work center(s).";
                        lblStatus.ForeColor = Color.DarkGreen;

                        // Auto-query both tabs after loading work centers
                        if (cboWorkCenter.SelectedItem != null)
                            ExecuteBothQueries();
                    }));
                }
                catch (Exception ex)
                {
                    Invoke((Action)(() =>
                    {
                        cboWorkCenter.SelectedIndexChanged += OnWorkCenterSelectionChanged;
                        SetQueryingState(false);
                        lblStatus.Text      = "Could not load work centers: " + ex.Message;
                        lblStatus.ForeColor = Color.OrangeRed;
                    }));
                }
            });
        }

        // ── Query orchestration ───────────────────────────────────────────

        private void OnWorkCenterSelectionChanged(object sender, EventArgs e)
        {
            if (cboWorkCenter.SelectedItem == null || string.IsNullOrWhiteSpace(_settings?.Url)) return;
            ExecuteBothQueries();
        }

        private void OnRefresh(object sender, EventArgs e)
        {
            if (cboWorkCenter.SelectedItem == null) return;
            ExecuteBothQueries();
        }

        private void OnQueryAvailable(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settings?.Url))
            {
                MessageBox.Show("Please configure the connection first (File > Connection Settings).", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (cboWorkCenter.SelectedItem == null)
            {
                MessageBox.Show("Please select a Work Center.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ExecuteAvailableQuery();
        }

        private void ExecuteBothQueries()
        {
            _settings.WorkCenter = cboWorkCenter.SelectedItem.ToString();
            _settings.Save();

            SetQueryingState(true);
            gridAvailable.Rows.Clear();
            gridStarted.Rows.Clear();
            txtJson.Clear();
            lblStatus.Text      = "Querying...";
            lblStatus.ForeColor = Color.DimGray;

            var url        = _settings.Url;
            var user       = _settings.User;
            var pass       = _pass;
            var workCenter = _settings.WorkCenter;
            var orderNo    = txtOrderNo.Text.Trim();
            var batchId    = GetBatchId();

            var taskAvailable = Task.Run(() =>
                new ConfiguratorClient { BaseUrl = url, UserName = user, Password = pass }
                    .QueryProductionOrdersForWorkCenter(new QueryProductionOrdersForWorkCenter
                    {
                        WorkCenter = workCenter,
                        Status     = WorkStatus.Available,
                        OrderNo    = string.IsNullOrEmpty(orderNo) ? null : orderNo,
                        BatchId    = batchId
                    }).ToList());

            var taskStarted = Task.Run(() =>
                new ConfiguratorClient { BaseUrl = url, UserName = user, Password = pass }
                    .QueryProductionOrdersForWorkCenter(new QueryProductionOrdersForWorkCenter
                    {
                        WorkCenter = workCenter,
                        Status     = WorkStatus.Started
                    }).ToList());

            Task.WhenAll(taskAvailable, taskStarted).ContinueWith(_ =>
            {
                Invoke((Action)(() =>
                {
                    SetQueryingState(false);
                    if (taskAvailable.IsFaulted || taskStarted.IsFaulted)
                    {
                        var ex = (taskAvailable.Exception ?? taskStarted.Exception).InnerException;
                        lblStatus.Text      = "Error: " + ex.Message;
                        lblStatus.ForeColor = Color.Red;
                        txtJson.Text        = ex.ToString();
                    }
                    else
                    {
                        _resultsAvailable = taskAvailable.Result;
                        _resultsStarted   = taskStarted.Result;
                        PopulateGrid(gridAvailable, _resultsAvailable);
                        PopulateGrid(gridStarted,   _resultsStarted);
                        lblStatus.Text      = $"{_resultsAvailable.Count} available, {_resultsStarted.Count} started.";
                        lblStatus.ForeColor = Color.DarkGreen;
                    }
                }));
            });
        }

        private void ExecuteAvailableQuery()
        {
            btnQueryAvailable.Enabled = false;
            gridAvailable.Rows.Clear();
            txtJson.Clear();
            lblStatus.Text      = "Querying available orders...";
            lblStatus.ForeColor = Color.DimGray;

            var url        = _settings.Url;
            var user       = _settings.User;
            var pass       = _pass;
            var workCenter = cboWorkCenter.SelectedItem.ToString();
            var orderNo    = txtOrderNo.Text.Trim();
            var batchId    = GetBatchId();

            Task.Run(() =>
                new ConfiguratorClient { BaseUrl = url, UserName = user, Password = pass }
                    .QueryProductionOrdersForWorkCenter(new QueryProductionOrdersForWorkCenter
                    {
                        WorkCenter = workCenter,
                        Status     = WorkStatus.Available,
                        OrderNo    = string.IsNullOrEmpty(orderNo) ? null : orderNo,
                        BatchId    = batchId
                    }).ToList()
            ).ContinueWith(t =>
            {
                Invoke((Action)(() =>
                {
                    btnQueryAvailable.Enabled = true;
                    if (t.IsFaulted)
                    {
                        var ex = t.Exception.InnerException;
                        lblStatus.Text      = "Error: " + ex.Message;
                        lblStatus.ForeColor = Color.Red;
                        txtJson.Text        = ex.ToString();
                    }
                    else
                    {
                        _resultsAvailable = t.Result;
                        PopulateGrid(gridAvailable, _resultsAvailable);
                        lblStatus.Text      = $"{_resultsAvailable.Count} available, {_resultsStarted?.Count ?? 0} started.";
                        lblStatus.ForeColor = Color.DarkGreen;
                    }
                }));
            });
        }

        private int? GetBatchId()
        {
            if (!string.IsNullOrWhiteSpace(txtBatchId.Text) && int.TryParse(txtBatchId.Text.Trim(), out int bid))
                return bid;
            return null;
        }

        private void SetQueryingState(bool querying)
        {
            cboWorkCenter.Enabled     = !querying;
            btnRefresh.Enabled        = !querying;
            btnQueryAvailable.Enabled = !querying;
        }

        // ── Grid population ───────────────────────────────────────────────

        private static void PopulateGrid(DataGridView g, List<ProductionOrderData> results)
        {
            g.Rows.Clear();
            foreach (var r in results)
            {
                var values = new object[g.Columns.Count];
                for (int i = 0; i < g.Columns.Count; i++)
                {
                    if (_getters.TryGetValue(g.Columns[i].Name, out var getter))
                        values[i] = getter(r);
                }
                g.Rows.Add(values);
            }
        }

        private void OnAvailableSelectionChanged(object sender, EventArgs e)
        {
            ShowJson(gridAvailable, _resultsAvailable);
            btnStartWork.Enabled = gridAvailable.SelectedRows.Count > 0;
        }

        private void OnStartedSelectionChanged(object sender, EventArgs e)
        {
            ShowJson(gridStarted, _resultsStarted);
        }

        private void ShowJson(DataGridView g, List<ProductionOrderData> results)
        {
            if (results == null || g.SelectedRows.Count == 0) { txtJson.Clear(); return; }
            var idx = g.SelectedRows[0].Index;
            if (idx < 0 || idx >= results.Count) return;
            txtJson.Text = JsonConvert.SerializeObject(results[idx], Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Converters        = { new Newtonsoft.Json.Converters.StringEnumConverter() }
            });
        }

        // ── Start Work ────────────────────────────────────────────────────

        private void OnStartWork(object sender, EventArgs e)
        {
            if (_resultsAvailable == null || gridAvailable.SelectedRows.Count == 0) return;
            var idx = gridAvailable.SelectedRows[0].Index;
            if (idx < 0 || idx >= _resultsAvailable.Count) return;
            var item = _resultsAvailable[idx];

            // TODO: call the start-work API with item
            MessageBox.Show($"Start work: {item.Barcode}", "Start Work", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
