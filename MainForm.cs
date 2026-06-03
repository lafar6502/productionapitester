using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Configurator.Interfaces.Client;
using Configurator.Interfaces.WebApi.Production;
using Newtonsoft.Json;

namespace ProductionApiTester
{
    public class MainForm : Form
    {
        // Connection controls
        private TextBox txtUrl;
        private TextBox txtUser;
        private TextBox txtPass;

        // Query controls
        private ComboBox cboWorkCenter;
        private ComboBox cboStatus;
        private TextBox txtOrderNo;
        private TextBox txtBatchId;
        private Button btnQuery;
        private Label lblStatus;

        // Results
        private DataGridView grid;
        private TextBox txtJson;

        private List<ProductionOrderData> _results;

        // Debounce timer for connection field changes
        private Timer _reloadTimer;

        public MainForm()
        {
            BuildUI();
            LoadSettings();

            // Trigger initial work center load if URL is already set
            if (!string.IsNullOrWhiteSpace(txtUrl.Text))
                ScheduleWorkCenterReload();
        }

        private void BuildUI()
        {
            Text = "Production API Tester";
            Size = new Size(1200, 800);
            MinimumSize = new Size(800, 600);
            Font = new Font("Segoe UI", 9f);

            _reloadTimer = new Timer { Interval = 800 };
            _reloadTimer.Tick += (s, e) => { _reloadTimer.Stop(); LoadWorkCenters(); };

            // ── Connection group ──────────────────────────────────────────
            var grpConn = new GroupBox { Text = "Connection", Dock = DockStyle.Top, Height = 60, Padding = new Padding(6, 2, 6, 2) };

            var pnlConn = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true
            };

            pnlConn.Controls.Add(new Label { Text = "URL:", AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            txtUrl = new TextBox { Width = 340, Margin = new Padding(0, 4, 10, 0) };
            txtUrl.TextChanged += OnConnectionFieldChanged;
            pnlConn.Controls.Add(txtUrl);

            pnlConn.Controls.Add(new Label { Text = "User:", AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            txtUser = new TextBox { Width = 120, Margin = new Padding(0, 4, 10, 0) };
            txtUser.TextChanged += OnConnectionFieldChanged;
            pnlConn.Controls.Add(txtUser);

            pnlConn.Controls.Add(new Label { Text = "Password:", AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            txtPass = new TextBox { Width = 120, PasswordChar = '*', Margin = new Padding(0, 4, 0, 0) };
            txtPass.TextChanged += OnConnectionFieldChanged;
            pnlConn.Controls.Add(txtPass);

            grpConn.Controls.Add(pnlConn);

            // ── Query group ───────────────────────────────────────────────
            var grpQuery = new GroupBox { Text = "Query", Dock = DockStyle.Top, Height = 60, Padding = new Padding(6, 2, 6, 2) };

            var pnlQuery = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true
            };

            pnlQuery.Controls.Add(new Label { Text = "Work Center:", AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            cboWorkCenter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220,
                Margin = new Padding(0, 4, 10, 0)
            };
            pnlQuery.Controls.Add(cboWorkCenter);

            pnlQuery.Controls.Add(new Label { Text = "Status:", AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            cboStatus = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120,
                Margin = new Padding(0, 4, 10, 0)
            };
            cboStatus.Items.AddRange(new object[] { "Available", "Started", "Completed" });
            cboStatus.SelectedIndex = 0;
            pnlQuery.Controls.Add(cboStatus);

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
            lblStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.DimGray,
                Text = "Ready.",
                Padding = new Padding(4, 2, 0, 0)
            };

            // ── Splitter: grid (top) + JSON (bottom) ──────────────────────
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 420
            };

            grid = new DataGridView
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
            grid.SelectionChanged += OnGridSelectionChanged;

            AddColumn("Barcode", "Barcode");
            AddColumn("ShortInfo", "Description");
            AddColumn("ProductId", "Product ID");
            AddColumn("ModelId", "Model");
            AddColumn("Quantity", "Qty");
            AddColumn("Status", "Status");
            AddColumn("OrderNo", "Order No");
            AddColumn("Customer", "Customer");
            AddColumn("PlannedStart", "Planned Start");
            AddColumn("PlannedEnd", "Planned End");
            AddColumn("RackId", "Rack");
            AddColumn("BatchId", "Batch");
            AddColumn("ModelCode", "Model Code");
            AddColumn("FabricCode", "Fabric Code");
            AddColumn("SteeringCode", "Steering Code");
            AddColumn("ProfileColor", "Profile Color");
            AddColumn("Dimensions", "Dimensions");

            txtJson = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                Font = new Font("Consolas", 9f),
                WordWrap = false
            };

            split.Panel1.Controls.Add(grid);
            split.Panel2.Controls.Add(txtJson);

            // ── Assemble form (controls added bottom-up for Dock=Top) ─────
            Controls.Add(split);
            Controls.Add(lblStatus);
            Controls.Add(grpQuery);
            Controls.Add(grpConn);

            AcceptButton = btnQuery;
        }

        private void AddColumn(string name, string header)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                DataPropertyName = name,
                SortMode = DataGridViewColumnSortMode.Automatic
            });
        }

