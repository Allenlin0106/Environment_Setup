using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.Plugins.LocalAccount
{
    /// <summary>
    /// 本機帳號檢查外掛（Plugin Architecture）。
    /// 透過 System.DirectoryServices.AccountManagement 查詢本機使用者帳號，
    /// 確認指定帳號是否存在及其啟用狀態是否符合設定。
    ///
    /// CheckItem.Properties 支援的鍵：
    ///   Username        : 要檢查的本機帳號名稱（必填）
    ///   ShouldExist     : "true" 期望帳號存在 / "false" 期望帳號不存在（預設 true）
    ///   ShouldBeEnabled : "true" 期望啟用 / "false" 期望停用（省略則不檢查此項）
    /// </summary>
    public class LocalAccountPlugin : ICompliancePlugin
    {
        public string PluginName  => "LocalAccountPlugin";
        public string DisplayName => "本機帳號檢查器";
        public string Description => "檢查 Windows 本機使用者帳號是否存在及其啟用狀態";
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
                item.Properties.TryGetValue("Username",        out var username);
                item.Properties.TryGetValue("ShouldExist",     out var shouldExistStr);
                item.Properties.TryGetValue("ShouldBeEnabled", out var shouldBeEnabledStr);

                if (string.IsNullOrWhiteSpace(username))
                {
                    result.Status  = CheckStatus.Error;
                    result.Message = "設定項目缺少必要屬性 (Username)";
                    return result;
                }

                bool shouldExist = !string.Equals(
                    shouldExistStr, "false", StringComparison.OrdinalIgnoreCase);

                bool? shouldBeEnabled = null;
                if (!string.IsNullOrEmpty(shouldBeEnabledStr))
                    shouldBeEnabled = !string.Equals(
                        shouldBeEnabledStr, "false", StringComparison.OrdinalIgnoreCase);

                using (var ctx = new PrincipalContext(ContextType.Machine))
                {
                    var user = UserPrincipal.FindByIdentity(
                        ctx, IdentityType.SamAccountName, username);

                    bool exists = user != null;

                    // ── 帳號存在性檢查 ──────────────────────────────────────
                    if (shouldExist && !exists)
                    {
                        result.Status      = CheckStatus.Fail;
                        result.ExpectedValue = "帳號存在";
                        result.ActualValue   = "帳號不存在";
                        result.Message     = $"本機帳號 '{username}' 不存在";
                        return result;
                    }

                    if (!shouldExist && exists)
                    {
                        result.Status      = CheckStatus.Fail;
                        result.ExpectedValue = "帳號不存在";
                        result.ActualValue   = "帳號存在";
                        result.Message     = $"本機帳號 '{username}' 存在（預期應不存在）";
                        return result;
                    }

                    if (!shouldExist && !exists)
                    {
                        result.Status      = CheckStatus.Pass;
                        result.ExpectedValue = "帳號不存在";
                        result.ActualValue   = "帳號不存在";
                        result.Message     = $"本機帳號 '{username}' 確認不存在，符合設定";
                        return result;
                    }

                    // 帳號存在且預期存在 → 進一步檢查啟用狀態
                    if (!shouldBeEnabled.HasValue)
                    {
                        result.Status      = CheckStatus.Pass;
                        result.ExpectedValue = "帳號存在";
                        result.ActualValue   = "帳號存在";
                        result.Message     = $"本機帳號 '{username}' 確認存在，符合設定";
                        return result;
                    }

                    // ── 啟用狀態檢查 ────────────────────────────────────────
                    bool isEnabled = user.Enabled ?? false;
                    result.ExpectedValue = shouldBeEnabled.Value ? "已啟用" : "已停用";
                    result.ActualValue   = isEnabled ? "已啟用" : "已停用";

                    if (isEnabled == shouldBeEnabled.Value)
                    {
                        result.Status  = CheckStatus.Pass;
                        result.Message = $"本機帳號 '{username}' 存在且狀態 ({result.ActualValue}) 符合設定";
                    }
                    else
                    {
                        result.Status  = CheckStatus.Fail;
                        result.Message =
                            $"本機帳號 '{username}' 狀態不符合設定 " +
                            $"（期望: {result.ExpectedValue}，實際: {result.ActualValue}）";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Status  = CheckStatus.Error;
                result.Message = $"查詢本機帳號時發生錯誤: {ex.Message}";
            }

            return result;
        }
    }
}
