using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HK_AREA_SEARCH.Infrastructure.Services;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace HK_AREA_SEARCH.Divide
{
    /// <summary>
    /// 可建设土地划分服务实现
    /// </summary>
    public class DivideService : IDivideService
    {
        private readonly TempFileManager _tempFileManager;

        public DivideService(TempFileManager tempFileManager)
        {
            _tempFileManager = tempFileManager ?? throw new ArgumentNullException(nameof(tempFileManager));
        }

        /// <summary>
        /// 执行可建设土地划分
        /// </summary>
        /// <param name="analysisAreaPath">分析区域路径</param>
        /// <param name="constraintPaths">约束条件路径列表</param>
        /// <param name="outputPath">输出路径</param>
        /// <param name="minArea">最小面积阈值(平方米),null则使用默认值200.7</param>
        /// <param name="maxArea">最大面积阈值(平方米),null则不限制</param>
        /// <returns>生成的可建设土地文件路径</returns>
        public async Task<string> ExecuteAsync(
            string analysisAreaPath,
            List<string> constraintPaths,
            string outputPath,
            double? minArea = null,
            double? maxArea = null)
        {
            try
            {
                await ValidateInputs(analysisAreaPath, constraintPaths);
                
                string mergedConstraintsPath = await MergeConstraints(constraintPaths);
                string differenceResultPath = await PerformDifference(analysisAreaPath, mergedConstraintsPath, outputPath);
                string filteredResultPath = await FilterByArea(differenceResultPath, minArea, maxArea);

                if (filteredResultPath != outputPath)
                {
                    await CopyToOutputPath(filteredResultPath, outputPath);
                }

                return outputPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"划分可建设土地失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 验证输入数据
        /// </summary>
        private async Task ValidateInputs(string analysisAreaPath, List<string> constraintPaths)
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
        /// 合并约束条件
        /// </summary>
        private async Task<string> MergeConstraints(List<string> constraintPaths)
        {
            var geometryProcessor = new GeometryProcessor();
            return await geometryProcessor.Union(constraintPaths, _tempFileManager);
        }

        /// <summary>
        /// 执行差集运算
        /// </summary>
        private async Task<string> PerformDifference(string analysisAreaPath, string constraintsPath, string outputPath)
        {
            var geometryProcessor = new GeometryProcessor();

            // 创建临时输出路径
            string tempOutputPath = _tempFileManager.CreateTempFile("difference_result.shp");
            _tempFileManager.RegisterTempFile(tempOutputPath);

            return await geometryProcessor.Difference(analysisAreaPath, constraintsPath, tempOutputPath);
        }

        /// <summary>
        /// 面积过滤 - 过滤不在面积范围内的地块
        /// </summary>
        private async Task<string> FilterByArea(string inputPath, double? minArea, double? maxArea)
        {
            // 如果最小和最大面积都为null,跳过过滤
            if (minArea == null && maxArea == null)
            {
                System.Diagnostics.Debug.WriteLine("Area filter disabled - skipping");
                return inputPath;
            }

            var areaFilter = new AreaFilter(_tempFileManager);
            string filteredPath = await areaFilter.FilterByAreaAsync(inputPath, minArea, maxArea);

            // 获取并输出统计信息
            string statistics = await areaFilter.GetFilterStatistics(inputPath, filteredPath);
            System.Diagnostics.Debug.WriteLine($"========== Area Filter Statistics ==========");
            System.Diagnostics.Debug.WriteLine(statistics);
            System.Diagnostics.Debug.WriteLine($"===========================================");

            return filteredPath;
        }

        /// <summary>
        /// 复制到最终输出路径
        /// </summary>
        private async Task CopyToOutputPath(string sourcePath, string targetPath)
        {
            await Task.Run(() =>
            {
                try
                {
                    // 复制 shapefile 及其相关文件
                    string sourceDir = Path.GetDirectoryName(sourcePath);
                    string targetDir = Path.GetDirectoryName(targetPath);
                    string sourceBase = Path.GetFileNameWithoutExtension(sourcePath);
                    string targetBase = Path.GetFileNameWithoutExtension(targetPath);

                    // Shapefile 相关扩展名
                    string[] extensions = { ".shp", ".shx", ".dbf", ".prj", ".sbn", ".sbx", ".cpg" };

                    foreach (var ext in extensions)
                    {
                        string sourceFile = Path.Combine(sourceDir, sourceBase + ext);
                        string targetFile = Path.Combine(targetDir, targetBase + ext);

                        if (File.Exists(sourceFile))
                        {
                            File.Copy(sourceFile, targetFile, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"警告: 复制到输出路径失败: {ex.Message}");
                }
            });
        }
    }
}