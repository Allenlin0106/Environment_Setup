using System;
using System.IO;
using System.Windows.Forms;
using WindowsComplianceChecker.BLL;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.DAL;
using WindowsComplianceChecker.UI.Forms;

namespace WindowsComplianceChecker.UI
{
    /// <summary>
    /// 應用程式進入點（Composition Root）。
    /// 在此集中完成所有服務的建立與依賴注入容器的配置，
    /// 各層的具體實作只在這裡被直接參考，其餘程式碼均透過介面互動。
    /// </summary>
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var container = BuildContainer();
            Application.Run(container.Resolve<MainForm>());
        }

        private static IServiceContainer BuildContainer()
        {
            var container = new ServiceContainer();

            // ── DAL：設定服務 ──────────────────────────────────────────────────
            var configPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "config",
                "compliance_config.json");
            var configService = new JsonConfigurationService(configPath);
            container.RegisterInstance<IConfigurationService>(configService);

            // ── BLL：外掛管理器 ────────────────────────────────────────────────
            var pluginManager = new PluginManager();
            container.RegisterInstance<IPluginManager>(pluginManager);

            // 初始載入外掛（後續每次執行 RunAllChecks 也會重新載入，支援熱插拔）
            var config = configService.LoadConfiguration();
            var pluginDir = Path.IsPathRooted(config.PluginDirectory)
                ? config.PluginDirectory
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.PluginDirectory);
            pluginManager.LoadPlugins(pluginDir);

            // ── BLL：合規檢查服務 ──────────────────────────────────────────────
            var checkService = new ComplianceCheckService(pluginManager, configService);
            container.RegisterInstance<IComplianceCheckService>(checkService);

            // ── UI：主視窗（注入所需服務） ─────────────────────────────────────
            var mainForm = new MainForm(checkService, configService, pluginManager);
            container.RegisterInstance(mainForm);

            return container;
        }
    }
}
