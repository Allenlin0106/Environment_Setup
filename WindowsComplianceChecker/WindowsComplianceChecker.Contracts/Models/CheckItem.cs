using System;
using System.Collections.Generic;

namespace WindowsComplianceChecker.Contracts.Models
{
    /// <summary>
    /// 代表一個可透過外掛執行的合規檢查項目。
    /// 透過 Properties 字典擴充各外掛所需的參數，達到彈性設定的目的。
    /// </summary>
    public class CheckItem
    {
        /// <summary>唯一識別碼</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>顯示名稱</summary>
        public string Name { get; set; }

        /// <summary>說明</summary>
        public string Description { get; set; }

        /// <summary>是否啟用此檢查項目</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 負責處理此項目的外掛名稱，需與 ICompliancePlugin.PluginName 相符
        /// </summary>
        public string PluginName { get; set; }

        /// <summary>
        /// 外掛自訂屬性字典，各外掛依需求讀取對應的鍵值。
        /// 例如 RegistryPlugin 讀取 KeyPath / ValueName / ExpectedValue 等。
        /// </summary>
        public Dictionary<string, string> Properties { get; set; }
            = new Dictionary<string, string>();
    }
}
