namespace WindowsComplianceChecker.Contracts.Models
{
    /// <summary>
    /// 檢查結果狀態列舉
    /// </summary>
    public enum CheckStatus
    {
        /// <summary>通過</summary>
        Pass,
        /// <summary>失敗</summary>
        Fail,
        /// <summary>警告</summary>
        Warning,
        /// <summary>執行錯誤</summary>
        Error,
        /// <summary>略過</summary>
        Skipped
    }
}
