using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.Plugins.GroupPolicy
{
    /// <summary>
    /// 群組原則檢查外掛（Plugin Architecture）。
    /// 透過 secedit /export 匯出本機安全性原則 INF 檔，
    /// 再解析各區段的鍵值與設定檔比對，判斷是否合規。
    ///
    /// CheckItem.Properties 支援的鍵：
    ///   PolicySection  : INF 區段名稱，例如 "System Access"
    ///   PolicyName     : 原則項目名稱，例如 "MinimumPasswordLength"
    ///   ExpectedValue  : 期望值
    ///   Operator       : 比較運算子（Equal / NotEqual / GreaterThan /
    ///                    GreaterThanOrEqual / LessThan / LessThanOrEqual）
    ///                    省略時預設為 Equal
    /// </summary>
    public class GroupPolicyPlugin : ICompliancePlugin
    {
        public string PluginName  => "GroupPolicyPlugin";
        public string DisplayName => "群組原則檢查器";
        public string Description => "透過 secedit 工具匯出並比對本機安全性原則設定";
        public string Version     => "1.0.0";

        public bool CanHandle(CheckItem item)
            => string.Equals(item?.PluginName, PluginName, StringComparison.OrdinalIgnoreCase);

        public IEnumerable<CheckResult> RunChecks(IEnumerable<CheckItem> items)
        {
            var results = new List<CheckResult>();

            // 一次匯出，供所有項目共用，避免重複執行 secedit
            var policyData = ExportSecurityPolicy();

            foreach (var item in items)
            {
                if (!CanHandle(item)) continue;
                results.Add(EvaluateItem(item, policyData));
            }

            return results;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private CheckResult EvaluateItem(
            CheckItem item,
            Dictionary<string, Dictionary<string, string>> policyData)
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
                item.Properties.TryGetValue("PolicySection",  out var section);
                item.Properties.TryGetValue("PolicyName",     out var policyName);
                item.Properties.TryGetValue("ExpectedValue",  out var expectedValue);
                item.Properties.TryGetValue("Operator",       out var op);

                if (string.IsNullOrEmpty(section) ||
                    string.IsNullOrEmpty(policyName) ||
                    string.IsNullOrEmpty(expectedValue))
                {
                    result.Status  = CheckStatus.Error;
                    result.Message = "設定項目缺少必要屬性 (PolicySection / PolicyName / ExpectedValue)";
                    return result;
                }

                result.ExpectedValue = expectedValue;

                if (!policyData.ContainsKey(section) ||
                    !policyData[section].ContainsKey(policyName))
                {
                    result.Status     = CheckStatus.Warning;
                    result.ActualValue = "未找到";
                    result.Message    = $"找不到群組原則 [{section}] {policyName}，" +
                                        "可能此系統版本未設定此原則。";
                    return result;
                }

                var actualValue = policyData[section][policyName];
                result.ActualValue = actualValue;

                bool passed = Compare(actualValue, expectedValue, op ?? "Equal");
                result.Status  = passed ? CheckStatus.Pass : CheckStatus.Fail;
                result.Message = passed
                    ? $"群組原則 [{section}] {policyName} 符合設定"
                    : $"群組原則 [{section}] {policyName} 不符合設定 " +
                      $"（期望: {expectedValue}，實際: {actualValue}，運算子: {op ?? "Equal"}）";
            }
            catch (Exception ex)
            {
                result.Status  = CheckStatus.Error;
                result.Message = $"檢查群組原則時發生錯誤: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 執行 secedit /export 匯出本機安全性原則至暫存 INF 檔，
        /// 解析後回傳 [Section][Key]=Value 的巢狀字典。
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> ExportSecurityPolicy()
        {
            var result = new Dictionary<string, Dictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);

            var tempFile = Path.Combine(
                Path.GetTempPath(),
                $"secedit_{Guid.NewGuid():N}.cfg");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "secedit.exe",
                    Arguments              = $"/export /cfg \"{tempFile}\" /quiet",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true
                };

                using (var process = Process.Start(psi))
                {
                    process.WaitForExit(30000);
                }

                if (!File.Exists(tempFile))
                    return result;

                var lines = File.ReadAllLines(tempFile, System.Text.Encoding.Unicode);
                string currentSection = null;

                foreach (var raw in lines)
                {
                    var line = raw.Trim();
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2);
                        if (!result.ContainsKey(currentSection))
                            result[currentSection] =
                                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                    else if (currentSection != null && line.Contains("="))
                    {
                        var idx   = line.IndexOf('=');
                        var key   = line.Substring(0, idx).Trim();
                        var value = line.Substring(idx + 1).Trim();
                        result[currentSection][key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GroupPolicyPlugin] secedit 匯出失敗: {ex.Message}");
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); }
                catch { /* 忽略暫存檔清除失敗 */ }
            }

            return result;
        }

        /// <summary>依指定運算子比較兩個字串或整數值</summary>
        private bool Compare(string actual, string expected, string op)
        {
            // 優先嘗試整數比較
            if (int.TryParse(actual,   out var a) &&
                int.TryParse(expected, out var e))
            {
                switch (op)
                {
                    case "Equal":              return a == e;
                    case "NotEqual":           return a != e;
                    case "GreaterThan":        return a >  e;
                    case "GreaterThanOrEqual": return a >= e;
                    case "LessThan":           return a <  e;
                    case "LessThanOrEqual":    return a <= e;
                    default:                   return a == e;
                }
            }

            // 回退至字串比較（忽略大小寫）
            return string.Equals(
                actual.Trim(), expected.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
