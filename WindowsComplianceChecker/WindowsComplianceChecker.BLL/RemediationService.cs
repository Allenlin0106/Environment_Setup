using System;
using System.IO;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.BLL
{
    /// <summary>
    /// 修復服務實作（BLL 層）。
    /// 透過 IPluginManager 取得對應外掛，並轉呼叫 IRemediationPlugin.Remediate。
    /// </summary>
    public class RemediationService : IRemediationService
    {
        private readonly IPluginManager       _pluginManager;
        private readonly IConfigurationService _configService;

        public RemediationService(
            IPluginManager       pluginManager,
            IConfigurationService configService)
        {
            _pluginManager = pluginManager
                ?? throw new ArgumentNullException(nameof(pluginManager));
            _configService = configService
                ?? throw new ArgumentNullException(nameof(configService));
        }

        public bool CanRemediate(CheckItem item)
        {
            if (item == null) return false;
            EnsurePluginsLoaded();
            var plugin = _pluginManager.GetPlugin(item.PluginName);
            var rem    = plugin as IRemediationPlugin;
            return rem != null && rem.CanRemediate(item);
        }

        public RemediationResult RemediateItem(CheckItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            EnsurePluginsLoaded();

            var plugin = _pluginManager.GetPlugin(item.PluginName);
            if (plugin == null)
            {
                return new RemediationResult
                {
                    CheckItemId   = item.Id,
                    CheckItemName = item.Name,
                    PluginName    = item.PluginName,
                    Success       = false,
                    Message       = $"找不到外掛 '{item.PluginName}'，請確認 DLL 已放置於 Plugins 目錄。",
                    RemediatedAt  = DateTime.Now
                };
            }

            var rem = plugin as IRemediationPlugin;
            if (rem == null)
            {
                return new RemediationResult
                {
                    CheckItemId   = item.Id,
                    CheckItemName = item.Name,
                    PluginName    = item.PluginName,
                    Success       = false,
                    Message       = $"外掛 '{plugin.DisplayName}' 未實作修復功能。",
                    RemediatedAt  = DateTime.Now
                };
            }

            return rem.Remediate(item);
        }

        private void EnsurePluginsLoaded()
        {
            var config    = _configService.LoadConfiguration();
            var pluginDir = Path.IsPathRooted(config.PluginDirectory)
                ? config.PluginDirectory
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.PluginDirectory);
            _pluginManager.LoadPlugins(pluginDir);
        }
    }
}
