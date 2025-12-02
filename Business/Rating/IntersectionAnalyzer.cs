using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using HK_AREA_SEARCH.Infrastructure.Services;

namespace HK_AREA_SEARCH.Rating
{
    /// <summary>
    /// 相交分析器
    /// </summary>
    public class IntersectionAnalyzer
    {
        private readonly TempFileManager _tempFileManager;

        public IntersectionAnalyzer(TempFileManager tempFileManager)
        {
            _tempFileManager = tempFileManager ?? throw new ArgumentNullException(nameof(tempFileManager));
        }

        /// <summary>
        /// 执行相交
        /// </summary>
        /// <param name="inputPath1">输入要素路径1（评分结果）</param>
        /// <param name="inputPath2">输入要素路径2（可建设土地）</param>
        /// <param name="outputPath">输出路径</param>
        /// <param name="minArea">最小面积阈值(平方米),null则使用默认值200.7</param>
        /// <returns>相交结果路径</returns>
        public async Task<string> IntersectAsync(string inputPath1, string inputPath2, string outputPath, double? minArea = null)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    // 验证输入
                    if (string.IsNullOrEmpty(inputPath1) || string.IsNullOrEmpty(inputPath2))
                    {
                        throw new Exception("输入要素路径不能为空");
                    }

                    if (!File.Exists(inputPath1))
                    {
                        throw new Exception($"输入要素1不存在: {inputPath1}");
                    }

                    if (!File.Exists(inputPath2))
                    {
                        throw new Exception($"输入要素2不存在: {inputPath2}");
                    }

                    System.Diagnostics.Debug.WriteLine($"开始相交分析:");
                    System.Diagnostics.Debug.WriteLine($"  输入1: {inputPath1}");
                    System.Diagnostics.Debug.WriteLine($"  输入2: {inputPath2}");
                    System.Diagnostics.Debug.WriteLine($"  输出: {outputPath}");

                    // 使用ArcGIS Pro的相交工具
                    var parameters = Geoprocessing.MakeValueArray(
                        $"{inputPath1};{inputPath2}",  // 输入要素列表（分号分隔）
                        outputPath,                     // 输出要素类
                        "ALL",                         // 连接属性（ALL/NO_FID/ONLY_FID）
                        "",                            // 聚合距离（可选）
                        "INPUT"                        // 输出类型（INPUT保持输入几何类型）
                    );

                    System.Diagnostics.Debug.WriteLine("执行 Intersect 工具...");

                    var result = await Geoprocessing.ExecuteToolAsync(
                        "Intersect_analysis",
                        parameters,
                        null,
                        null,
                        null,
                        GPExecuteToolFlags.AddToHistory
                    );

                    if (result.IsFailed)
                    {
                        var errors = result.ErrorMessages?.Select(e => e.Text).ToList();
                        string errorMessages = errors != null && errors.Any() 
                            ? string.Join("; ", errors) 
                            : "未知错误";
                        
                        System.Diagnostics.Debug.WriteLine($"相交分析失败:");
                        System.Diagnostics.Debug.WriteLine($"  错误: {errorMessages}");
                        
                        throw new Exception($"相交分析失败: {errorMessages}");
                    }

                    System.Diagnostics.Debug.WriteLine($"相交分析成功: {outputPath}");

                    // 验证输出
                    if (!File.Exists(outputPath))
                    {
                        throw new Exception($"相交完成但输出文件不存在: {outputPath}");
                    }

                    // 计算面积
                    await CalculateArea(outputPath);

                    // ⭐ 修改: 使用用户设置的最小面积
                    string filteredPath = await FilterSmallAreas(outputPath, minArea);
                    
                    // 如果过滤后的路径不同,复制回输出路径
                    if (filteredPath != outputPath)
                    {
                        CopyShapefile(filteredPath, outputPath);
                    }

