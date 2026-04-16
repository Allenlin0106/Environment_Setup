using System.Collections.Generic;

namespace WindowsComplianceChecker.Contracts.Models
{
    /// <summary>
    /// 合規設定檔的根物件，序列化為 JSON 後存放於 config\compliance_config.json。
    /// 新增或修改檢查項目只需編輯此設定檔，無需重新編譯程式。
    /// </summary>
    public class ComplianceConfig
    {
        /// <summary>
        /// 外掛 DLL 所在目錄（相對於執行檔目錄，或絕對路徑）
        /// </summary>
        public string PluginDirectory { get; set; } = "Plugins";

        /// <summary>
        /// 所有需要執行的檢查項目清單。
        /// 每個項目透過 PluginName 指定要由哪個外掛處理。
        /// </summary>
        public List<CheckItem> CheckItems { get; set; } = new List<CheckItem>();
    }
}
