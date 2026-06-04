using System;
using System.Collections.Generic;
using System.Configuration;
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
        // Connection settings (edited via ConfigDialog)
        private string _url;
        private string _user;
        private string _pass;

        // Query controls
        private ComboBox cboWorkCenter;
        private TextBox txtOrderNo;
        private TextBox txtBatchId;
        private Button btnQuery;
        private Label lblStatus;

        // Tabs & grids
        private DataGridView gridAvailable;
        private DataGridView gridStarted;
        private Button btnStartWork;

        // JSON preview
        private TextBox txtJson;

        private List<ProductionOrderData> _resultsAvailable;
        private List<ProductionOrderData> _resultsStarted;

        public MainForm()
        {
            BuildUI();
            LoadSettings();

            if (!string.IsNullOrWhiteSpace(_url))
                LoadWorkCenters();
        }

        private void BuildUI()
        {
            Text = "Production API Tester";
            Size = new Size(1200, 800);
            MinimumSize = new Size(800, 600);
            Font = new Font("Segoe UI", 9f);

            // ── Menu ──────────────────────────────────────────────────────
            var menuStrip    = new MenuStrip();
            var menuFile     = new ToolStripMenuItem("File");
            var menuSettings = new ToolStripMenuItem("Connection Settings...", null, OnConnectionSettings);
            var menuExit     = new ToolStripMenuItem("Exit", null, (s, e) => Close());

            menuFile.DropDownItems.Add(menuSettings);
            menuFile.DropDownItems.Add(new ToolStripSeparator());
            menuFile.DropDownItems.Add(menuExit);
            menuStrip.Items.Add(menuFile);

            MainMenuStrip = menuStrip;

            // ── Query group ───────────────────────────────────────────────
            var grpQuery = new GroupBox { Text = "Query", Dock = DockStyle.Top, Height = 60, Padding = new Padding(6, 2, 6, 2) };
            var pnlQuery = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };

            pnlQuery.Controls.Add(new Label { Text = "Work Center:", AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            cboWorkCenter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Margin = new Padding(0, 4, 10, 0) };
            pnlQuery.Controls.Add(cboWorkCenter);

            pnlQuery.Controls.Add(new Label { Text = "Order No:", AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            txtOrderNo = new TextBox { Width = 120, Margin = new Padding(0, 4, 10, 0) };
            pnlQuery.Controls.Add(txtOrderNo);

            pnlQuery.Controls.Add(new Label { Text = "Batch ID:", AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            txtBatchId = new TextBox { Width = 80, Margin = new Padding(0, 4, 10, 0) };
            pnlQuery.Controls.Add(txtBatchId);

            btnQuery = new Button { Text = "Query", Width = 80, Height = 26, Margin = new Padding(0, 3, 0, 0) };
            btnQuery.Click += OnQuery;
            pnlQuery.Controls.Add(btnQuery);

            grpQuery.Controls.Add(pnlQuery);

            // ── Status label ──────────────────────────────────────────────
            lblStatus = new Label { Dock = DockStyle.Top, Height = 20, ForeColor = Color.DimGray, Text = "Ready.", Padding = new Padding(4, 2, 0, 0) };

            // ── Splitter: tabs (top) + JSON (bottom) ──────────────────────
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 420 };

            // Available tab
            gridAvailable = CreateGrid();
            gridAvailable.SelectionChanged += OnAvailableSelectionChanged;

            btnStartWork = new Button { Text = "Start Work", Height = 32, Dock = DockStyle.Bottom, Enabled = false };
            btnStartWork.Click += OnStartWork;

            var pnlAvailable = new Panel { Dock = DockStyle.Fill };
            pnlAvailable.Controls.Add(btnStartWork);
            pnlAvailable.Controls.Add(gridAvailable);

            var tabAvailable = new TabPage { Text = "Available" };
            tabAvailable.Controls.Add(pnlAvailable);

            // Started tab
            gridStarted = CreateGrid();
            gridStarted.SelectionChanged += OnStartedSelectionChanged;

            var tabStarted = new TabPage { Text = "Started" };
            tabStarted.Controls.Add(gridStarted);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(tabAvailable);
            tabs.TabPages.Add(tabStarted);

            split.Panel1.Controls.Add(tabs);

            // JSON preview
            txtJson = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, Font = new Font("Consolas", 9f), WordWrap = false };
            split.Panel2.Controls.Add(txtJson);

            // ── Assemble form (controls added bottom-up for Dock=Top) ─────
            Controls.Add(split);
            Controls.Add(lblStatus);
            Controls.Add(grpQuery);
            Controls.Add(menuStrip);

            AcceptButton = btnQuery;
        }

        private DataGridView CreateGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None
            };
            AddColumns(g);
            return g;
        }

        private static void AddColumns(DataGridView g)
        {
            AddCol(g, "Barcode",      "Barcode");
            AddCol(g, "ShortInfo",    "Description");
            AddCol(g, "ProductId",    "Product ID");
            AddCol(g, "ModelId",      "Model");
            AddCol(g, "Quantity",     "Qty");
            AddCol(g, "Status",       "Status");
            AddCol(g, "OrderNo",      "Order No");
            AddCol(g, "Customer",     "Customer");
            AddCol(g, "PlannedStart", "Planned Start");
            AddCol(g, "PlannedEnd",   "Planned End");
            AddCol(g, "RackId",       "Rack");
            AddCol(g, "BatchId",      "Batch");
            AddCol(g, "ModelCode",    "Model Code");
            AddCol(g, "FabricCode",   "Fabric Code");
            AddCol(g, "SteeringCode", "Steering Code");
            AddCol(g, "ProfileColor", "Profile Color");
            AddCol(g, "Dimensions",   "Dimensions");
        }

        private static void AddCol(DataGridView g, string name, string header)
        {
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                DataPropertyName = name,
                SortMode = DataGridViewColumnSortMode.Automatic
            });
        }

        // ── Settings ──────────────────────────────────────────────────────

        private void LoadSettings()
        {
            _url  = ConfigurationManager.AppSettings["Url"]  ?? "http://localhost:8486/";
            _user = ConfigurationManager.AppSettings["User"] ?? "";
            _pass = "";
            cboWorkCenter.Tag = ConfigurationManager.AppSettings["WorkCenter"] ?? "";
        }

        private void SaveSettings()
        {
            var cfg = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            SetOrAdd(cfg, "Url",        _url);
            SetOrAdd(cfg, "User",       _user);
            SetOrAdd(cfg, "WorkCenter", cboWorkCenter.SelectedItem?.ToString() ?? "");
            // intentionally not saving password
            cfg.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private static void SetOrAdd(System.Configuration.Configuration cfg, string key, string value)
        {
            if (cfg.AppSettings.Settings[key] != null)
                cfg.AppSettings.Settings[key].Value = value;
            else
                cfg.AppSettings.Settings.Add(key, value);
        }

        private void OnConnectionSettings(object sender, EventArgs e)
        {
            using (var dlg = new ConfigDialog(_url, _user, _pass))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _url  = dlg.Url;
                _user = dlg.UserName;
                _pass = dlg.Password;
                SaveSettings();
                LoadWorkCenters();
            }
        }

        // ── Work center loading ───────────────────────────────────────────

        private void LoadWorkCenters()
        {
            if (string.IsNullOrWhiteSpace(_url)) return;

            var url  = _url;
            var user = _user;
            var pass = _pass;
            var previousSelection = cboWorkCenter.SelectedItem?.ToString() ?? cboWorkCenter.Tag?.ToString() ?? "";

            cboWorkCenter.Enabled = false;
            lblStatus.Text = "Loading work centers...";
            lblStatus.ForeColor = Color.DimGray;

            Task.Run(() =>
            {
                try
                {
                    var cc = new ConfiguratorClient { BaseUrl = url, UserName = user, Password = pass };
                    var data = cc.GetProductionConfigurationData();
                    var names = (data.WorkCenters ?? Enumerable.Empty<WorkCenterInfo>())
                        .Select(w => w.Name)
                        .OrderBy(n => n)
                        .ToList();

                    Invoke((Action)(() =>
                    {
                        cboWorkCenter.Items.Clear();
                        foreach (var n in names)
                            cboWorkCenter.Items.Add(n);

                        if (!string.IsNullOrEmpty(previousSelection))
                        {
                            var idx = cboWorkCenter.FindStringExact(previousSelection);
                            cboWorkCenter.SelectedIndex = idx >= 0 ? idx : (cboWorkCenter.Items.Count > 0 ? 0 : -1);
                        }
                        else if (cboWorkCenter.Items.Count > 0)
                        {
                            cboWorkCenter.SelectedIndex = 0;
                        }

                        cboWorkCenter.Tag = null;
                        cboWorkCenter.Enabled = true;
                        lblStatus.Text = $"Loaded {names.Count} work center(s).";
                        lblStatus.ForeColor = Color.DarkGreen;
                    }));
                }
                catch (Exception ex)
                {
                    Invoke((Action)(() =>
                    {
                        cboWorkCenter.Enabled = true;
                        lblStatus.Text = "Could not load work centers: " + ex.Message;
                        lblStatus.ForeColor = Color.OrangeRed;
                    }));
                }
            });
        }

        // ── Query ─────────────────────────────────────────────────────────

        private void OnQuery(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_url))
            {
                MessageBox.Show("Please configure the connection first (File > Connection Settings).", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (cboWorkCenter.SelectedItem == null)
            {
                MessageBox.Show("Please select a Work Center.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnQuery.Enabled = false;
            btnStartWork.Enabled = false;
            lblStatus.Text = "Querying...";
            lblStatus.ForeColor = Color.DimGray;
            gridAvailable.Rows.Clear();
            gridStarted.Rows.Clear();
            txtJson.Clear();

            SaveSettings();

            var url        = _url;
            var user       = _user;
            var pass       = _pass;
            var workCenter = cboWorkCenter.SelectedItem.ToString();
            var orderNo    = txtOrderNo.Text.Trim();
            int? batchId   = null;
            if (!string.IsNullOrWhiteSpace(txtBatchId.Text) && int.TryParse(txtBatchId.Text.Trim(), out int bid))
                batchId = bid;

            QueryProductionOrdersForWorkCenter MakeMsg(WorkStatus status) => new QueryProductionOrdersForWorkCenter
            {
                WorkCenter = workCenter,
                Status     = status,
                OrderNo    = string.IsNullOrEmpty(orderNo) ? null : orderNo,
                BatchId    = batchId
            };

            var taskAvailable = Task.Run(() =>
                new ConfiguratorClient { BaseUrl = url, UserName = user, Password = pass }
                    .QueryProductionOrdersForWorkCenter(MakeMsg(WorkStatus.Available)).ToList());

            var taskStarted = Task.Run(() =>
                new ConfiguratorClient { BaseUrl = url, UserName = user, Password = pass }
                    .QueryProductionOrdersForWorkCenter(MakeMsg(WorkStatus.Started)).ToList());

            Task.WhenAll(taskAvailable, taskStarted).ContinueWith(_ =>
            {
                Invoke((Action)(() =>
                {
                    if (taskAvailable.IsFaulted || taskStarted.IsFaulted)
                    {
                        var ex = (taskAvailable.Exception ?? taskStarted.Exception).InnerException;
                        lblStatus.Text = "Error: " + ex.Message;
                        lblStatus.ForeColor = Color.Red;
                        txtJson.Text = ex.ToString();
                    }
                    else
                    {
                        _resultsAvailable = taskAvailable.Result;
                        _resultsStarted   = taskStarted.Result;
                        PopulateGrid(gridAvailable, _resultsAvailable);
                        PopulateGrid(gridStarted,   _resultsStarted);
                        lblStatus.Text = $"{_resultsAvailable.Count} available, {_resultsStarted.Count} started.";
                        lblStatus.ForeColor = Color.DarkGreen;
                    }
                    btnQuery.Enabled = true;
                }));
            });
        }

        private static void PopulateGrid(DataGridView g, List<ProductionOrderData> results)
        {
            g.Rows.Clear();
            foreach (var r in results)
            {
                g.Rows.Add(
                    r.Barcode,
                    r.ShortInfo,
                    r.ProductId,
                    r.ModelId,
                    r.Quantity,
                    r.Status,
                    r.Order?.OrderNo,
                    r.Order?.Customer,
                    r.PlannedStart?.ToString("yyyy-MM-dd"),
                    r.PlannedEnd?.ToString("yyyy-MM-dd"),
                    r.RackId,
                    r.BatchId,
                    r.ModelCode,
                    r.FabricCode,
                    r.SteeringCode,
                    r.ProfileColor,
                    r.Dimensions
                );
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
                Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
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
