using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.Contracts.Interfaces
{
    /// <summary>
    /// 設定服務介面（DAL 層）。
    /// 負責讀取與儲存合規設定檔，實作可替換為 XML、資料庫等不同來源。
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>設定檔的完整路徑</summary>
        string ConfigurationFilePath { get; }

        /// <summary>從持久化來源載入合規設定</summary>
        ComplianceConfig LoadConfiguration();

        /// <summary>將合規設定寫回持久化來源</summary>
        void SaveConfiguration(ComplianceConfig config);
    }
}
