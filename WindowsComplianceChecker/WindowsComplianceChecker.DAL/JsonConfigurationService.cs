using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.DAL
{
    /// <summary>
    /// 以 JSON 格式讀寫合規設定檔的實作（DAL 層）。
    /// 若設定檔不存在，首次啟動時會自動建立包含範例項目的預設設定檔。
    /// 替換此類別即可切換為其他儲存格式（XML、資料庫等），上層程式碼無需修改。
    /// </summary>
    public class JsonConfigurationService : IConfigurationService
    {
        private readonly string _configFilePath;

        public JsonConfigurationService(string configFilePath)
        {
            if (string.IsNullOrWhiteSpace(configFilePath))
                throw new ArgumentNullException(nameof(configFilePath));
            _configFilePath = configFilePath;
        }

        public string ConfigurationFilePath => _configFilePath;

        public ComplianceConfig LoadConfiguration()
        {
            if (!File.Exists(_configFilePath))
            {
                var defaultConfig = CreateDefaultConfig();
                SaveConfiguration(defaultConfig);
                return defaultConfig;
            }

            try
            {
                var json = File.ReadAllText(_configFilePath, System.Text.Encoding.UTF8);
                return JsonConvert.DeserializeObject<ComplianceConfig>(json) ?? new ComplianceConfig();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"讀取設定檔失敗: {_configFilePath}\n{ex.Message}", ex);
            }
        }

        public void SaveConfiguration(ComplianceConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var directory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(_configFilePath, json, System.Text.Encoding.UTF8);
        }

        /// <summary>建立包含三類外掛範例項目的預設設定</summary>
        private ComplianceConfig CreateDefaultConfig()
        {
            return new ComplianceConfig
            {
                PluginDirectory = "Plugins",
                CheckItems = new List<CheckItem>
                {
                    // ── 群組原則檢查範例 ──────────────────────────────────────
                    new CheckItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "最低密碼長度",
                        Description = "確認本機安全性原則中最低密碼長度 >= 8 個字元",
                        IsEnabled = true,
                        PluginName = "GroupPolicyPlugin",
                        Properties = new Dictionary<string, string>
                        {
                            { "PolicySection",  "System Access" },
                            { "PolicyName",     "MinimumPasswordLength" },
                            { "ExpectedValue",  "8" },
                            { "Operator",       "GreaterThanOrEqual" }
                        }
                    },
                    new CheckItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "密碼最長使用期限",
                        Description = "確認密碼最長使用期限 <= 90 天",
                        IsEnabled = true,
                        PluginName = "GroupPolicyPlugin",
                        Properties = new Dictionary<string, string>
                        {
                            { "PolicySection",  "System Access" },
                            { "PolicyName",     "MaximumPasswordAge" },
                            { "ExpectedValue",  "90" },
                            { "Operator",       "LessThanOrEqual" }
                        }
                    },
                    new CheckItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "帳戶鎖定閾值",
                        Description = "確認連續登入失敗超過 5 次即鎖定帳戶",
                        IsEnabled = true,
                        PluginName = "GroupPolicyPlugin",
                        Properties = new Dictionary<string, string>
                        {
                            { "PolicySection",  "System Access" },
                            { "PolicyName",     "LockoutBadCount" },
                            { "ExpectedValue",  "5" },
                            { "Operator",       "LessThanOrEqual" }
                        }
                    },
                    // ── 登錄檔檢查範例 ────────────────────────────────────────
                    new CheckItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Windows 防火牆（標準設定檔）啟用",
                        Description = "確認 Windows Defender 防火牆已針對標準設定檔啟用",
                        IsEnabled = true,
                        PluginName = "RegistryPlugin",
                        Properties = new Dictionary<string, string>
                        {
                            { "KeyPath",       @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile" },
                            { "ValueName",     "EnableFirewall" },
                            { "ExpectedValue", "1" },
                            { "ValueType",     "DWORD" }
                        }
                    },
                    new CheckItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "UAC 啟用狀態",
                        Description = "確認使用者帳戶控制 (UAC) 已開啟",
                        IsEnabled = true,
                        PluginName = "RegistryPlugin",
                        Properties = new Dictionary<string, string>
                        {
                            { "KeyPath",       @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System" },
                            { "ValueName",     "EnableLUA" },
                            { "ExpectedValue", "1" },
                            { "ValueType",     "DWORD" }
                        }
                    },
                    new CheckItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "自動播放功能停用",
                        Description = "確認 AutoRun 功能已透過登錄檔停用（值 0xFF = 255）",
                        IsEnabled = true,
                        PluginName = "RegistryPlugin",
                        Properties = new Dictionary<string, string>
                        {
                            { "KeyPath",       @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer" },
                            { "ValueName",     "NoDriveTypeAutoRun" },
                            { "ExpectedValue", "255" },
                            { "ValueType",     "DWORD" }
                        }
                    },
                    // ── 本機帳號檢查範例 ──────────────────────────────────────
                    new CheckItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Guest 帳號已停用",
                        Description = "確認內建 Guest 帳號存在但已停用",
                        IsEnabled = true,
                        PluginName = "LocalAccountPlugin",
                        Properties = new Dictionary<string, string>
                        {
                            { "Username",        "Guest" },
                            { "ShouldExist",     "true" },
                            { "ShouldBeEnabled", "false" }
                        }
                    },
                    new CheckItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Administrator 帳號存在",
                        Description = "確認內建 Administrator 帳號存在",
                        IsEnabled = true,
                        PluginName = "LocalAccountPlugin",
                        Properties = new Dictionary<string, string>
                        {
                            { "Username",    "Administrator" },
                            { "ShouldExist", "true" }
                        }
                    },
                    new CheckItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "稽核帳號建立確認",
                        Description = "確認合規稽核專用帳號 AuditUser 已建立",
                        IsEnabled = false,
                        PluginName = "LocalAccountPlugin",
                        Properties = new Dictionary<string, string>
                        {
                            { "Username",        "AuditUser" },
                            { "ShouldExist",     "true" },
                            { "ShouldBeEnabled", "true" }
                        }
                    }
                }
            };
        }
    }
}
