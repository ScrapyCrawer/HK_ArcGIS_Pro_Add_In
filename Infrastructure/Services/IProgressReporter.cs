using System;

namespace HK_AREA_SEARCH.Infrastructure.Services
{
    /// <summary>
    /// 进度报告接口
    /// </summary>
    public interface IProgressReporter
    {
        /// <summary>
        /// 报告进度
        /// </summary>
        /// <param name="message">当前操作描述</param>
        /// <param name="percent">进度百分比 (0-100)</param>
        void ReportProgress(string message, int percent);
    }
}