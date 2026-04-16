using System.Collections.Generic;

namespace WindowsComplianceChecker.Contracts.Interfaces
{
    /// <summary>
    /// 外掛管理器介面（BLL 層）。
    /// 負責從指定目錄動態載入符合 ICompliancePlugin 介面的外掛 DLL，
    /// 實現 Plugin Architecture，讓各外掛可獨立部署與更新。
    /// </summary>
    public interface IPluginManager
    {
        /// <summary>掃描並載入指定目錄下的所有外掛 DLL</summary>
        void LoadPlugins(string pluginDirectory);

        /// <summary>取得所有已載入的外掛</summary>
        IEnumerable<ICompliancePlugin> GetAllPlugins();

        /// <summary>依外掛名稱取得單一外掛（找不到時回傳 null）</summary>
        ICompliancePlugin GetPlugin(string pluginName);
    }
}
