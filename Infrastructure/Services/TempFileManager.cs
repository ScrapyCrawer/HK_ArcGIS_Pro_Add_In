using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HK_AREA_SEARCH.Infrastructure.Services
{
    /// <summary>
    /// 临时文件管理器
    /// </summary>
    public class TempFileManager
    {
        private readonly List<string> _tempFiles;
        private readonly string _tempDirectory;
        private readonly object _lock = new object();

        public TempFileManager()
        {
            _tempFiles = new List<string>();
            
            //使用时间戳+GUID创建唯一临时文件夹
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            
            _tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "HK_AREA_SEARCH",
                $"{timestamp}_{uniqueId}"  //  每次运行使用不同的文件夹
            );

            if (!Directory.Exists(_tempDirectory))
            {
                Directory.CreateDirectory(_tempDirectory);
                System.Diagnostics.Debug.WriteLine($"========== Temp Directory Created ==========");
                System.Diagnostics.Debug.WriteLine($"Path: {_tempDirectory}");
                System.Diagnostics.Debug.WriteLine($"===========================================");
            }
        }

        /// <summary>
        /// 创建临时文件路径
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>临时文件完整路径</returns>
        public string CreateTempFile(string fileName)
        {
            lock (_lock)
            {
                string fullPath = Path.Combine(_tempDirectory, fileName);
                
                // 由于每次都是新文件夹,理论上不会冲突
                // 但为了保险,仍然检查
                if (File.Exists(fullPath))
                {
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string extension = Path.GetExtension(fileName);
                    string timestamp = DateTime.Now.ToString("HHmmss");
                    fullPath = Path.Combine(_tempDirectory, $"{fileNameWithoutExt}_{timestamp}{extension}");
                    
                    System.Diagnostics.Debug.WriteLine($"⚠️ File exists, using: {Path.GetFileName(fullPath)}");
                }

                return fullPath;
            }
        }

        /// <summary>
        /// 注册临时文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public void RegisterTempFile(string filePath)
        {
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(filePath) && !_tempFiles.Contains(filePath))
                {
                    _tempFiles.Add(filePath);
                    System.Diagnostics.Debug.WriteLine($"Registered temp file: {Path.GetFileName(filePath)}");
                }
            }
        }

        /// <summary>
        /// 清理所有临时文件和文件夹
        /// </summary>
        public void CleanupAll()
        {
            lock (_lock)
            {
                System.Diagnostics.Debug.WriteLine($"========== Cleanup Started ==========");
                int successCount = 0;
                int failCount = 0;

                // 1. 清理已注册的单个文件
                foreach (string tempFile in _tempFiles.ToList())
                {
                    if (TryDeleteFile(tempFile))
                        successCount++;
                    else
                        failCount++;
                }

                _tempFiles.Clear();

                // 2.  清理整个临时文件夹
                if (Directory.Exists(_tempDirectory))
                {
                    try
                    {
                        // 等待一下,确保所有文件句柄释放
                        System.Threading.Thread.Sleep(500);
                        
                        Directory.Delete(_tempDirectory, true);
                        System.Diagnostics.Debug.WriteLine($"✅ Deleted temp directory: {_tempDirectory}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Failed to delete temp directory: {ex.Message}");
                        // 文件夹删除失败不影响程序运行
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Cleanup result: {successCount} success, {failCount} failed");
                System.Diagnostics.Debug.WriteLine($"====================================");
            }
        }

        /// <summary>
        ///  尝试删除文件 (包括Shapefile相关文件)
        /// </summary>
        private bool TryDeleteFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return true;

                // 如果是 shapefile,删除所有相关文件
                string extension = Path.GetExtension(filePath)?.ToLower();
                if (extension == ".shp")
                {
                    DeleteShapefileSet(filePath);
                }
                else
                {
                    File.Delete(filePath);
                }

                System.Diagnostics.Debug.WriteLine($"✅ Deleted: {Path.GetFileName(filePath)}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Failed to delete {Path.GetFileName(filePath)}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///  删除 Shapefile 的所有关联文件
        /// </summary>
        private void DeleteShapefileSet(string shpPath)
        {
            string directory = Path.GetDirectoryName(shpPath);
            string baseName = Path.GetFileNameWithoutExtension(shpPath);

            string[] extensions = { ".shp", ".shx", ".dbf", ".prj", ".cpg", ".sbn", ".sbx", ".shp.xml", ".lock" };

            foreach (var ext in extensions)
            {
                string file = Path.Combine(directory, baseName + ext);
                if (File.Exists(file))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // 忽略单个文件删除失败
                    }
                }
            }
        }

        /// <summary>
        /// 按模式清理
        /// </summary>
        /// <param name="pattern">文件名模式</param>
        public void CleanupByPattern(string pattern)
        {
            lock (_lock)
            {
                foreach (string tempFile in _tempFiles.Where(f => Path.GetFileName(f).Contains(pattern)).ToList())
                {
                    TryDeleteFile(tempFile);
                    _tempFiles.Remove(tempFile);
                }
            }
        }

        /// <summary>
        /// 获取临时文件夹路径
        /// </summary>
        public string GetTempDirectory() => _tempDirectory;

        /// <summary>
        /// 获取注册的临时文件列表
        /// </summary>
        public List<string> GetTempFiles => new List<string>(_tempFiles);

        /// <summary>
        /// ⭐ 新增: 静态方法 - 清理旧的临时文件夹 (可选)
        /// </summary>
        /// <param name="daysOld">清理几天前的文件夹</param>
        public static void CleanupOldTempFolders(int daysOld = 7)
        {
            try
            {
                string parentFolder = Path.Combine(Path.GetTempPath(), "HK_AREA_SEARCH");
                
                if (!Directory.Exists(parentFolder))
                    return;

                var oldFolders = Directory.GetDirectories(parentFolder)
                    .Where(dir => 
                    {
                        var info = new DirectoryInfo(dir);
                        return (DateTime.Now - info.CreationTime).TotalDays > daysOld;
                    });

                int cleanedCount = 0;
                foreach (var folder in oldFolders)
                {
                    try
                    {
                        Directory.Delete(folder, true);
                        cleanedCount++;
                        System.Diagnostics.Debug.WriteLine($"✅ Cleaned old temp folder: {Path.GetFileName(folder)}");
                    }
                    catch
                    {
                        // 忽略删除失败
                    }
                }

                if (cleanedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Cleaned {cleanedCount} old temp folders");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cleaning old temp folders: {ex.Message}");
            }
        }
    }
}