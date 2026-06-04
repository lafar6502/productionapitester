using System.Drawing;
using System.Windows.Forms;

namespace ProductionApiTester
{
    public class ConfigDialog : Form
    {
        public string Url      { get; private set; }
        public string UserName { get; private set; }
        public string Password { get; private set; }

        private TextBox txtUrl;
        private TextBox txtUser;
        private TextBox txtPass;

        public ConfigDialog(string url, string userName, string password)
        {
            Text            = "Connection Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            Size            = new Size(400, 200);
            Font            = new System.Drawing.Font("Segoe UI", 9f);

            var table = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 4,
                Padding     = new Padding(12)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            txtUrl  = new TextBox { Dock = DockStyle.Fill, Text = url };
            txtUser = new TextBox { Dock = DockStyle.Fill, Text = userName };
            txtPass = new TextBox { Dock = DockStyle.Fill, PasswordChar = '*', Text = password };

            table.Controls.Add(new Label { Text = "URL:",      Anchor = AnchorStyles.Left | AnchorStyles.Right, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 0);
            table.Controls.Add(txtUrl,  1, 0);
            table.Controls.Add(new Label { Text = "User:",     Anchor = AnchorStyles.Left | AnchorStyles.Right, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 1);
            table.Controls.Add(txtUser, 1, 1);
            table.Controls.Add(new Label { Text = "Password:", Anchor = AnchorStyles.Left | AnchorStyles.Right, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 2);
            table.Controls.Add(txtPass, 1, 2);

            var pnlButtons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock          = DockStyle.Fill,
                WrapContents  = false
            };

            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
            var btnOk     = new Button { Text = "OK",     DialogResult = DialogResult.OK,     Width = 80 };
            btnOk.Click += (s, e) =>
            {
                Url      = txtUrl.Text.Trim();
                UserName = txtUser.Text.Trim();
                Password = txtPass.Text;
            };

            pnlButtons.Controls.Add(btnCancel);
            pnlButtons.Controls.Add(btnOk);

            table.Controls.Add(pnlButtons, 0, 3);
            table.SetColumnSpan(pnlButtons, 2);

            Controls.Add(table);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