                    return outputPath;
                }
                catch (Exception ex)
                {
                    throw new Exception($"执行相交分析时发生错误: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// 传递属性
        /// </summary>
        /// <param name="targetPath">目标路径</param>
        /// <param name="sourcePath">源路径</param>
        /// <returns>处理后的路径</returns>
        public async Task<string> TransferAttributes(string targetPath, string sourcePath)
        {
            // 属性传递通常在相交过程中自动完成
            await Task.CompletedTask;
            return targetPath;
        }

        /// <summary>
        /// 计算面积
        /// </summary>
        /// <param name="featureClassPath">要素类路径</param>
        /// <returns>异步任务</returns>
        public async Task CalculateArea(string featureClassPath)
        {
            await QueuedTask.Run(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"开始计算面积: {featureClassPath}");

                    // 使用 Calculate Geometry Attributes 工具
                    // 重要: Shapefile 字段名最多10个字符!
                    var parameters = Geoprocessing.MakeValueArray(
                        featureClassPath,               // 输入要素
                        "AREA_SQM AREA",               // 几何属性（字段名 属性类型）- 改为 AREA_SQM (9个字符)
                        "",                             // 长度单位
                        "SQUARE_METERS",               // 面积单位
                        ""                             // 坐标系
                    );

                    var result = await Geoprocessing.ExecuteToolAsync(
                        "CalculateGeometryAttributes_management",
                        parameters,
                        null,
                        null,
                        null,
                        GPExecuteToolFlags.AddToHistory
                    );

                    if (result.IsFailed)
                    {
                        var errors = result.ErrorMessages?.Select(e => e.Text).ToList();
                        string errorMessages = errors != null && errors.Any() 
                            ? string.Join("; ", errors) 
                            : "未知错误";
                        System.Diagnostics.Debug.WriteLine($"计算面积失败: {errorMessages}");
                        // 不抛出异常，只记录日志
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("面积计算成功 - 字段名: AREA_SQM");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"计算面积时发生错误: {ex.Message}");
                    // 不抛出异常，计算面积失败不应该影响主流程
                }
            });
        }

        /// <summary>
        /// 过滤小于阈值的面积
        /// </summary>
        /// <param name="inputPath">输入路径</param>
        /// <param name="minArea">最小面积阈值(平方米),null则使用默认值200.7</param>
        /// <returns>过滤后的路径</returns>
        private async Task<string> FilterSmallAreas(string inputPath, double? minArea)
        {
            try
            {
                // ⭐ 使用用户设置或默认值
                double threshold = minArea ?? 200.7;
                
                System.Diagnostics.Debug.WriteLine(">>> 相交后过滤小面积地块");
                System.Diagnostics.Debug.WriteLine($"    最小面积阈值: {threshold} m²");
                
                string filteredPath = _tempFileManager.CreateTempFile("intersect_filtered.shp");
                _tempFileManager.RegisterTempFile(filteredPath);
                
                // ⭐ 使用动态阈值
                string whereClause = $"AREA_SQM >= {threshold}";
                System.Diagnostics.Debug.WriteLine($"    WHERE 条件: {whereClause}");
                
                var parameters = Geoprocessing.MakeValueArray(
                    inputPath,
                    filteredPath,
                    whereClause
                );
                
                var result = await Geoprocessing.ExecuteToolAsync(
                    "Select_analysis",
                    parameters,
                    null,
                    null,
                    null,
                    GPExecuteToolFlags.AddToHistory
                );
                
                if (result.IsFailed)
                {
                    System.Diagnostics.Debug.WriteLine("过滤失败,返回原始文件");
                    return inputPath;
                }
                
                System.Diagnostics.Debug.WriteLine($"过滤完成: {filteredPath}");
                return filteredPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"过滤异常: {ex.Message}");
                return inputPath;
            }
        }

        /// <summary>
        /// 复制 Shapefile
        /// </summary>
        private void CopyShapefile(string sourcePath, string targetPath)
        {
            try
            {
                string sourceDir = Path.GetDirectoryName(sourcePath);
                string targetDir = Path.GetDirectoryName(targetPath);
                string sourceBase = Path.GetFileNameWithoutExtension(sourcePath);
                string targetBase = Path.GetFileNameWithoutExtension(targetPath);
                
                string[] extensions = { ".shp", ".shx", ".dbf", ".prj", ".cpg", ".sbn", ".sbx" };
                
                foreach (var ext in extensions)
                {
                    string srcFile = Path.Combine(sourceDir, sourceBase + ext);
                    string tgtFile = Path.Combine(targetDir, targetBase + ext);
                    
                    if (File.Exists(srcFile))
                    {
                        File.Copy(srcFile, tgtFile, true);
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Shapefile 复制完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"复制 Shapefile 失败: {ex.Message}");
            }
        }
    }
}