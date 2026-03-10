using System;
using System.Diagnostics;

namespace HK_AREA_SEARCH.Infrastructure.Services
{
    /// <summary>
    /// 轻量级日志服务 - 仅用于调试输出
    /// </summary>
    public static class LogService
    {
        /// <summary>
        /// 记录信息
        /// </summary>
        public static void LogInfo(string message)
        {
            Debug.WriteLine($"[INFO] {message}");
        }

        /// <summary>
        /// 记录警告
        /// </summary>
        public static void LogWarning(string message)
        {
            Debug.WriteLine($"[WARNING] {message}");
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        public static void LogError(string message)
        {
            Debug.WriteLine($"[ERROR] {message}");
        }

        /// <summary>
        /// 记录进度
        /// </summary>
        public static void LogProgress(string message)
        {
            Debug.WriteLine($"[PROGRESS] {message}");
        }
    }
}