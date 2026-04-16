using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.BLL
{
    /// <summary>
    /// 合規檢查服務實作（BLL 層）。
    /// 協調 IConfigurationService 與 IPluginManager，
    /// 依設定檔內容驅動對應外掛執行合規檢查並彙總結果。
    /// 各外掛透過 DI 注入，程式碼只依賴介面，符合開放封閉原則 (OCP)。
    /// </summary>
    public class ComplianceCheckService : IComplianceCheckService
    {
        private readonly IPluginManager _pluginManager;
        private readonly IConfigurationService _configService;

        public ComplianceCheckService(
            IPluginManager pluginManager,
            IConfigurationService configService)
        {
            _pluginManager = pluginManager
                ?? throw new ArgumentNullException(nameof(pluginManager));
            _configService = configService
                ?? throw new ArgumentNullException(nameof(configService));
        }

        /// <summary>執行設定檔中所有已啟用的檢查項目</summary>
        public IEnumerable<CheckResult> RunAllChecks()
        {
            var config = _configService.LoadConfiguration();
            EnsurePluginsLoaded(config.PluginDirectory);
            return RunChecksForItems(config.CheckItems.Where(i => i.IsEnabled));
        }

        /// <summary>僅執行指定外掛的已啟用檢查項目</summary>
        public IEnumerable<CheckResult> RunChecksByPlugin(string pluginName)
        {
            var config = _configService.LoadConfiguration();
            EnsurePluginsLoaded(config.PluginDirectory);
            var items = config.CheckItems
                .Where(i => i.IsEnabled && i.PluginName == pluginName);
            return RunChecksForItems(items);
        }

        /// <summary>對任意指定的檢查項目清單執行檢查</summary>
        public IEnumerable<CheckResult> RunChecksForItems(IEnumerable<CheckItem> items)
        {
            var results = new List<CheckResult>();

            foreach (var item in items)
            {
                var plugin = _pluginManager.GetPlugin(item.PluginName);
                if (plugin == null)
                {
                    results.Add(new CheckResult
                    {
                        CheckItemId   = item.Id,
                        CheckItemName = item.Name,
                        PluginName    = item.PluginName,
                        Status        = CheckStatus.Error,
                        Message       = $"找不到外掛程式: '{item.PluginName}'，" +
                                        $"請確認 DLL 已放置於 Plugins 目錄。",
                        CheckedAt     = DateTime.Now
                    });
                    continue;
                }

                try
                {
                    var pluginResults = plugin.RunChecks(new[] { item });
                    results.AddRange(pluginResults);
                }
                catch (Exception ex)
                {
                    results.Add(new CheckResult
                    {
                        CheckItemId   = item.Id,
                        CheckItemName = item.Name,
                        PluginName    = item.PluginName,
                        Status        = CheckStatus.Error,
                        Message       = $"外掛執行時發生未預期錯誤: {ex.Message}",
                        CheckedAt     = DateTime.Now
                    });
                }
            }

            return results;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void EnsurePluginsLoaded(string configuredPluginDir)
        {
            var pluginDir = Path.IsPathRooted(configuredPluginDir)
                ? configuredPluginDir
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configuredPluginDir);

            _pluginManager.LoadPlugins(pluginDir);
        }
    }
}
