using System;
using System.Collections.Generic;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.Contracts.Models;

// 使用別名避免與命名空間 WindowsComplianceChecker.Plugins.Registry 產生衝突
using WinReg = Microsoft.Win32;

namespace WindowsComplianceChecker.Plugins.Registry
{
    /// <summary>
    /// 登錄檔檢查外掛（Plugin Architecture）。
    /// 使用 Microsoft.Win32.Registry 直接讀取 Windows 登錄檔，
    /// 比對指定機碼值是否與設定的期望值相符。
    ///
    /// CheckItem.Properties 支援的鍵：
    ///   KeyPath       : 完整登錄檔路徑，例如
    ///                   "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion"
    ///                   支援前置詞 HKLM / HKCU / HKCR / HKU
    ///   ValueName     : 值名稱
    ///   ExpectedValue : 期望的字串值（DWORD 以十進位表示）
    ///   ValueType     : 值類型提示（STRING / DWORD / QWORD），影響比較方式
    /// </summary>
    public class RegistryPlugin : ICompliancePlugin
    {
        public string PluginName  => "RegistryPlugin";
        public string DisplayName => "登錄檔檢查器";
        public string Description => "讀取 Windows 登錄檔機碼值並與期望值比對";
        public string Version     => "1.0.0";

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
                        result.Message    = $"登錄檔機碼值 '{valueName}' 不存在於 {keyPath}";
                        return result;
                    }

                    result.ActualValue = rawValue.ToString();

                    if (string.IsNullOrEmpty(expectedValue))
                    {
                        result.Status  = CheckStatus.Pass;
                        result.Message = $"登錄檔值 '{valueName}' 存在（值: {result.ActualValue}）";
                        return result;
                    }

                    bool matched = ValuesMatch(result.ActualValue, expectedValue,
                                               valueType ?? "STRING");
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

        /// <summary>將完整登錄檔路徑拆解為 hive 物件與子機碼字串</summary>
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
                case "HKEY_LOCAL_MACHINE":
                case "HKLM":
                    hive = WinReg.Registry.LocalMachine;
                    break;
                case "HKEY_CURRENT_USER":
                case "HKCU":
                    hive = WinReg.Registry.CurrentUser;
                    break;
                case "HKEY_CLASSES_ROOT":
                case "HKCR":
                    hive = WinReg.Registry.ClassesRoot;
                    break;
                case "HKEY_USERS":
                case "HKU":
                    hive = WinReg.Registry.Users;
                    break;
                case "HKEY_CURRENT_CONFIG":
                case "HKCC":
                    hive = WinReg.Registry.CurrentConfig;
                    break;
                default:
                    throw new ArgumentException($"不支援的登錄檔根機碼: '{root}'");
            }
        }

        /// <summary>依 valueType 選擇比較策略</summary>
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
    }
}
