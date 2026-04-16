using System.Collections.Generic;
using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.Contracts.Interfaces
{
    /// <summary>
    /// 合規檢查服務介面（BLL 層）。
    /// 協調 IPluginManager 與 IConfigurationService，
    /// 依設定檔內容驅動各外掛執行合規檢查並彙總結果。
    /// </summary>
    public interface IComplianceCheckService
    {
        /// <summary>執行設定檔中所有已啟用的檢查項目</summary>
        IEnumerable<CheckResult> RunAllChecks();

        /// <summary>僅執行指定外掛的已啟用檢查項目</summary>
        IEnumerable<CheckResult> RunChecksByPlugin(string pluginName);

        /// <summary>對任意指定的檢查項目清單執行檢查（不受設定檔限制）</summary>
        IEnumerable<CheckResult> RunChecksForItems(IEnumerable<CheckItem> items);
    }
}
