using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.UI.Forms
{
    /// <summary>
    /// 單一檢查項目的新增 / 編輯視窗（Presentation 層）。
    /// 允許使用者調整基本屬性與外掛專屬屬性字典（Properties），
    /// 實現需求 5「可以透過介面調整設定值內容」與需求 4「設定檔驅動」。
    /// </summary>
    public class CheckItemEditForm : Form
    {
        private readonly IPluginManager _pluginManager;

        /// <summary>編輯完成後可取得最新的 CheckItem 資料</summary>
        public CheckItem CheckItem { get; private set; }

        // ── Controls ───────────────────────────────────────────────────────────
        private TextBox   _txtId;
        private TextBox   _txtName;
        private TextBox   _txtDesc;
        private CheckBox  _chkEnabled;
        private ComboBox  _cmbPlugin;
        private DataGridView _dgvProps;
        private Button    _btnAddProp;
        private Button    _btnRemoveProp;
        private Button    _btnOk;
        private Button    _btnCancel;

        public CheckItemEditForm(CheckItem item, IPluginManager pluginManager)
        {
            _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            CheckItem      = item != null ? DeepClone(item)
                                          : new CheckItem { IsEnabled = true };
            InitializeComponent();
            PopulateForm();
        }

        // ── UI Initialisation ──────────────────────────────────────────────────

        private void InitializeComponent()
        {
            this.Text            = CheckItem.Id == null ? "新增檢查項目" : "編輯檢查項目";
            this.Size            = new Size(640, 530);
            this.MinimumSize     = new Size(560, 480);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;

            // ── Labels + inputs (top section) ─────────────────────────────────
            int labelW = 70, inputX = 88, row = 14, rowH = 32;
            int inputW = this.ClientSize.Width - inputX - 14;

            _txtId = AddLabeledTextBox("ID:", inputX, row, inputW, ref row, rowH,
                readOnly: true);
            _txtName = AddLabeledTextBox("名稱 *:", inputX, row, inputW, ref row, rowH);
            _txtDesc = AddLabeledTextBox("說明:", inputX, row, inputW, ref row, rowH);

            // Plugin combobox
            AddLabel("外掛 *:", inputX - labelW, row, labelW);
            _cmbPlugin = new ComboBox
            {
                Location      = new Point(inputX, row),
                Width         = inputW,
                Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var p in _pluginManager.GetAllPlugins())
                _cmbPlugin.Items.Add(new PluginEntry(p.PluginName, p.DisplayName));
            _cmbPlugin.DisplayMember = "Display";
            this.Controls.Add(_cmbPlugin);
            row += rowH;

            // Enabled checkbox
            AddLabel("啟用:", inputX - labelW, row, labelW);
            _chkEnabled = new CheckBox
            {
                Location = new Point(inputX, row + 2),
                Width    = 120,
                Text     = "此項目已啟用"
            };
            this.Controls.Add(_chkEnabled);
            row += rowH;

            // ── Properties grid ───────────────────────────────────────────────
            AddLabel("屬性:", inputX - labelW, row + 2, labelW);

            _dgvProps = new DataGridView
            {
                Location              = new Point(inputX, row),
                Anchor                = AnchorStyles.Top | AnchorStyles.Bottom
                                      | AnchorStyles.Left | AnchorStyles.Right,
                Width                 = inputW,
                Height                = this.ClientSize.Height - row - 100,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible     = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor       = SystemColors.Window,
                BorderStyle           = BorderStyle.Fixed3D
            };
            _dgvProps.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "Key",   HeaderText = "屬性名稱", FillWeight = 40 });
            _dgvProps.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "Value", HeaderText = "值",       FillWeight = 60 });
            this.Controls.Add(_dgvProps);
            row += 10;

            // Property action buttons
            _btnAddProp = new Button
            {
                Text   = "+ 新增屬性",
                Size   = new Size(90, 26),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            _btnAddProp.Click += BtnAddProp_Click;

            _btnRemoveProp = new Button
            {
                Text   = "- 移除屬性",
                Size   = new Size(90, 26),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            _btnRemoveProp.Click += BtnRemoveProp_Click;

            this.Controls.AddRange(new Control[] { _btnAddProp, _btnRemoveProp });

            // ── Dialog buttons ────────────────────────────────────────────────
            _btnOk = new Button
            {
                Text         = "確定(&O)",
                Size         = new Size(90, 30),
                Anchor       = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.OK
            };
            _btnOk.Click += BtnOk_Click;

            _btnCancel = new Button
            {
                Text         = "取消(&C)",
                Size         = new Size(90, 30),
                Anchor       = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[] { _btnOk, _btnCancel });
            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            this.Resize += (s, e) => LayoutDynamic();
            LayoutDynamic();
        }

        /// <summary>依視窗大小動態調整可調整控制項的位置</summary>
        private void LayoutDynamic()
        {
            int bottom = this.ClientSize.Height;
            _btnCancel.Location    = new Point(this.ClientSize.Width - 104, bottom - 42);
            _btnOk.Location        = new Point(this.ClientSize.Width - 200, bottom - 42);
            _btnRemoveProp.Location = new Point(100, bottom - 42);
            _btnAddProp.Location   = new Point(6,   bottom - 42);
            _dgvProps.Height       = bottom - _dgvProps.Top - 60;
            _dgvProps.Width        = this.ClientSize.Width - _dgvProps.Left - 14;
        }

        // ── Helper builders ────────────────────────────────────────────────────

        private TextBox AddLabeledTextBox(string labelText, int inputX, int y,
            int inputW, ref int rowY, int rowH, bool readOnly = false)
        {
            AddLabel(labelText, inputX - 70, y, 68);
            var tb = new TextBox
            {
                Location   = new Point(inputX, y),
                Width      = inputW,
                ReadOnly   = readOnly,
                BackColor  = readOnly ? SystemColors.Control : SystemColors.Window,
                Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(tb);
            rowY += rowH;
            return tb;
        }

        private void AddLabel(string text, int x, int y, int width)
        {
            this.Controls.Add(new Label
            {
                Text      = text,
                Location  = new Point(x, y + 4),
                Size      = new Size(width, 20),
                TextAlign = ContentAlignment.MiddleRight
            });
        }

        // ── Data operations ────────────────────────────────────────────────────

        private void PopulateForm()
        {
            _txtId.Text    = CheckItem.Id;
            _txtName.Text  = CheckItem.Name;
            _txtDesc.Text  = CheckItem.Description;
            _chkEnabled.Checked = CheckItem.IsEnabled;

            // Select matching plugin in combobox
            if (!string.IsNullOrEmpty(CheckItem.PluginName))
            {
                for (int i = 0; i < _cmbPlugin.Items.Count; i++)
                {
                    if (((PluginEntry)_cmbPlugin.Items[i]).Name == CheckItem.PluginName)
                    {
                        _cmbPlugin.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Populate properties grid
            foreach (var kv in CheckItem.Properties)
                _dgvProps.Rows.Add(kv.Key, kv.Value);
        }

        // ── Button handlers ────────────────────────────────────────────────────

        private void BtnAddProp_Click(object sender, EventArgs e)
        {
            int rowIdx = _dgvProps.Rows.Add("", "");
            _dgvProps.CurrentCell = _dgvProps.Rows[rowIdx].Cells["Key"];
            _dgvProps.BeginEdit(true);
        }

        private void BtnRemoveProp_Click(object sender, EventArgs e)
        {
            if (_dgvProps.SelectedRows.Count > 0)
                _dgvProps.Rows.Remove(_dgvProps.SelectedRows[0]);
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            // ── Validation ────────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("請輸入「名稱」。", "必填欄位",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                DialogResult = DialogResult.None;
                return;
            }

            if (_cmbPlugin.SelectedItem == null)
            {
                MessageBox.Show("請選擇「外掛」。", "必填欄位",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _cmbPlugin.Focus();
                DialogResult = DialogResult.None;
                return;
            }

            // ── Commit edit to grid before reading values ─────────────────────
            _dgvProps.EndEdit();

            // ── Write back to CheckItem ───────────────────────────────────────
            CheckItem.Name        = _txtName.Text.Trim();
            CheckItem.Description = _txtDesc.Text.Trim();
            CheckItem.IsEnabled   = _chkEnabled.Checked;
            CheckItem.PluginName  = ((PluginEntry)_cmbPlugin.SelectedItem).Name;

            CheckItem.Properties.Clear();
            foreach (DataGridViewRow row in _dgvProps.Rows)
            {
                var key = row.Cells["Key"].Value?.ToString()?.Trim();
                var val = row.Cells["Value"].Value?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(key))
                    CheckItem.Properties[key] = val;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static CheckItem DeepClone(CheckItem src)
            => new CheckItem
            {
                Id          = src.Id,
                Name        = src.Name,
                Description = src.Description,
                IsEnabled   = src.IsEnabled,
                PluginName  = src.PluginName,
                Properties  = new Dictionary<string, string>(src.Properties)
            };

        // ── Inner types ────────────────────────────────────────────────────────

        private class PluginEntry
        {
            public string Name    { get; }
            public string Display { get; }
            public PluginEntry(string name, string display)
            {
                Name    = name;
                Display = display;
            }
            public override string ToString() => Display;
        }
    }
}
