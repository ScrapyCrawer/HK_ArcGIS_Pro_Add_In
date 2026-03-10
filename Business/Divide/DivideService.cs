using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HK_AREA_SEARCH.Infrastructure.Services;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace HK_AREA_SEARCH.Business.Divide
{
    /// <summary>
    /// 可建设土地划分服务实现
    /// </summary>
    public class DivideService : HK_AREA_SEARCH.Divide.IDivideService
    {
        private readonly TempFileManager _tempFileManager;

        public DivideService(TempFileManager tempFileManager)
        {
            _tempFileManager = tempFileManager ?? throw new ArgumentNullException(nameof(tempFileManager));
        }

        /// <summary>
        /// 执行可建设土地划分
        /// </summary>
        public async Task<string> ExecuteAsync(
            string analysisAreaPath,
            List<string> constraintPaths,
            string outputPath,
            double? minArea = null,
            double? maxArea = null)
        {
            try
            {
                LogService.LogInfo("║  Divide Service - Execute Started      ║");
                
                await ValidateInputs(analysisAreaPath, constraintPaths);
                
                // 不再合并约束条件，直接逐个擦除
                LogService.LogInfo("Step 1: Iteratively erasing constraints...");
                string suitableAreaPath = await IterativeErase(analysisAreaPath, constraintPaths);

                LogService.LogInfo("Step 2: Filtering by area...");
                string filteredResultPath = await FilterByArea(suitableAreaPath, minArea, maxArea);  // ⚠️ 直接使用 suitableAreaPath

                if (filteredResultPath != outputPath)
                {
                    LogService.LogInfo("Step 3: Copying to output path...");
                    await CopyToOutputPath(filteredResultPath, outputPath);
                }

                LogService.LogInfo("✅ Divide Service completed successfully");
                LogService.LogInfo($"   Output: {outputPath}\n");

                return outputPath;
            }
            catch (Exception ex)
            {
                // 尝试记录日志，忽略错误
                try { LogService.LogError($"Divide Service failed: {ex.Message}"); } catch { }
                
                // 抛出包含完整堆栈信息的异常
                throw new Exception($"划分可建设土地失败。\n\n>>> 错误详情:\n{ex.Message}\n\n>>> 堆栈跟踪:\n{ex.StackTrace}", ex);
            }
        }

        /// <summary>
        /// 验证输入数据
        /// </summary>
        private static async Task ValidateInputs(string analysisAreaPath, List<string> constraintPaths)
        {
            await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(analysisAreaPath))
                    throw new ArgumentException("分析区域路径不能为空");

                if (constraintPaths == null || constraintPaths.Count == 0)
                    throw new ArgumentException("约束条件路径列表不能为空");

                if (!File.Exists(analysisAreaPath))
                    throw new FileNotFoundException($"分析区域文件不存在: {analysisAreaPath}");

                foreach (var path in constraintPaths)
                {
                    if (!File.Exists(path))
                        throw new FileNotFoundException($"约束条件文件不存在: {path}");
                }
            });
        }

        /// <summary>
        /// 迭代擦除约束条件
        /// 思路: 从分析区域开始，逐个擦除每个约束条件
        /// </summary>
        private async Task<string> IterativeErase(string analysisAreaPath, List<string> constraintPaths)
        {   
            // 当前工作路径，初始为分析区域
            string currentPath = analysisAreaPath;
            
            for (int i = 0; i < constraintPaths.Count; i++)
            {
                string constraintPath = constraintPaths[i];
                string constraintName = Path.GetFileNameWithoutExtension(constraintPath);
                
                // 创建临时输出路径
                string tempOutputPath = _tempFileManager.CreateTempFile($"erase_step_{i + 1}.shp");
                _tempFileManager.RegisterTempFile(tempOutputPath);
                
                // 执行擦除操作
                currentPath = await GeometryProcessor.Difference(currentPath, constraintPath, tempOutputPath);
                
                LogService.LogInfo($"       ✓ Step {i + 1} completed");
            }
            
            LogService.LogInfo($"   ✅ All constraints erased successfully");
            return currentPath;
        }

        /// <summary>
        /// 面积过滤 - 过滤不在面积范围内的地块
        /// </summary>
        private async Task<string> FilterByArea(string inputPath, double? minArea, double? maxArea)
        {
            // 如果最小和最大面积都为null,跳过过滤
            if (minArea == null && maxArea == null)
            {
                LogService.LogInfo("Area filter disabled - skipping");
                return inputPath;
            }

            var areaFilter = new HK_AREA_SEARCH.Divide.AreaFilter(_tempFileManager);
            string filteredPath = await areaFilter.FilterByAreaAsync(inputPath, minArea, maxArea);

            // 获取并输出统计信息
            string statistics = await areaFilter.GetFilterStatistics(inputPath, filteredPath);
            LogService.LogInfo("========== Area Filter Statistics ==========");
            LogService.LogInfo(statistics);
            LogService.LogInfo("============================================");

            return filteredPath;
        }

        /// <summary>
        /// 复制到最终输出路径
        /// </summary>
        private static async Task CopyToOutputPath(string sourcePath, string targetPath)
        {
            await Task.Run(() =>
            {
                try
                {
                    LogService.LogInfo($"Copying from {Path.GetFileName(sourcePath)} to {Path.GetFileName(targetPath)}");
                    
                    // 复制 shapefile 及其相关文件
                    string sourceDir = Path.GetDirectoryName(sourcePath);
                    string targetDir = Path.GetDirectoryName(targetPath);
                    string sourceBase = Path.GetFileNameWithoutExtension(sourcePath);
                    string targetBase = Path.GetFileNameWithoutExtension(targetPath);

                    // Shapefile 相关扩展名
                    string[] extensions = { ".shp", ".shx", ".dbf", ".prj", ".sbn", ".sbx", ".cpg" };

                    int copiedCount = 0;
                    foreach (var ext in extensions)
                    {
                        string sourceFile = Path.Combine(sourceDir, sourceBase + ext);
                        string targetFile = Path.Combine(targetDir, targetBase + ext);

                        if (File.Exists(sourceFile))
                        {
                            File.Copy(sourceFile, targetFile, true);
                            copiedCount++;
                        }
                    }
                    
                    LogService.LogInfo($"Copied {copiedCount} files successfully");
                }
                catch (Exception ex)
                {
                    LogService.LogWarning($"Copy to output failed: {ex.Message}");
                    throw;
                }
            });
        }
    }
}