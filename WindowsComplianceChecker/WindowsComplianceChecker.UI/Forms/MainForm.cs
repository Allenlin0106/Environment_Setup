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
    /// 提供執行合規檢查、檢視結果，以及對失敗項目執行「修復」的功能。
    /// 所有業務邏輯均委由注入的服務層介面處理，視窗只負責 UI 互動。
    /// </summary>
    public class MainForm : Form
    {
        private readonly IComplianceCheckService _checkService;
        private readonly IConfigurationService   _configService;
        private readonly IPluginManager          _pluginManager;
        private readonly IRemediationService     _remediationService;

        // ── Controls ───────────────────────────────────────────────────────────
        private MenuStrip            _menuStrip;
        private ToolStrip            _toolStrip;
        private ToolStripButton      _btnRemediate;
        private SplitContainer       _split;
        private TreeView             _tree;
        private DataGridView         _grid;
        private StatusStrip          _status;
        private ToolStripStatusLabel _statusLabel;
        private ToolStripProgressBar _progressBar;

        public MainForm(
            IComplianceCheckService checkService,
            IConfigurationService   configService,
            IPluginManager          pluginManager,
            IRemediationService     remediationService)
        {
            _checkService       = checkService       ?? throw new ArgumentNullException(nameof(checkService));
            _configService      = configService      ?? throw new ArgumentNullException(nameof(configService));
            _pluginManager      = pluginManager      ?? throw new ArgumentNullException(nameof(pluginManager));
            _remediationService = remediationService ?? throw new ArgumentNullException(nameof(remediationService));

            InitializeComponent();
            RefreshTree();
        }

        // ── UI Initialisation ──────────────────────────────────────────────────

        private void InitializeComponent()
        {
            this.Text          = "Windows 合規檢查程式";
            this.Size          = new Size(1200, 750);
            this.MinimumSize   = new Size(900, 550);
            this.StartPosition = FormStartPosition.CenterScreen;

            BuildMenu();
            BuildToolbar();
            BuildSplitter();
            BuildStatusBar();

            this.Controls.Add(_split);
            this.Controls.Add(_toolStrip);
            this.Controls.Add(_menuStrip);
            this.Controls.Add(_status);
            this.MainMenuStrip = _menuStrip;

            this.Load += (s, e) =>
            {
                _split.Panel1MinSize = 160;
                _split.Panel2MinSize = 200;
                int available = _split.Width - _split.Panel2MinSize - _split.SplitterWidth;
                if (available > _split.Panel1MinSize)
                    _split.SplitterDistance = Math.Min(260, available);
            };
        }

        private void BuildMenu()
        {
            _menuStrip = new MenuStrip();

            var fileMenu = new ToolStripMenuItem("檔案(&F)");
            fileMenu.DropDownItems.Add("以記事本開啟設定檔(&O)",
                null, (s, e) => OpenConfigInNotepad());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("結束(&X)", null, (s, e) => Application.Exit());

            var checkMenu = new ToolStripMenuItem("檢查(&C)");
            checkMenu.DropDownItems.Add("執行所有檢查(&A)",
                null, (s, e) => RunAllChecks());
            checkMenu.DropDownItems.Add("重新整理外掛(&R)",
                null, (s, e) => { RefreshTree(); SetStatus("外掛已重新載入"); });
            checkMenu.DropDownItems.Add("清除結果(&L)",
                null, (s, e) => ClearResults());

            var remMenu = new ToolStripMenuItem("修復(&R)");
            remMenu.DropDownItems.Add("修復選取項目(&F)",
                null, (s, e) => RemediateSelected());
            remMenu.DropDownItems.Add("修復所有失敗項目(&A)",
                null, (s, e) => RemediateAllFailed());

            var configMenu = new ToolStripMenuItem("設定(&S)");
            configMenu.DropDownItems.Add("管理檢查項目(&M)",
                null, (s, e) => OpenConfiguration());

            var helpMenu = new ToolStripMenuItem("說明(&H)");
            helpMenu.DropDownItems.Add("關於(&A)", null, (s, e) => ShowAbout());

            _menuStrip.Items.AddRange(
                new ToolStripItem[] { fileMenu, checkMenu, remMenu, configMenu, helpMenu });
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

            _btnRemediate = new ToolStripButton("修復選取項目")
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                Enabled      = false,
                ToolTipText  = "對選取的失敗項目執行自動修復（需管理員權限）"
            };
            _btnRemediate.Click += (s, e) => RemediateSelected();

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
                _btnRemediate,
                new ToolStripSeparator(),
                btnCfg,
                btnClear
            });
        }

        private void BuildSplitter()
        {
            _split = new SplitContainer { Dock = DockStyle.Fill };

            _tree = new TreeView
            {
                Dock          = DockStyle.Fill,
                HideSelection = false,
                ShowLines     = true,
                ShowPlusMinus = true
            };
            _tree.AfterSelect += Tree_AfterSelect;
            _split.Panel1.Controls.Add(_tree);

            _grid = new DataGridView
            {
                Dock                            = DockStyle.Fill,
                ReadOnly                        = true,
                AllowUserToAddRows              = false,
                AllowUserToDeleteRows           = false,
                AutoSizeColumnsMode             = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode                   = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible               = false,
                BackgroundColor                 = SystemColors.Window,
                BorderStyle                     = BorderStyle.None,
                AlternatingRowsDefaultCellStyle =
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

            // 隱藏欄位，用於對應回 CheckItem.Id（修復時需要）
            _grid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ColItemId", Visible = false });

            _grid.CellFormatting   += Grid_CellFormatting;
            _grid.SelectionChanged += Grid_SelectionChanged;
            _grid.ContextMenuStrip  = BuildContextMenu();
        }

        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            var miRemediate = new ToolStripMenuItem("修復此項目(&F)");
            miRemediate.Click += (s, e) => RemediateSelected();

            var miRecheck = new ToolStripMenuItem("重新驗證此項目(&V)");
            miRecheck.Click += (s, e) => RecheckSelected();

            menu.Items.AddRange(new ToolStripItem[]
            {
                miRemediate,
                miRecheck
            });

            // 在顯示前更新項目的啟用狀態
            menu.Opening += (s, e) =>
            {
                miRemediate.Enabled = CanRemediateSelected();
                miRecheck.Enabled   = _grid.SelectedRows.Count > 0;
            };

            return menu;
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

        private void Tree_AfterSelect(object sender, TreeViewEventArgs e) { }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_grid.Columns[e.ColumnIndex].Name != "ColStatus" || e.RowIndex < 0)
                return;

            var raw = _grid.Rows[e.RowIndex].Cells["ColStatus"].Value?.ToString();
            Color fore; string label;

            switch (raw)
            {
                case "Pass":    fore = Color.DarkGreen;  label = "通過"; break;
                case "Fail":    fore = Color.Crimson;    label = "失敗"; break;
                case "Warning": fore = Color.DarkOrange; label = "警告"; break;
                case "Error":   fore = Color.DarkRed;    label = "錯誤"; break;
                case "Skipped": fore = Color.Gray;       label = "略過"; break;
                default: return;
            }

            e.CellStyle.ForeColor          = fore;
            e.CellStyle.SelectionForeColor = fore;
            e.Value                        = label;
            e.FormattingApplied            = true;
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            _btnRemediate.Enabled = CanRemediateSelected();
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
                MessageBox.Show($"執行合規檢查時發生錯誤:\n\n{ex.Message}",
                    "執行錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("檢查失敗");
            }
        }

        // ── 修復：單一項目 ─────────────────────────────────────────────────────

        private void RemediateSelected()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var row    = _grid.SelectedRows[0];
            var itemId = row.Cells["ColItemId"].Value?.ToString();

            var config = _configService.LoadConfiguration();
            var item   = config.CheckItems.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return;

            if (!ConfirmRemediation(item, row)) return;

            SetStatus("正在修復...", busy: true);
            try
            {
                var remResult = _remediationService.RemediateItem(item);

                if (remResult.Success)
                {
                    // 修復後立即重新驗證並更新列
                    var checkResults = _checkService.RunChecksForItems(new[] { item }).ToList();
                    if (checkResults.Count > 0)
                        ApplyCheckResultToRow(row, checkResults[0]);

                    SetStatus($"修復完成：{item.Name}");
                    MessageBox.Show(remResult.Message, "修復成功",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    SetStatus("修復失敗");
                    MessageBox.Show(remResult.Message, "修復失敗",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                SetStatus("修復失敗");
                MessageBox.Show($"執行修復時發生未預期錯誤：\n{ex.Message}",
                    "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── 修復：所有失敗項目 ─────────────────────────────────────────────────

        private void RemediateAllFailed()
        {
            // 收集目前 Grid 中所有 Fail 且可修復的項目
            var failedRows = new List<(DataGridViewRow Row, CheckItem Item)>();
            var config     = _configService.LoadConfiguration();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                var status = row.Cells["ColStatus"].Value?.ToString();
                if (status != "Fail") continue;

                var itemId = row.Cells["ColItemId"].Value?.ToString();
                var item   = config.CheckItems.FirstOrDefault(i => i.Id == itemId);
                if (item == null) continue;

                if (_remediationService.CanRemediate(item))
                    failedRows.Add((row, item));
            }

            if (failedRows.Count == 0)
            {
                MessageBox.Show("目前沒有可自動修復的失敗項目。",
                    "無可修復項目", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var names = string.Join("\n  • ", failedRows.Select(x => x.Item.Name));
            if (MessageBox.Show(
                    $"確定要修復以下 {failedRows.Count} 個失敗項目嗎？\n\n  • {names}\n\n" +
                    "⚠ 此操作將修改系統設定，需要管理員權限。",
                    "確認批次修復",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            SetStatus($"正在批次修復 {failedRows.Count} 個項目...", busy: true);

            int success = 0, failed = 0;
            foreach (var (row, item) in failedRows)
            {
                try
                {
                    var remResult = _remediationService.RemediateItem(item);
                    if (remResult.Success)
                    {
                        var checkResults = _checkService.RunChecksForItems(new[] { item }).ToList();
                        if (checkResults.Count > 0)
                            ApplyCheckResultToRow(row, checkResults[0]);
                        success++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            SetStatus($"批次修復完成 — 成功: {success}，失敗: {failed}");
            MessageBox.Show(
                $"批次修復完成。\n\n成功: {success} 項\n失敗: {failed} 項",
                "批次修復結果",
                MessageBoxButtons.OK,
                failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        // ── 重新驗證單一項目 ──────────────────────────────────────────────────

        private void RecheckSelected()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var row    = _grid.SelectedRows[0];
            var itemId = row.Cells["ColItemId"].Value?.ToString();

            var config = _configService.LoadConfiguration();
            var item   = config.CheckItems.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return;

            SetStatus($"正在重新驗證：{item.Name}...", busy: true);
            try
            {
                var results = _checkService.RunChecksForItems(new[] { item }).ToList();
                if (results.Count > 0)
                    ApplyCheckResultToRow(row, results[0]);
                SetStatus($"重新驗證完成：{item.Name}");
            }
            catch (Exception ex)
            {
                SetStatus("重新驗證失敗");
                MessageBox.Show($"重新驗證時發生錯誤：\n{ex.Message}",
                    "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Grid helpers ───────────────────────────────────────────────────────

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
                    r.CheckedAt.ToString("yyyy/MM/dd HH:mm:ss"),
                    r.CheckItemId);   // ColItemId（隱藏）

                var bg = RowBackground(r.Status);
                _grid.Rows[rowIdx].DefaultCellStyle.BackColor          = bg;
                _grid.Rows[rowIdx].DefaultCellStyle.SelectionBackColor =
                    ControlPaint.Dark(bg, 0.06f);
            }
        }

        /// <summary>修復或重新驗證後，就地更新 Grid 中單列的顯示</summary>
        private void ApplyCheckResultToRow(DataGridViewRow row, CheckResult r)
        {
            row.Cells["ColStatus"].Value   = r.Status.ToString();
            row.Cells["ColExpected"].Value = r.ExpectedValue;
            row.Cells["ColActual"].Value   = r.ActualValue;
            row.Cells["ColMessage"].Value  = r.Message;
            row.Cells["ColTime"].Value     = r.CheckedAt.ToString("yyyy/MM/dd HH:mm:ss");

            var bg = RowBackground(r.Status);
            row.DefaultCellStyle.BackColor          = bg;
            row.DefaultCellStyle.SelectionBackColor = ControlPaint.Dark(bg, 0.06f);

            // 更新修復按鈕狀態（選取列狀態改變了）
            _btnRemediate.Enabled = CanRemediateSelected();
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

        // ── Remediation helpers ────────────────────────────────────────────────

        private bool CanRemediateSelected()
        {
            if (_grid.SelectedRows.Count == 0) return false;

            var row    = _grid.SelectedRows[0];
            var status = row.Cells["ColStatus"].Value?.ToString();

            // 只對失敗項目啟用修復
            if (status != "Fail") return false;

            var itemId = row.Cells["ColItemId"].Value?.ToString();
            if (string.IsNullOrEmpty(itemId)) return false;

            var config = _configService.LoadConfiguration();
            var item   = config.CheckItems.FirstOrDefault(i => i.Id == itemId);
            return item != null && _remediationService.CanRemediate(item);
        }

        private bool ConfirmRemediation(CheckItem item, DataGridViewRow row)
        {
            var actual   = row.Cells["ColActual"].Value?.ToString() ?? "-";
            var expected = row.Cells["ColExpected"].Value?.ToString() ?? "-";

            var msg =
                $"確定要修復「{item.Name}」嗎？\n\n" +
                $"  外掛：{item.PluginName}\n" +
                $"  目前值：{actual}\n" +
                $"  期望值：{expected}\n\n" +
                "⚠ 此操作將修改系統設定，通常需要系統管理員權限。";

            return MessageBox.Show(msg, "確認修復",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        // ── Other actions ──────────────────────────────────────────────────────

        private void ClearResults()
        {
            _grid.Rows.Clear();
            _btnRemediate.Enabled = false;
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

            foreach (var group in config.CheckItems.Where(i => i.IsEnabled).GroupBy(i => i.PluginName))
            {
                var plugin  = plugins.FirstOrDefault(p => p.PluginName == group.Key);
                var display = plugin != null ? plugin.DisplayName : group.Key;
                var pNode   = new TreeNode(display) { Tag = group.Key };
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
                MessageBox.Show($"無法開啟設定檔:\n{ex.Message}",
                    "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "Windows 合規檢查程式  v1.0\n\n" +
                "架構：三層式 + Plugin Architecture + Dependency Injection\n" +
                "開發工具：Visual Studio 2017 / .NET Framework 4.8 / C#\n\n" +
                "支援的外掛類型：\n" +
                "  • GroupPolicyPlugin  — 群組原則檢查 / 修復\n" +
                "  • RegistryPlugin     — 登錄檔檢查 / 修復\n" +
                "  • LocalAccountPlugin — 本機帳號檢查 / 修復（啟停用）\n\n" +
                "修復功能需要系統管理員權限。",
                "關於",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void SetStatus(string text, bool busy = false)
        {
            _statusLabel.Text    = text;
            _progressBar.Visible = busy;
        }
    }
}
