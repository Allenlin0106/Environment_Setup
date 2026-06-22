using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.Contracts.Interfaces
{
    /// <summary>
    /// 外掛可選擇性實作的修復介面。
    /// 實作此介面的外掛可在 UI 中提供「修復」功能，
    /// 將系統實際值調整為設定檔中定義的期望值。
    ///
    /// 設計為獨立介面（而非合併至 ICompliancePlugin），
    /// 讓唯讀外掛不需實作修復邏輯，符合介面隔離原則 (ISP)。
    /// </summary>
    public interface IRemediationPlugin
    {
        /// <summary>判斷此外掛是否能對指定項目執行修復</summary>
        bool CanRemediate(CheckItem item);

        /// <summary>
        /// 對指定項目執行修復，將系統設定調整為期望值。
        /// 呼叫前應先呼叫 CanRemediate 確認可執行。
        /// </summary>
        RemediationResult Remediate(CheckItem item);
    }
}
