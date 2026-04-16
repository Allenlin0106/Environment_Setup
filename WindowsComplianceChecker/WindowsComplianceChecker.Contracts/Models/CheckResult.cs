using System;

namespace WindowsComplianceChecker.Contracts.Models
{
    /// <summary>
    /// 單一檢查項目的執行結果
    /// </summary>
    public class CheckResult
    {
        /// <summary>對應 CheckItem.Id</summary>
        public string CheckItemId { get; set; }

        /// <summary>對應 CheckItem.Name</summary>
        public string CheckItemName { get; set; }

        /// <summary>產生此結果的外掛名稱</summary>
        public string PluginName { get; set; }

        /// <summary>檢查結果狀態</summary>
        public CheckStatus Status { get; set; }

        /// <summary>結果說明訊息</summary>
        public string Message { get; set; }

        /// <summary>設定檔中定義的期望值（顯示用）</summary>
        public string ExpectedValue { get; set; }

        /// <summary>執行時讀取到的實際值（顯示用）</summary>
        public string ActualValue { get; set; }

        /// <summary>執行檢查的時間點</summary>
        public DateTime CheckedAt { get; set; } = DateTime.Now;
    }
}
