using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.UI.Forms
{
    /// <summary>
    /// 主視窗（Presentation 層）。
    /// 提供樹狀結構覽覽檢查項目、執行合規檢查、檢視結果彙總等功能。
    /// 所有業務邏輯均委由注入的服務層介面處理，視窗只負責 UI 互動。
    /// </summary>
    public class MainForm : Form
    {
        private readonly IComplianceCheckService _checkService;
        private readonly IConfigurationService   _configService;
        private readonly IPluginManager          _pluginManager;

        // ── Controls ───────────────────────────────────────────────────────────
        private MenuStrip           _menuStrip;
        private ToolStrip           _toolStrip;
        private SplitContainer      _split;
        private TreeView            _tree;
        private DataGridView        _grid;
        private StatusStrip         _status;
        private ToolStripStatusLabel _statusLabel;
        private ToolStripProgressBar _progressBar;

        public MainForm(
            IComplianceCheckService checkService,
            IConfigurationService   configService,
            IPluginManager          pluginManager)
        {
            _checkService  = checkService  ?? throw new ArgumentNullException(nameof(checkService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));

            InitializeComponent();
            RefreshTree();
        }

        // ── UI Initialisation ──────────────────────────────────────────────────

        private void InitializeComponent()
        {
            this.Text            = "Windows 合規檢查程式";
            this.Size            = new Size(1200, 750);
            this.MinimumSize     = new Size(900, 550);
            this.StartPosition   = FormStartPosition.CenterScreen;

            BuildMenu();
            BuildToolbar();
            BuildSplitter();
            BuildStatusBar();

            this.Controls.Add(_split);
            this.Controls.Add(_toolStrip);
            this.Controls.Add(_menuStrip);
            this.Controls.Add(_status);
            this.MainMenuStrip = _menuStrip;
        }

        private void BuildMenu()
        {
            _menuStrip = new MenuStrip();

            var fileMenu = new ToolStripMenuItem("檔案(&F)");
            fileMenu.DropDownItems.Add("以記事本開啟設定檔(&O)",
                null, (s, e) => OpenConfigInNotepad());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("結束(&X)",
                null, (s, e) => Application.Exit());

            var checkMenu = new ToolStripMenuItem("檢查(&C)");
            checkMenu.DropDownItems.Add("執行所有檢查(&A)",
                null, (s, e) => RunAllChecks());
            checkMenu.DropDownItems.Add("重新整理外掛(&R)",
                null, (s, e) => { RefreshTree(); SetStatus("外掛已重新載入"); });
            checkMenu.DropDownItems.Add("清除結果(&L)",
                null, (s, e) => ClearResults());

            var configMenu = new ToolStripMenuItem("設定(&S)");
            configMenu.DropDownItems.Add("管理檢查項目(&M)",
                null, (s, e) => OpenConfiguration());

            var helpMenu = new ToolStripMenuItem("說明(&H)");
            helpMenu.DropDownItems.Add("關於(&A)",
                null, (s, e) => ShowAbout());

            _menuStrip.Items.AddRange(
                new ToolStripItem[] { fileMenu, checkMenu, configMenu, helpMenu });
        }

        private void BuildToolbar()
        {
            _toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };

            var btnRun = new ToolStripButton("執行所有檢查")
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                Font         = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnRun.Click += (s, e) => RunAllChecks();

            var btnCfg = new ToolStripButton("管理設定")
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            btnCfg.Click += (s, e) => OpenConfiguration();

            var btnClear = new ToolStripButton("清除結果")
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            btnClear.Click += (s, e) => ClearResults();

            _toolStrip.Items.AddRange(new ToolStripItem[]
            {
                btnRun,
                new ToolStripSeparator(),
                btnCfg,
                btnClear
            });
        }

        private void BuildSplitter()
        {
            _split = new SplitContainer
            {
                Dock              = DockStyle.Fill,
                SplitterDistance  = 260,
                Panel1MinSize     = 160,
                Panel2MinSize     = 400
            };

            // Left: Tree
            _tree = new TreeView
            {
                Dock          = DockStyle.Fill,
                HideSelection = false,
                ShowLines     = true,
                ShowPlusMinus = true
            };
            _tree.AfterSelect += Tree_AfterSelect;
            _split.Panel1.Controls.Add(_tree);

            // Right: Grid
            _grid = new DataGridView
            {
                Dock                             = DockStyle.Fill,
                ReadOnly                         = true,
                AllowUserToAddRows               = false,
                AllowUserToDeleteRows            = false,
                AutoSizeColumnsMode              = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode                    = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible                = false,
                BackgroundColor                  = SystemColors.Window,
                BorderStyle                      = BorderStyle.None,
                AlternatingRowsDefaultCellStyle  =
                    new DataGridViewCellStyle { BackColor = Color.FromArgb(245, 248, 255) }
            };
            ConfigureGrid();
            _split.Panel2.Controls.Add(_grid);
        }

        private void ConfigureGrid()
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ColStatus",   HeaderText = "狀態",     Width = 70  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ColPlugin",   HeaderText = "外掛",     Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ColName",     HeaderText = "檢查項目", Width = 200 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ColExpected", HeaderText = "期望值",   Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ColActual",   HeaderText = "實際值",   Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ColMessage",  HeaderText = "說明",     FillWeight = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ColTime",     HeaderText = "檢查時間", Width = 145 });

            _grid.CellFormatting += Grid_CellFormatting;
        }

        private void BuildStatusBar()
        {
            _status      = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("就緒")
            {
                Spring    = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _progressBar = new ToolStripProgressBar
            {
                Visible = false,
                Width   = 160,
                Style   = ProgressBarStyle.Marquee
            };
            _status.Items.AddRange(new ToolStripItem[] { _statusLabel, _progressBar });
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        private void Tree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // 未來可依樹狀選取篩選 Grid 結果
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_grid.Columns[e.ColumnIndex].Name != "ColStatus" || e.RowIndex < 0)
                return;

            var statusRaw = _grid.Rows[e.RowIndex].Cells["ColStatus"].Value?.ToString();
            Color fore;
            string label;

            switch (statusRaw)
            {
                case "Pass":
                    fore = Color.DarkGreen; label = "通過"; break;
                case "Fail":
                    fore = Color.Crimson;   label = "失敗"; break;
                case "Warning":
                    fore = Color.DarkOrange; label = "警告"; break;
                case "Error":
                    fore = Color.DarkRed;   label = "錯誤"; break;
                case "Skipped":
                    fore = Color.Gray;      label = "略過"; break;
                default:
                    return;
            }

            e.CellStyle.ForeColor      = fore;
            e.CellStyle.SelectionForeColor = fore;
            e.Value                    = label;
            e.FormattingApplied        = true;
        }

        // ── Business actions ───────────────────────────────────────────────────

        private void RunAllChecks()
        {
            SetStatus("正在執行合規檢查...", busy: true);

            try
            {
                var results = _checkService.RunAllChecks().ToList();
                PopulateGrid(results);

                int pass    = results.Count(r => r.Status == CheckStatus.Pass);
                int fail    = results.Count(r => r.Status == CheckStatus.Fail);
                int warning = results.Count(r => r.Status == CheckStatus.Warning);
                int error   = results.Count(r => r.Status == CheckStatus.Error);

                SetStatus($"檢查完成 — 通過: {pass}  失敗: {fail}  " +
                          $"警告: {warning}  錯誤: {error}  共 {results.Count} 項");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"執行合規檢查時發生錯誤:\n\n{ex.Message}",
                    "執行錯誤",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                SetStatus("檢查失敗");
            }
        }

        private void PopulateGrid(List<CheckResult> results)
        {
            _grid.Rows.Clear();

            foreach (var r in results)
            {
                var rowIdx = _grid.Rows.Add(
                    r.Status.ToString(),
                    r.PluginName,
                    r.CheckItemName,
                    r.ExpectedValue,
                    r.ActualValue,
                    r.Message,
                    r.CheckedAt.ToString("yyyy/MM/dd HH:mm:ss"));

                var bg = RowBackground(r.Status);
                _grid.Rows[rowIdx].DefaultCellStyle.BackColor = bg;
                _grid.Rows[rowIdx].DefaultCellStyle.SelectionBackColor =
                    ControlPaint.Dark(bg, 0.06f);
            }
        }

        private Color RowBackground(CheckStatus status)
        {
            switch (status)
            {
                case CheckStatus.Pass:    return Color.FromArgb(232, 255, 232);
                case CheckStatus.Fail:    return Color.FromArgb(255, 232, 232);
                case CheckStatus.Warning: return Color.FromArgb(255, 248, 220);
                case CheckStatus.Error:   return Color.FromArgb(255, 232, 240);
                default:                  return SystemColors.Window;
            }
        }

        private void ClearResults()
        {
            _grid.Rows.Clear();
            SetStatus("就緒");
        }

        private void OpenConfiguration()
        {
            using (var form = new ConfigurationForm(_configService, _pluginManager))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    RefreshTree();
                    SetStatus("設定已儲存並更新");
                }
            }
        }

        private void RefreshTree()
        {
            _tree.Nodes.Clear();

            var config  = _configService.LoadConfiguration();
            var plugins = _pluginManager.GetAllPlugins().ToList();

            var root = new TreeNode("所有檢查項目") { Tag = "all" };
            _tree.Nodes.Add(root);

            var grouped = config.CheckItems
                .Where(i => i.IsEnabled)
                .GroupBy(i => i.PluginName);

            foreach (var group in grouped)
            {
                var plugin   = plugins.FirstOrDefault(p => p.PluginName == group.Key);
                var display  = plugin != null ? plugin.DisplayName : group.Key;
                var pNode    = new TreeNode(display) { Tag = group.Key };

                foreach (var item in group)
                    pNode.Nodes.Add(new TreeNode(item.Name) { Tag = item.Id });

                root.Nodes.Add(pNode);
            }

            _tree.ExpandAll();
        }

        private void OpenConfigInNotepad()
        {
            try
            {
                System.Diagnostics.Process.Start("notepad.exe",
                    _configService.ConfigurationFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"無法開啟設定檔:\n{ex.Message}",
                    "錯誤",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "Windows 合規檢查程式  v1.0\n\n" +
                "架構：三層式 + Plugin Architecture + Dependency Injection\n" +
                "開發工具：Visual Studio 2017 / .NET Framework 4.8 / C#\n\n" +
                "支援的外掛類型：\n" +
                "  • GroupPolicyPlugin  — 群組原則檢查\n" +
                "  • RegistryPlugin     — 登錄檔檢查\n" +
                "  • LocalAccountPlugin — 本機帳號檢查\n\n" +
                "外掛 DLL 請放置於 Plugins 目錄，程式啟動時自動載入。",
                "關於",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void SetStatus(string text, bool busy = false)
        {
            _statusLabel.Text    = text;
            _progressBar.Visible = busy;
        }
    }
}
