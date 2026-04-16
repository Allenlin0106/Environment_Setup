using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.UI.Forms
{
    /// <summary>
    /// 設定管理視窗（Presentation 層）。
    /// 允許使用者新增、編輯、刪除檢查項目，並儲存回設定檔，
    /// 實現需求 5「可以透過介面調整設定值內容」。
    /// </summary>
    public class ConfigurationForm : Form
    {
        private readonly IConfigurationService _configService;
        private readonly IPluginManager        _pluginManager;
        private ComplianceConfig               _config;

        // ── Controls ───────────────────────────────────────────────────────────
        private DataGridView _grid;
        private Button       _btnAdd;
        private Button       _btnEdit;
        private Button       _btnDelete;
        private Button       _btnMoveUp;
        private Button       _btnMoveDown;
        private Button       _btnSave;
        private Button       _btnCancel;
        private Label        _lblInfo;

        public ConfigurationForm(
            IConfigurationService configService,
            IPluginManager        pluginManager)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));

            InitializeComponent();
            LoadConfig();
        }

        // ── UI Initialisation ──────────────────────────────────────────────────

        private void InitializeComponent()
        {
            this.Text            = "管理檢查項目";
            this.Size            = new Size(960, 600);
            this.MinimumSize     = new Size(720, 450);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;

            // ── Grid ──────────────────────────────────────────────────────────
            _grid = new DataGridView
            {
                Location              = new Point(10, 40),
                Anchor                = AnchorStyles.Top | AnchorStyles.Bottom
                                      | AnchorStyles.Left | AnchorStyles.Right,
                Width                 = this.ClientSize.Width - 120,
                Height                = this.ClientSize.Height - 100,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible     = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                MultiSelect           = false,
                BackgroundColor       = SystemColors.Window,
                BorderStyle           = BorderStyle.Fixed3D
            };
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
                { Name = "ColEnabled", HeaderText = "啟用", Width = 50, ReadOnly = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ColPlugin",  HeaderText = "外掛",     Width = 145 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ColName",    HeaderText = "名稱",     Width = 200 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ColDesc",    HeaderText = "說明",     FillWeight = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ColId",      HeaderText = "ID",       Visible = false });

            _grid.CellDoubleClick += (s, e) => EditSelected();
            _grid.CellValueChanged += Grid_CellValueChanged;
            _grid.CurrentCellDirtyStateChanged += Grid_DirtyStateChanged;

            // ── Info label ────────────────────────────────────────────────────
            _lblInfo = new Label
            {
                Location  = new Point(10, 12),
                Size      = new Size(700, 20),
                Text      = "雙擊列或點選「編輯」以修改檢查項目；勾選「啟用」欄可直接切換狀態。",
                ForeColor = Color.DimGray,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // ── Side buttons ──────────────────────────────────────────────────
            int bx = this.ClientSize.Width - 100;
            int by = 40;
            const int bw = 84, bh = 30, gap = 8;

            _btnAdd      = MakeButton("新增",     bx, by,              BtnAdd_Click);
            _btnEdit     = MakeButton("編輯",     bx, by += bh + gap,  BtnEdit_Click);
            _btnDelete   = MakeButton("刪除",     bx, by += bh + gap,  BtnDelete_Click);
            _btnMoveUp   = MakeButton("上移",     bx, by += bh + gap * 3, BtnMoveUp_Click);
            _btnMoveDown = MakeButton("下移",     bx, by += bh + gap,  BtnMoveDown_Click);

            // All side buttons anchor right
            foreach (Control c in new Control[]
                { _btnAdd, _btnEdit, _btnDelete, _btnMoveUp, _btnMoveDown })
                c.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            // ── Bottom buttons ────────────────────────────────────────────────
            _btnSave   = new Button
            {
                Text         = "儲存(&S)",
                Size         = new Size(90, 30),
                Anchor       = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.OK
            };
            _btnSave.Click += BtnSave_Click;

            _btnCancel = new Button
            {
                Text         = "取消(&C)",
                Size         = new Size(90, 30),
                Anchor       = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };

            this.Resize += (s, e) => LayoutBottomButtons();
            LayoutBottomButtons();

            this.Controls.AddRange(new Control[]
            {
                _lblInfo, _grid,
                _btnAdd, _btnEdit, _btnDelete, _btnMoveUp, _btnMoveDown,
                _btnSave, _btnCancel
            });

            this.AcceptButton = _btnSave;
            this.CancelButton = _btnCancel;
        }

        private void LayoutBottomButtons()
        {
            _btnSave.Location   = new Point(this.ClientSize.Width - 194,
                                            this.ClientSize.Height - 44);
            _btnCancel.Location = new Point(this.ClientSize.Width - 98,
                                            this.ClientSize.Height - 44);

            // Resize side buttons column and grid
            int rightEdge = this.ClientSize.Width - 10;
            int bx = rightEdge - 84;
            _btnAdd.Left      = bx;
            _btnEdit.Left     = bx;
            _btnDelete.Left   = bx;
            _btnMoveUp.Left   = bx;
            _btnMoveDown.Left = bx;

            _grid.Width  = bx - 18;
            _grid.Height = this.ClientSize.Height - 100;
        }

        private Button MakeButton(string text, int x, int y, EventHandler handler)
        {
            var btn = new Button { Text = text, Location = new Point(x, y), Size = new Size(84, 30) };
            btn.Click += handler;
            return btn;
        }

        // ── Data operations ────────────────────────────────────────────────────

        private void LoadConfig()
        {
            _config = _configService.LoadConfiguration();
            RepopulateGrid();
        }

        private void RepopulateGrid()
        {
            _grid.Rows.Clear();
            foreach (var item in _config.CheckItems)
            {
                _grid.Rows.Add(item.IsEnabled, item.PluginName, item.Name,
                               item.Description, item.Id);
            }
        }

        private void Grid_DirtyStateChanged(object sender, EventArgs e)
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void Grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "ColEnabled")
                return;

            var id   = _grid.Rows[e.RowIndex].Cells["ColId"].Value?.ToString();
            var item = _config.CheckItems.FirstOrDefault(i => i.Id == id);
            if (item != null)
                item.IsEnabled = (bool)(_grid.Rows[e.RowIndex].Cells["ColEnabled"].Value ?? false);
        }

        private void EditSelected()
        {
            var item = GetSelectedItem();
            if (item == null) return;

            using (var form = new CheckItemEditForm(item, _pluginManager))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                var idx = _config.CheckItems.IndexOf(item);
                _config.CheckItems[idx] = form.CheckItem;
                RepopulateGrid();
                SelectRowById(form.CheckItem.Id);
            }
        }

        // ── Button handlers ────────────────────────────────────────────────────

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var form = new CheckItemEditForm(null, _pluginManager))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _config.CheckItems.Add(form.CheckItem);
                RepopulateGrid();
                SelectRowById(form.CheckItem.Id);
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e) => EditSelected();

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            var item = GetSelectedItem();
            if (item == null) return;

            if (MessageBox.Show(
                    $"確定要刪除檢查項目「{item.Name}」嗎？",
                    "確認刪除",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _config.CheckItems.Remove(item);
            RepopulateGrid();
        }

        private void BtnMoveUp_Click(object sender, EventArgs e)
        {
            var item = GetSelectedItem();
            if (item == null) return;
            var idx = _config.CheckItems.IndexOf(item);
            if (idx <= 0) return;
            _config.CheckItems.RemoveAt(idx);
            _config.CheckItems.Insert(idx - 1, item);
            RepopulateGrid();
            SelectRowById(item.Id);
        }

        private void BtnMoveDown_Click(object sender, EventArgs e)
        {
            var item = GetSelectedItem();
            if (item == null) return;
            var idx = _config.CheckItems.IndexOf(item);
            if (idx >= _config.CheckItems.Count - 1) return;
            _config.CheckItems.RemoveAt(idx);
            _config.CheckItems.Insert(idx + 1, item);
            RepopulateGrid();
            SelectRowById(item.Id);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                _configService.SaveConfiguration(_config);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"儲存設定失敗:\n{ex.Message}",
                    "儲存錯誤",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private CheckItem GetSelectedItem()
        {
            if (_grid.SelectedRows.Count == 0) return null;
            var id = _grid.SelectedRows[0].Cells["ColId"].Value?.ToString();
            return _config.CheckItems.FirstOrDefault(i => i.Id == id);
        }

        private void SelectRowById(string id)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells["ColId"].Value?.ToString() == id)
                {
                    row.Selected = true;
                    _grid.FirstDisplayedScrollingRowIndex = row.Index;
                    break;
                }
            }
        }
    }
}
