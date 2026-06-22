using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using WindowsComplianceChecker.Contracts.Interfaces;
using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.Plugins.LocalAccount
{
    /// <summary>
    /// 本機帳號檢查 + 修復外掛（Plugin Architecture）。
    ///
    /// 【檢查】透過 DirectoryServices.AccountManagement 查詢本機帳號存在性與啟用狀態。
    /// 【修復】依設定啟用或停用帳號（ShouldBeEnabled）。
    ///         若帳號不存在且 ShouldExist=true，會提示需手動建立。
    ///
    /// CheckItem.Properties 支援的鍵：
    ///   Username        : 本機帳號名稱（必填）
    ///   ShouldExist     : "true" 期望存在 / "false" 期望不存在（預設 true）
    ///   ShouldBeEnabled : "true" 期望啟用 / "false" 期望停用（省略則不驗證此項）
    /// </summary>
    public class LocalAccountPlugin : ICompliancePlugin, IRemediationPlugin
    {
        public string PluginName  => "LocalAccountPlugin";
        public string DisplayName => "本機帳號檢查器";
        public string Description => "檢查 Windows 本機使用者帳號存在性與啟用狀態";
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
            item.Properties.TryGetValue("Username",        out var u);
            item.Properties.TryGetValue("ShouldBeEnabled", out var e);
            // 目前支援啟用/停用修復；需提供 Username 與 ShouldBeEnabled
            return !string.IsNullOrEmpty(u) && !string.IsNullOrEmpty(e);
        }

        /// <summary>
        /// 依 ShouldBeEnabled 的設定啟用或停用本機帳號。
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
                item.Properties.TryGetValue("Username",        out var username);
                item.Properties.TryGetValue("ShouldBeEnabled", out var shouldBeEnabledStr);

                if (string.IsNullOrWhiteSpace(username))
                {
                    result.Success = false;
                    result.Message = "設定項目缺少必要屬性 (Username)";
                    return result;
                }

                if (string.IsNullOrEmpty(shouldBeEnabledStr))
                {
                    result.Success = false;
                    result.Message = "未設定 ShouldBeEnabled 屬性，無法確定目標狀態。";
                    return result;
                }

                bool targetEnabled = !string.Equals(
                    shouldBeEnabledStr, "false", StringComparison.OrdinalIgnoreCase);

                using (var ctx = new PrincipalContext(ContextType.Machine))
                {
                    var user = UserPrincipal.FindByIdentity(
                        ctx, IdentityType.SamAccountName, username);

                    if (user == null)
                    {
                        result.Success = false;
                        result.Message =
                            $"本機帳號 '{username}' 不存在，無法自動修復。\n" +
                            "請先手動建立帳號後再執行修復。";
                        return result;
                    }

                    var currentEnabled = user.Enabled ?? false;
                    if (currentEnabled == targetEnabled)
                    {
                        result.Success = true;
                        result.Message =
                            $"帳號 '{username}' 已經是{(targetEnabled ? "啟用" : "停用")}狀態，無需修復。";
                        return result;
                    }

                    user.Enabled = targetEnabled;
                    user.Save();

                    result.Success = true;
                    result.Message =
                        $"已成功將本機帳號 '{username}' {(targetEnabled ? "啟用" : "停用")}。";
                }
            }
            catch (UnauthorizedAccessException)
            {
                result.Success = false;
                result.Message = "存取被拒絕，請以系統管理員身份執行程式後再試。";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"修復本機帳號時發生錯誤：{ex.Message}";
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
                    var user   = UserPrincipal.FindByIdentity(
                        ctx, IdentityType.SamAccountName, username);
                    bool exists = user != null;

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

                    if (!shouldBeEnabled.HasValue)
                    {
                        result.Status      = CheckStatus.Pass;
                        result.ExpectedValue = "帳號存在";
                        result.ActualValue   = "帳號存在";
                        result.Message     = $"本機帳號 '{username}' 確認存在，符合設定";
                        return result;
                    }

                    bool isEnabled = user.Enabled ?? false;
                    result.ExpectedValue = shouldBeEnabled.Value ? "已啟用" : "已停用";
                    result.ActualValue   = isEnabled ? "已啟用" : "已停用";

                    if (isEnabled == shouldBeEnabled.Value)
                    {
                        result.Status  = CheckStatus.Pass;
                        result.Message = $"本機帳號 '{username}' 狀態 ({result.ActualValue}) 符合設定";
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
