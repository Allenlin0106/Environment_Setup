using System.Collections.Generic;
using WindowsComplianceChecker.Contracts.Models;

namespace WindowsComplianceChecker.Contracts.Interfaces
{
    /// <summary>
    /// 合規檢查外掛的標準介面。
    /// 實作此介面並編譯為 DLL 後放置於 Plugins 目錄，即可在執行期動態載入。
    /// 採用 Plugin Architecture，各外掛可獨立修改與更新，不影響主程式。
    /// </summary>
    public interface ICompliancePlugin
    {
        /// <summary>外掛唯一識別名稱（與 CheckItem.PluginName 對應）</summary>
        string PluginName { get; }

        /// <summary>外掛顯示名稱（用於 UI）</summary>
        string DisplayName { get; }

        /// <summary>外掛功能說明</summary>
        string Description { get; }

        /// <summary>外掛版本號</summary>
        string Version { get; }

        /// <summary>
        /// 判斷此外掛是否能處理指定的檢查項目
        /// </summary>
        bool CanHandle(CheckItem item);

        /// <summary>
        /// 對傳入的檢查項目清單執行合規檢查，並回傳各項結果
        /// </summary>
        IEnumerable<CheckResult> RunChecks(IEnumerable<CheckItem> items);
    }
}
