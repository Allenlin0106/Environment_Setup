using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WindowsComplianceChecker.Contracts.Interfaces;

namespace WindowsComplianceChecker.BLL
{
    /// <summary>
    /// 外掛管理器實作（BLL 層）。
    /// 掃描指定目錄中所有 DLL，透過反射找出實作 ICompliancePlugin 的類別，
    /// 動態載入並快取於記憶體，實現 Plugin Architecture。
    /// 新增或替換外掛只需更換對應的 DLL 檔案，無需修改主程式。
    /// </summary>
    public class PluginManager : IPluginManager
    {
        private readonly Dictionary<string, ICompliancePlugin> _plugins
            = new Dictionary<string, ICompliancePlugin>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 掃描並載入 pluginDirectory 下所有包含 ICompliancePlugin 實作的 DLL。
        /// 載入失敗的 DLL 會以 Debug 訊息記錄，不影響其他外掛的載入。
        /// </summary>
        public void LoadPlugins(string pluginDirectory)
        {
            _plugins.Clear();

            if (!Directory.Exists(pluginDirectory))
            {
                Directory.CreateDirectory(pluginDirectory);
                return;
            }

            foreach (var dllPath in Directory.GetFiles(pluginDirectory, "*.dll"))
            {
                TryLoadPluginsFromAssembly(dllPath);
            }
        }

        public IEnumerable<ICompliancePlugin> GetAllPlugins()
            => _plugins.Values.ToList();

        public ICompliancePlugin GetPlugin(string pluginName)
        {
            _plugins.TryGetValue(pluginName, out var plugin);
            return plugin;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void TryLoadPluginsFromAssembly(string dllPath)
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                var pluginType = typeof(ICompliancePlugin);

                var types = assembly.GetTypes()
                    .Where(t => pluginType.IsAssignableFrom(t)
                             && !t.IsInterface
                             && !t.IsAbstract);

                foreach (var type in types)
                {
                    try
                    {
                        var plugin = (ICompliancePlugin)Activator.CreateInstance(type);
                        _plugins[plugin.PluginName] = plugin;
                        System.Diagnostics.Debug.WriteLine(
                            $"[PluginManager] 已載入外掛: {plugin.DisplayName} v{plugin.Version}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[PluginManager] 無法建立外掛實例 '{type.FullName}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PluginManager] 無法載入 DLL '{dllPath}': {ex.Message}");
            }
        }
    }
}
