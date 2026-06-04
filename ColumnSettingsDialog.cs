using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ProductionApiTester
{
    public class ColumnSettingsDialog : Form
    {
        public List<string> VisibleColumns { get; private set; }

        private readonly CheckedListBox _lst;

        private class ColItem
        {
            public string Name;
            public string Header;
            public override string ToString() => Header;
        }

        public ColumnSettingsDialog(List<string> currentVisible)
        {
            Text            = "Column Settings";
            Size            = new Size(300, 460);
            MinimumSize     = new Size(260, 340);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.CenterParent;
            Font            = new Font("Segoe UI", 9f);

            var allNames = AppSettings.AllColumns.Select(c => c.Name).ToList();
            var visible  = (currentVisible ?? allNames).Where(n => allNames.Contains(n)).ToList();
            var hidden   = allNames.Except(visible).ToList();

            _lst = new CheckedListBox
            {
                Dock          = DockStyle.Fill,
                CheckOnClick  = true,
                IntegralHeight = false
            };

            foreach (var name in visible.Concat(hidden))
            {
                var col = AppSettings.AllColumns.First(c => c.Name == name);
                _lst.Items.Add(new ColItem { Name = name, Header = col.Header }, visible.Contains(name));
            }

            var btnUp   = new Button { Text = "▲", Width = 76, Height = 28, Margin = new Padding(0, 0, 0, 4) };
            var btnDown = new Button { Text = "▼", Width = 76, Height = 28 };
            btnUp.Click   += (s, e) => MoveSelected(-1);
            btnDown.Click += (s, e) => MoveSelected(1);

            var pnlRight = new FlowLayoutPanel
            {
                Dock          = DockStyle.Right,
                Width         = 88,
                FlowDirection = FlowDirection.TopDown,
                Padding       = new Padding(4, 8, 0, 0),
                WrapContents  = false
            };
            pnlRight.Controls.Add(btnUp);
            pnlRight.Controls.Add(btnDown);

            var btnOk     = new Button { Text = "OK",     Width = 76, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Width = 76, DialogResult = DialogResult.Cancel };
            btnOk.Click += (s, e) =>
            {
                VisibleColumns = Enumerable.Range(0, _lst.Items.Count)
                    .Where(i => _lst.GetItemChecked(i))
                    .Select(i => ((ColItem)_lst.Items[i]).Name)
                    .ToList();
            };

            var pnlBottom = new FlowLayoutPanel
            {
                Dock          = DockStyle.Bottom,
                Height        = 38,
                FlowDirection = FlowDirection.RightToLeft,
                Padding       = new Padding(4, 4, 4, 0)
            };
            pnlBottom.Controls.Add(btnCancel);
            pnlBottom.Controls.Add(btnOk);

            Controls.Add(_lst);
            Controls.Add(pnlRight);
            Controls.Add(pnlBottom);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void MoveSelected(int direction)
        {
            var idx = _lst.SelectedIndex;
            if (idx < 0) return;
            var newIdx = idx + direction;
            if (newIdx < 0 || newIdx >= _lst.Items.Count) return;

            var item      = _lst.Items[idx];
            var isChecked = _lst.GetItemChecked(idx);
            _lst.Items.RemoveAt(idx);
            _lst.Items.Insert(newIdx, item);
            _lst.SetItemChecked(newIdx, isChecked);
            _lst.SelectedIndex = newIdx;
        }
    }
}
