using System;
using System.IO;

namespace HK_AREA_SEARCH.Infrastructure.Services
{
    /// <summary>
    /// 日志服务
    /// </summary>
    public static class LogService
    {
        private static readonly string LogFilePath;
        private static readonly object _lock = new();

        static LogService()
        {
            // 日志文件保存在桌面
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string logFolder = Path.Combine(desktop, "HK_AreaSearch_Logs");
            
            // 创建日志文件夹
            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }

            // 每次运行创建新的日志文件（带时间戳）
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            LogFilePath = Path.Combine(logFolder, $"Log_{timestamp}.txt");
            
            // 写入文件头
            try
            {
                var header = new System.Text.StringBuilder();
                header.AppendLine("╔════════════════════════════════════════════════╗");
                header.AppendLine("║    HK Area Search - Diagnostic Log            ║");
                header.AppendLine("╚════════════════════════════════════════════════╝");
                header.AppendLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                header.AppendLine($"Machine: {Environment.MachineName}");
                header.AppendLine($"User: {Environment.UserName}");
                header.AppendLine($"OS: {Environment.OSVersion}");
                header.AppendLine($"Log File: {LogFilePath}");
                header.AppendLine("════════════════════════════════════════════════\n");
                
                File.WriteAllText(LogFilePath, header.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogService initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录信息
        /// </summary>
        public static void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        /// <summary>
        /// 记录警告
        /// </summary>
        public static void LogWarning(string message)
        {
            WriteLog("WARNING", message);
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        public static void LogError(string message)
        {
            WriteLog("ERROR", message);
        }

        /// <summary>
        /// 记录进度
        /// </summary>
        public static void LogProgress(string message)
        {
            WriteLog("PROGRESS", message);
        }

        /// <summary>
        /// 获取日志文件路径
        /// </summary>
        public static string GetLogFilePath() => LogFilePath;

        /// <summary>
        /// 写入日志
        /// </summary>
        private static void WriteLog(string level, string message)
        {
            lock (_lock)
            {
                try
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                    
                    // 同时输出到 Debug（方便开发时查看）
                    System.Diagnostics.Debug.WriteLine(logEntry);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Log write failed: {ex.Message}");
                }
            }
        }
    }
}