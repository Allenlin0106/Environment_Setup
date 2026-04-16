using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.Contracts.Interfaces
{
    /// <summary>
    /// 修復服務介面（BLL 層）。
    /// 協調 IPluginManager 找到能處理該項目的外掛，
    /// 並轉呼叫 IRemediationPlugin.Remediate。
    /// UI 層只依賴此介面，不直接接觸外掛實作。
    /// </summary>
    public interface IRemediationService
    {
        /// <summary>判斷指定項目是否有對應外掛支援修復</summary>
        bool CanRemediate(CheckItem item);

        /// <summary>對指定項目執行修復並回傳結果</summary>
        RemediationResult RemediateItem(CheckItem item);
    }
}
