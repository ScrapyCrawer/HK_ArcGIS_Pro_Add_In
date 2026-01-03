using System;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using HK_AREA_SEARCH.Infrastructure.Services;
using HK_AREA_SEARCH.Common;

namespace HK_AREA_SEARCH.Divide
{
    /// <summary>
    /// 面积过滤器 - 根据面积范围过滤地块
    /// </summary>
    public class AreaFilter
    {
        private readonly TempFileManager _tempFileManager;

        public AreaFilter(TempFileManager tempFileManager)
        {
            _tempFileManager = tempFileManager ?? throw new ArgumentNullException(nameof(tempFileManager));
        }

        /// <summary>
        /// 根据面积范围过滤地块
        /// </summary>
        /// <param name="inputPath">输入要素路径</param>
        /// <param name="minArea">最小面积(平方米),null表示无下限</param>
        /// <param name="maxArea">最大面积(平方米),null表示无上限</param>
        /// <returns>过滤后的要素路径</returns>
        public async Task<string> FilterByAreaAsync(string inputPath, double? minArea, double? maxArea)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    string outputPath = _tempFileManager.CreateTempFile("area_filtered.shp");
                    _tempFileManager.RegisterTempFile(outputPath);

                    // 构建SQL查询条件
                    string whereClause = BuildWhereClause(minArea, maxArea);

                    System.Diagnostics.Debug.WriteLine($"Area filter WHERE clause: {whereClause}");

                    // 使用 Select 工具根据面积筛选
                    var parameters = Geoprocessing.MakeValueArray(
                        inputPath,
                        outputPath,
                        whereClause  // SQL查询条件
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
                        throw new Exception($"Area filtering failed: {result.ErrorMessages}");
                    }

                    return outputPath;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Area filtering error: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// 构建SQL查询条件
        /// </summary>
        private string BuildWhereClause(double? minArea, double? maxArea)
        {
            //  MinArea 留空时使用 0 (无下限)
            double actualMinArea = minArea ?? 0; 

            //  验证:最大面积不能小于最小面积
            if (maxArea.HasValue && maxArea.Value > 0 && maxArea.Value < actualMinArea)
            {
                throw new ArgumentException($"Maximum area ({maxArea.Value}) cannot be less than minimum area ({actualMinArea})");
            }

            //  验证:最大面积如果设置,必须大于0
            if (maxArea.HasValue && maxArea.Value <= 0)
            {
                throw new ArgumentException($"Maximum area must be greater than 0 (current: {maxArea.Value})");
            }

            if (maxArea.HasValue)
            {
                // 有最大面积限制
                if (actualMinArea > 0)
                {
                    // 有最小和最大限制
                    return $"Shape_Area >= {actualMinArea} AND Shape_Area <= {maxArea.Value}";
                }
                else
                {
                    // 只有最大限制
                    return $"Shape_Area <= {maxArea.Value}";
                }
            }
            else
            {
                // 没有最大面积限制
                if (actualMinArea > 0)
                {
                    // 只有最小限制
                    return $"Shape_Area >= {actualMinArea}";
                }
                else
                {
                    // 两个都不限制,返回所有数据
                    return "1=1";  // SQL中的恒真条件
                }
            }
        }

        /// <summary>
        /// 获取过滤统计信息
        /// </summary>
        public async Task<string> GetFilterStatistics(string inputPath, string outputPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 这里可以使用 ArcGIS 的 GetCount 工具获取要素数量
                    return $"Area filtering completed.\n" +
                           $"Input: {inputPath}\n" +
                           $"Output: {outputPath}";
                }
                catch
                {
                    return "Statistics unavailable";
                }
            });
        }
    }
}