        private void LoadSettings()
        {
            txtUrl.Text = ConfigurationManager.AppSettings["Url"] ?? "http://localhost:8486/";
            txtUser.Text = ConfigurationManager.AppSettings["User"] ?? "";
            txtPass.Text = ConfigurationManager.AppSettings["Pass"] ?? "";
            // saved work center name — will be re-selected after the list loads
            cboWorkCenter.Tag = ConfigurationManager.AppSettings["WorkCenter"] ?? "";
        }

        private void SaveSettings()
        {
            var cfg = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            SetOrAdd(cfg, "Url", txtUrl.Text);
            SetOrAdd(cfg, "User", txtUser.Text);
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

        // ── Work center loading ───────────────────────────────────────────

        private void OnConnectionFieldChanged(object sender, EventArgs e)
        {
            ScheduleWorkCenterReload();
        }

        private void ScheduleWorkCenterReload()
        {
            _reloadTimer.Stop();
            _reloadTimer.Start();
        }

        private void LoadWorkCenters()
        {
            var url = txtUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(url)) return;

            var user = txtUser.Text.Trim();
            var pass = txtPass.Text;
            var previousSelection = cboWorkCenter.SelectedItem?.ToString() ?? cboWorkCenter.Tag?.ToString() ?? "";

            cboWorkCenter.Enabled = false;
            lblStatus.Text = "Loading work centers...";
            lblStatus.ForeColor = Color.DimGray;

            System.Threading.Tasks.Task.Run(() =>
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

                        // Restore previous selection if still present
                        if (!string.IsNullOrEmpty(previousSelection))
                        {
                            var idx = cboWorkCenter.FindStringExact(previousSelection);
                            cboWorkCenter.SelectedIndex = idx >= 0 ? idx : (cboWorkCenter.Items.Count > 0 ? 0 : -1);
                        }
                        else if (cboWorkCenter.Items.Count > 0)
                        {
                            cboWorkCenter.SelectedIndex = 0;
                        }

                        cboWorkCenter.Tag = null; // clear the startup hint
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
            if (string.IsNullOrWhiteSpace(txtUrl.Text))
            {
                MessageBox.Show("Please enter the server URL.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (cboWorkCenter.SelectedItem == null)
            {
                MessageBox.Show("Please select a Work Center.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnQuery.Enabled = false;
            lblStatus.Text = "Querying...";
            lblStatus.ForeColor = Color.DimGray;
            grid.Rows.Clear();
            txtJson.Clear();

            SaveSettings();

            var url = txtUrl.Text.Trim();
            var user = txtUser.Text.Trim();
            var pass = txtPass.Text;
            var workCenter = cboWorkCenter.SelectedItem.ToString();
            var status = (WorkStatus)cboStatus.SelectedIndex;
            var orderNo = txtOrderNo.Text.Trim();
            int? batchId = null;
            if (!string.IsNullOrWhiteSpace(txtBatchId.Text) && int.TryParse(txtBatchId.Text.Trim(), out int bid))
                batchId = bid;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var cc = new ConfiguratorClient { BaseUrl = url, UserName = user, Password = pass };

                    var msg = new QueryProductionOrdersForWorkCenter
                    {
                        WorkCenter = workCenter,
                        Status = status,
                        OrderNo = string.IsNullOrEmpty(orderNo) ? null : orderNo,
                        BatchId = batchId
                    };

                    var results = cc.QueryProductionOrdersForWorkCenter(msg).ToList();

                    Invoke((Action)(() =>
                    {
                        _results = results;
                        PopulateGrid(results);
                        lblStatus.Text = $"{results.Count} result(s) returned.";
                        lblStatus.ForeColor = Color.DarkGreen;
                        btnQuery.Enabled = true;
                    }));
                }
                catch (Exception ex)
                {
                    Invoke((Action)(() =>
                    {
                        lblStatus.Text = "Error: " + ex.Message;
                        lblStatus.ForeColor = Color.Red;
                        txtJson.Text = ex.ToString();
                        btnQuery.Enabled = true;
                    }));
                }
            });
        }

        private void PopulateGrid(List<ProductionOrderData> results)
        {
            grid.Rows.Clear();
            foreach (var r in results)
            {
                grid.Rows.Add(
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
                    r.FabricCode,
                    r.ModelCode,
                    r.SteeringCode,
                    r.Dimensions,
                    r.ProductionSize,
                    r.ProfileColor
                );
            }
        }

        private void OnGridSelectionChanged(object sender, EventArgs e)
        {
            if (_results == null || grid.SelectedRows.Count == 0)
            {
                txtJson.Clear();
                return;
            }

            var idx = grid.SelectedRows[0].Index;
            if (idx < 0 || idx >= _results.Count) return;

            var item = _results[idx];
            txtJson.Text = JsonConvert.SerializeObject(item, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
            });
        }
    }
}
