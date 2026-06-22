using System;

namespace WindowsComplianceChecker.Contracts.Models
{
    /// <summary>
    /// 單一項目的修復執行結果
    /// </summary>
    public class RemediationResult
    {
        public string CheckItemId   { get; set; }
        public string CheckItemName { get; set; }
        public string PluginName    { get; set; }

        /// <summary>修復是否成功</summary>
        public bool   Success  { get; set; }

        /// <summary>修復結果說明（成功時描述動作，失敗時描述原因）</summary>
        public string Message  { get; set; }

        public DateTime RemediatedAt { get; set; } = DateTime.Now;
    }
}
