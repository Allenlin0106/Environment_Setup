using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.Plugins.GroupPolicy
{
    /// <summary>
    /// 群組原則檢查 + 修復外掛（Plugin Architecture）。
    ///
    /// 【檢查】透過 secedit /export 匯出本機安全性原則 INF，解析後比對期望值。
    /// 【修復】建立最小化 INF 範本，透過 secedit /configure 套用單一原則設定。
    ///
    /// CheckItem.Properties 支援的鍵：
    ///   PolicySection  : INF 區段名稱，例如 "System Access"
    ///   PolicyName     : 原則鍵名，例如 "MinimumPasswordLength"
    ///   ExpectedValue  : 期望值（修復時即套用此值）
    ///   Operator       : Equal / NotEqual / GreaterThan / GreaterThanOrEqual /
    ///                    LessThan / LessThanOrEqual（省略預設 Equal）
    /// </summary>
    public class GroupPolicyPlugin : ICompliancePlugin, IRemediationPlugin
    {
        public string PluginName  => "GroupPolicyPlugin";
        public string DisplayName => "群組原則檢查器";
        public string Description => "透過 secedit 工具匯出並比對本機安全性原則設定";
        public string Version     => "1.0.0";

        // ── ICompliancePlugin ──────────────────────────────────────────────────

        public bool CanHandle(CheckItem item)
            => string.Equals(item?.PluginName, PluginName, StringComparison.OrdinalIgnoreCase);

        public IEnumerable<CheckResult> RunChecks(IEnumerable<CheckItem> items)
        {
            var results    = new List<CheckResult>();
            var policyData = ExportSecurityPolicy();

            foreach (var item in items)
            {
                if (!CanHandle(item)) continue;
                results.Add(EvaluateItem(item, policyData));
            }
            return results;
        }

        // ── IRemediationPlugin ─────────────────────────────────────────────────

        public bool CanRemediate(CheckItem item)
        {
            if (!CanHandle(item)) return false;
            item.Properties.TryGetValue("PolicySection",  out var s);
            item.Properties.TryGetValue("PolicyName",     out var n);
            item.Properties.TryGetValue("ExpectedValue",  out var v);
            return !string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(n) && !string.IsNullOrEmpty(v);
        }

        /// <summary>
        /// 建立最小化安全性範本 INF，透過 secedit /configure 套用單一原則設定。
        /// 需要系統管理員權限。
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
                item.Properties.TryGetValue("PolicySection",  out var section);
                item.Properties.TryGetValue("PolicyName",     out var policyName);
                item.Properties.TryGetValue("ExpectedValue",  out var expectedValue);

                var tempCfg = Path.Combine(Path.GetTempPath(),
                    $"secedit_fix_{Guid.NewGuid():N}.inf");
                var tempDb = Path.Combine(Path.GetTempPath(),
                    $"secedit_fix_{Guid.NewGuid():N}.sdb");

                try
                {
                    // 建立最小化 INF 安全性範本（Unicode 格式，secedit 要求）
                    var sb = new StringBuilder();
                    sb.AppendLine("[Unicode]");
                    sb.AppendLine("Unicode=yes");
                    sb.AppendLine("[Version]");
                    sb.AppendLine("signature=\"$CHICAGO$\"");
                    sb.AppendLine("Revision=1");
                    sb.AppendLine($"[{section}]");
                    sb.AppendLine($"{policyName} = {expectedValue}");
                    File.WriteAllText(tempCfg, sb.ToString(), Encoding.Unicode);

                    var psi = new ProcessStartInfo
                    {
                        FileName               = "secedit.exe",
                        Arguments              = $"/configure /cfg \"{tempCfg}\" /db \"{tempDb}\" /quiet",
                        UseShellExecute        = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        CreateNoWindow         = true
                    };

                    using (var p = Process.Start(psi))
                    {
                        p.WaitForExit(30000);
                        if (p.ExitCode == 0)
                        {
                            result.Success = true;
                            result.Message =
                                $"已套用群組原則 [{section}] {policyName} = {expectedValue}";
                        }
                        else
                        {
                            var err = p.StandardError.ReadToEnd().Trim();
                            result.Success = false;
                            result.Message =
                                $"secedit 執行失敗 (exit {p.ExitCode})" +
                                (string.IsNullOrEmpty(err) ? "" : $"：{err}");
                        }
                    }
                }
                finally
                {
                    TryDelete(tempCfg);
                    TryDelete(tempDb);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"修復群組原則時發生錯誤：{ex.Message}";
            }

            return result;
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

                if (!policyData.ContainsKey(section) || !policyData[section].ContainsKey(policyName))
                {
                    result.Status     = CheckStatus.Warning;
                    result.ActualValue = "未找到";
                    result.Message    = $"找不到群組原則 [{section}] {policyName}";
                    return result;
                }

                var actual = policyData[section][policyName];
                result.ActualValue = actual;
                bool passed = Compare(actual, expectedValue, op ?? "Equal");
                result.Status  = passed ? CheckStatus.Pass : CheckStatus.Fail;
                result.Message = passed
                    ? $"群組原則 [{section}] {policyName} 符合設定"
                    : $"群組原則 [{section}] {policyName} 不符合設定 " +
                      $"（期望: {expectedValue}，實際: {actual}，運算子: {op ?? "Equal"}）";
            }
            catch (Exception ex)
            {
                result.Status  = CheckStatus.Error;
                result.Message = $"檢查群組原則時發生錯誤: {ex.Message}";
            }
            return result;
        }

        private Dictionary<string, Dictionary<string, string>> ExportSecurityPolicy()
        {
            var result   = new Dictionary<string, Dictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);
            var tempFile = Path.Combine(Path.GetTempPath(), $"secedit_{Guid.NewGuid():N}.cfg");

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
                using (var p = Process.Start(psi))
                    p.WaitForExit(30000);

                if (!File.Exists(tempFile)) return result;

                string currentSection = null;
                foreach (var raw in File.ReadAllLines(tempFile, Encoding.Unicode))
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
                TryDelete(tempFile);
            }
            return result;
        }

        private bool Compare(string actual, string expected, string op)
        {
            if (int.TryParse(actual, out var a) && int.TryParse(expected, out var e))
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
            return string.Equals(actual.Trim(), expected.Trim(),
                                 StringComparison.OrdinalIgnoreCase);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* 忽略暫存檔清除失敗 */ }
        }
    }
}
