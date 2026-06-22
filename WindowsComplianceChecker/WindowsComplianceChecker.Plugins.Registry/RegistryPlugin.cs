using System;
using System.Collections.Generic;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.Contracts.Models;

using WinReg = Microsoft.Win32;

namespace WindowsComplianceChecker.Plugins.Registry
{
    /// <summary>
    /// 登錄檔檢查 + 修復外掛（Plugin Architecture）。
    ///
    /// 【檢查】使用 Microsoft.Win32.Registry 讀取機碼值並與期望值比對。
    /// 【修復】以寫入模式開啟（或建立）機碼，將指定值設為期望值。
    ///         需要對該機碼擁有寫入權限（通常需要系統管理員）。
    ///
    /// CheckItem.Properties 支援的鍵：
    ///   KeyPath       : 完整機碼路徑（支援 HKLM / HKCU / HKCR / HKU / HKCC）
    ///   ValueName     : 值名稱
    ///   ExpectedValue : 期望值（DWORD/QWORD 以十進位字串表示）
    ///   ValueType     : STRING / DWORD / QWORD（省略預設 STRING）
    /// </summary>
    public class RegistryPlugin : ICompliancePlugin, IRemediationPlugin
    {
        public string PluginName  => "RegistryPlugin";
        public string DisplayName => "登錄檔檢查器";
        public string Description => "讀取 Windows 登錄檔機碼值並與期望值比對";
        public string Version     => "1.0.0";

        // ── ICompliancePlugin ──────────────────────────────────────────────────

        public bool CanHandle(CheckItem item)
            => string.Equals(item?.PluginName, PluginName, StringComparison.OrdinalIgnoreCase);

        public IEnumerable<CheckResult> RunChecks(IEnumerable<CheckItem> items)
        {
            var results = new List<CheckResult>();
            foreach (var item in items)
            {
                if (!CanHandle(item)) continue;
                results.Add(EvaluateItem(item));
            }
            return results;
        }

        // ── IRemediationPlugin ─────────────────────────────────────────────────

        public bool CanRemediate(CheckItem item)
        {
            if (!CanHandle(item)) return false;
            item.Properties.TryGetValue("KeyPath",       out var k);
            item.Properties.TryGetValue("ValueName",     out var v);
            item.Properties.TryGetValue("ExpectedValue", out var e);
            return !string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v) && !string.IsNullOrEmpty(e);
        }

