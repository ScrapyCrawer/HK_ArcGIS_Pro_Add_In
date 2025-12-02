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
    /// 栅格转矢量转换器
    /// </summary>
    public class RasterToVectorConverter
    {
        private readonly TempFileManager _tempFileManager;

        public RasterToVectorConverter(TempFileManager tempFileManager)
        {
            _tempFileManager = tempFileManager ?? throw new ArgumentNullException(nameof(tempFileManager));
        }

        /// <summary>
        /// 执行转换
        /// </summary>
        /// <param name="inputRasterPath">输入栅格路径</param>
        /// <returns>转换后的矢量路径</returns>
        public async Task<string> ConvertAsync(string inputRasterPath)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    // 1. 验证输入
                    ValidateInput(inputRasterPath);

                    System.Diagnostics.Debug.WriteLine($"========== 栅格转矢量开始 ==========");
                    System.Diagnostics.Debug.WriteLine($"输入栅格: {inputRasterPath}");

                    // 2. 转换为整型栅格 (关键步骤!)
                    string intRasterPath = await ConvertToIntegerRaster(inputRasterPath);
                    System.Diagnostics.Debug.WriteLine($"整型栅格: {intRasterPath}");

                    // 3. 创建输出路径
                    string outputVectorPath = _tempFileManager.CreateTempFile("rating.shp");
                    _tempFileManager.RegisterTempFile(outputVectorPath);
                    System.Diagnostics.Debug.WriteLine($"输出矢量: {outputVectorPath}");

                    // 4. 执行转换
                    await ExecuteRasterToPolygon(intRasterPath, outputVectorPath);

                    // 5. 验证输出
                    ValidateOutput(outputVectorPath);

                    System.Diagnostics.Debug.WriteLine($"========== 栅格转矢量成功 ==========");
                    return outputVectorPath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"========== 栅格转矢量失败 ==========");
                    System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"堆栈: {ex.StackTrace}");
                    throw new Exception($"栅格转矢量失败: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// 将浮点型栅格转换为整型栅格
        /// </summary>
        /// <param name="inputRasterPath">输入浮点型栅格</param>
        /// <returns>整型栅格路径</returns>
        private async Task<string> ConvertToIntegerRaster(string inputRasterPath)
        {
            try
            {
                string intRasterPath = _tempFileManager.CreateTempFile("rating_int.tif");
                _tempFileManager.RegisterTempFile(intRasterPath);

                System.Diagnostics.Debug.WriteLine(">>> 步骤1: 转换浮点型为整型");
                System.Diagnostics.Debug.WriteLine($"    使用工具: Int (Spatial Analyst)");

                // 使用 Int 工具四舍五入并转为整型
                var parameters = Geoprocessing.MakeValueArray(
                    inputRasterPath,    // 输入栅格
                    intRasterPath       // 输出栅格
                );

                var result = await Geoprocessing.ExecuteToolAsync(
                    "sa.Int",          // Spatial Analyst 的 Int 工具
                    parameters,
                    null,
                    null,
                    null,
                    GPExecuteToolFlags.AddToHistory
                );

                // 输出消息
                if (result.Messages != null)
                {
                    System.Diagnostics.Debug.WriteLine("    GP工具消息:");
                    foreach (var msg in result.Messages)
                    {
                        System.Diagnostics.Debug.WriteLine($"      [{msg.Type}] {msg.Text}");
                    }
                }

                if (result.IsFailed)
                {
                    var errors = result.ErrorMessages?.Select(e => e.Text).ToList();
                    string errorMsg = errors != null && errors.Any() 
                        ? string.Join("; ", errors) 
                        : "未知错误";
                    throw new Exception($"转换为整型失败: {errorMsg}");
                }

                System.Diagnostics.Debug.WriteLine("    >>> 整型转换成功!");
                return intRasterPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"!!! 整型转换异常: {ex.Message}");
                throw new Exception($"转换为整型栅格时发生错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 验证输入文件
        /// </summary>
        private void ValidateInput(string inputRasterPath)
        {
            if (string.IsNullOrWhiteSpace(inputRasterPath))
            {
                throw new ArgumentException("输入栅格路径不能为空");
            }

            if (!File.Exists(inputRasterPath))
            {
                throw new FileNotFoundException($"输入栅格文件不存在: {inputRasterPath}");
            }

            var fileInfo = new FileInfo(inputRasterPath);
            System.Diagnostics.Debug.WriteLine($"输入文件大小: {fileInfo.Length} 字节");
            
            if (fileInfo.Length == 0)
            {
                throw new Exception("输入栅格文件为空");
            }
        }

        /// <summary>
        /// 执行栅格转多边形工具
        /// </summary>
        private async Task ExecuteRasterToPolygon(string inputRasterPath, string outputVectorPath)
        {
            System.Diagnostics.Debug.WriteLine(">>> 步骤2: 栅格转多边形");
            System.Diagnostics.Debug.WriteLine($"    使用工具: RasterToPolygon");

            // 构建参数
            var parameters = Geoprocessing.MakeValueArray(
                inputRasterPath,      // 输入栅格 (现在是整型了!)
                outputVectorPath,     // 输出要素类
                "NO_SIMPLIFY",        // 不简化 (避免几何问题)
                "Value"               // 值字段
            );

            // 执行工具
            var result = await Geoprocessing.ExecuteToolAsync(
                "RasterToPolygon_conversion",
                parameters,
                null,
                null,
                null,
                GPExecuteToolFlags.AddToHistory
            );

            // 输出消息
            if (result.Messages != null)
            {
                System.Diagnostics.Debug.WriteLine("    GP工具消息:");
                foreach (var msg in result.Messages)
                {
                    System.Diagnostics.Debug.WriteLine($"      [{msg.Type}] {msg.Text}");
                }
            }

            // 检查结果
            if (result.IsFailed)
            {
                HandleToolFailure(result);
            }

            System.Diagnostics.Debug.WriteLine("    >>> 栅格转多边形成功!");
        }

        /// <summary>
        /// 处理工具执行失败
        /// </summary>
        private void HandleToolFailure(IGPResult result)
        {
            var errors = result.ErrorMessages?.Select(e => e.Text).ToList();
            var messages = result.Messages?.Select(m => m.Text).ToList();

            System.Diagnostics.Debug.WriteLine("!!! 工具执行失败:");
            
            if (errors != null && errors.Any())
            {
                foreach (var error in errors)
                {
                    System.Diagnostics.Debug.WriteLine($"    错误: {error}");
                }
                throw new Exception($"栅格转多边形失败: {string.Join("; ", errors)}");
            }
            
            if (messages != null && messages.Any())
            {
                System.Diagnostics.Debug.WriteLine("    所有消息:");
                foreach (var msg in messages)
                {
                    System.Diagnostics.Debug.WriteLine($"      {msg}");
                }
            }

            throw new Exception("栅格转多边形失败: 工具执行失败但未返回具体错误信息。");
        }

        /// <summary>
        /// 验证输出文件
        /// </summary>
        private void ValidateOutput(string outputVectorPath)
        {
            if (!File.Exists(outputVectorPath))
            {
                throw new Exception($"工具执行完成但输出文件不存在: {outputVectorPath}");
            }

            var fileInfo = new FileInfo(outputVectorPath);
            System.Diagnostics.Debug.WriteLine($"输出文件大小: {fileInfo.Length} 字节");

            if (fileInfo.Length == 0)
            {
                throw new Exception("输出文件为空，输入栅格可能没有有效数据");
            }
        }

        /// <summary>
        /// 简化面要素
        /// </summary>
        /// <param name="inputVectorPath">输入矢量路径</param>
        /// <returns>简化后的矢量路径</returns>
        public async Task<string> SimplifyPolygons(string inputVectorPath)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    string outputVectorPath = _tempFileManager.CreateTempFile("simplified.shp");
                    _tempFileManager.RegisterTempFile(outputVectorPath);

                    var parameters = Geoprocessing.MakeValueArray(
                        inputVectorPath,
                        outputVectorPath,
                        "POINT_REMOVE",
                        "10 Meters"
                    );

                    var result = await Geoprocessing.ExecuteToolAsync(
                        "SimplifyPolygon_cartography",
                        parameters,
                        null,
                        null,
                        null,
                        GPExecuteToolFlags.AddToHistory
                    );

                    if (result.IsFailed)
                    {
                        System.Diagnostics.Debug.WriteLine($"简化多边形失败，返回原始文件");
                        return inputVectorPath;
                    }

                    return outputVectorPath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"简化面要素异常: {ex.Message}");
                    return inputVectorPath;
                }
            });
        }
    }
}