        /// <summary>
        /// 以寫入模式開啟（或建立）登錄檔機碼，並將指定值設為期望值。
        /// </summary>
        public RemediationResult Remediate(CheckItem item)
        {
            var result = new RemediationResult
            {
                CheckItemId   = item.Id,
                CheckItemName = item.Name,
                PluginName    = PluginName,
                RemediatedAt  = DateTime.Now
            };

            try
            {
                item.Properties.TryGetValue("KeyPath",       out var keyPath);
                item.Properties.TryGetValue("ValueName",     out var valueName);
                item.Properties.TryGetValue("ExpectedValue", out var expectedValue);
                item.Properties.TryGetValue("ValueType",     out var valueType);

                if (string.IsNullOrEmpty(keyPath) ||
                    string.IsNullOrEmpty(valueName) ||
                    string.IsNullOrEmpty(expectedValue))
                {
                    result.Success = false;
                    result.Message = "設定項目缺少必要屬性 (KeyPath / ValueName / ExpectedValue)";
                    return result;
                }

                WinReg.RegistryKey hive;
                string subKey;
                ParseKeyPath(keyPath, out hive, out subKey);

                // 嘗試以寫入模式開啟；若機碼不存在則建立
                var regKey = hive.OpenSubKey(subKey, writable: true)
                          ?? hive.CreateSubKey(subKey);

                using (regKey)
                {
                    WriteValue(regKey, valueName, expectedValue, valueType ?? "STRING");
                }

                result.Success = true;
                result.Message =
                    $"已寫入 {keyPath}\\{valueName} = {expectedValue}" +
                    $"（類型: {(valueType ?? "STRING").ToUpperInvariant()}）";
            }
            catch (UnauthorizedAccessException)
            {
                result.Success = false;
                result.Message = "存取被拒絕，請以系統管理員身份執行程式後再試。";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"修復登錄檔時發生錯誤：{ex.Message}";
            }

            return result;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private CheckResult EvaluateItem(CheckItem item)
        {
            var result = new CheckResult
            {
                CheckItemId   = item.Id,
                CheckItemName = item.Name,
                PluginName    = PluginName,
                CheckedAt     = DateTime.Now
            };

            try
            {
                item.Properties.TryGetValue("KeyPath",       out var keyPath);
                item.Properties.TryGetValue("ValueName",     out var valueName);
                item.Properties.TryGetValue("ExpectedValue", out var expectedValue);
                item.Properties.TryGetValue("ValueType",     out var valueType);

                if (string.IsNullOrEmpty(keyPath) || string.IsNullOrEmpty(valueName))
                {
                    result.Status  = CheckStatus.Error;
                    result.Message = "設定項目缺少必要屬性 (KeyPath / ValueName)";
                    return result;
                }

                result.ExpectedValue = expectedValue ?? "(any)";

                WinReg.RegistryKey hive;
                string subKey;
                ParseKeyPath(keyPath, out hive, out subKey);

                using (var regKey = hive.OpenSubKey(subKey, writable: false))
                {
                    if (regKey == null)
                    {
                        result.Status     = CheckStatus.Fail;
                        result.ActualValue = "機碼不存在";
                        result.Message    = $"登錄檔機碼不存在: {keyPath}";
                        return result;
                    }

                    var rawValue = regKey.GetValue(valueName);
                    if (rawValue == null)
                    {
                        result.Status     = CheckStatus.Fail;
                        result.ActualValue = "值不存在";
                        result.Message    = $"登錄檔值 '{valueName}' 不存在於 {keyPath}";
                        return result;
                    }

                    result.ActualValue = rawValue.ToString();

                    if (string.IsNullOrEmpty(expectedValue))
                    {
                        result.Status  = CheckStatus.Pass;
                        result.Message = $"登錄檔值 '{valueName}' 存在（{result.ActualValue}）";
                        return result;
                    }

                    bool matched = ValuesMatch(result.ActualValue, expectedValue, valueType ?? "STRING");
                    result.Status  = matched ? CheckStatus.Pass : CheckStatus.Fail;
                    result.Message = matched
                        ? $"登錄檔值 '{valueName}' 符合設定（{result.ActualValue}）"
                        : $"登錄檔值 '{valueName}' 不符合設定 " +
                          $"（期望: {expectedValue}，實際: {result.ActualValue}）";
                }
            }
            catch (Exception ex)
            {
                result.Status  = CheckStatus.Error;
                result.Message = $"讀取登錄檔時發生錯誤: {ex.Message}";
            }
            return result;
        }

        private void ParseKeyPath(string fullPath,
                                  out WinReg.RegistryKey hive,
                                  out string subKey)
        {
            var idx = fullPath.IndexOf('\\');
            if (idx < 0)
                throw new ArgumentException($"登錄檔路徑格式錯誤，缺少 '\\': {fullPath}");

            var root = fullPath.Substring(0, idx).ToUpperInvariant();
            subKey   = fullPath.Substring(idx + 1);

            switch (root)
            {
                case "HKEY_LOCAL_MACHINE": case "HKLM":
                    hive = WinReg.Registry.LocalMachine;  break;
                case "HKEY_CURRENT_USER":  case "HKCU":
                    hive = WinReg.Registry.CurrentUser;   break;
                case "HKEY_CLASSES_ROOT":  case "HKCR":
                    hive = WinReg.Registry.ClassesRoot;   break;
                case "HKEY_USERS":         case "HKU":
                    hive = WinReg.Registry.Users;         break;
                case "HKEY_CURRENT_CONFIG": case "HKCC":
                    hive = WinReg.Registry.CurrentConfig; break;
                default:
                    throw new ArgumentException($"不支援的登錄檔根機碼: '{root}'");
            }
        }

        private bool ValuesMatch(string actual, string expected, string valueType)
        {
            switch (valueType.ToUpperInvariant())
            {
                case "DWORD":
                case "QWORD":
                    long a, e;
                    if (long.TryParse(actual, out a) && long.TryParse(expected, out e))
                        return a == e;
                    break;
            }
            return string.Equals(actual.Trim(), expected.Trim(),
                                 StringComparison.OrdinalIgnoreCase);
        }

        private void WriteValue(WinReg.RegistryKey key, string name,
                                string value, string valueType)
        {
            switch (valueType.ToUpperInvariant())
            {
                case "DWORD":
                    int dw;
                    if (!int.TryParse(value, out dw))
                        throw new ArgumentException($"無法將 '{value}' 轉換為 DWORD 整數");
                    key.SetValue(name, dw, WinReg.RegistryValueKind.DWord);
                    break;
                case "QWORD":
                    long qw;
                    if (!long.TryParse(value, out qw))
                        throw new ArgumentException($"無法將 '{value}' 轉換為 QWORD 整數");
                    key.SetValue(name, qw, WinReg.RegistryValueKind.QWord);
                    break;
                default:
                    key.SetValue(name, value, WinReg.RegistryValueKind.String);
                    break;
            }
        }
    }
}
