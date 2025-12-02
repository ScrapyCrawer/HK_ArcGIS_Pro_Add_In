using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping;
using HK_AREA_SEARCH.Models;
using HK_AREA_SEARCH.Infrastructure.Services;
using HK_AREA_SEARCH.Infrastructure.Helpers;
using System.Reflection;


namespace HK_AREA_SEARCH.Distance
{
    /// <summary>
    /// 栅格重分类器
    /// </summary>
    public class RasterReclassifier
    {
        private readonly TempFileManager _tempFileManager;

        public RasterReclassifier(TempFileManager tempFileManager)
        {
            _tempFileManager = tempFileManager ?? throw new ArgumentNullException(nameof(tempFileManager));
        }

        /// <summary>
        /// 获取栅格的最小值和最大值
        /// </summary>
        /// <param name="rasterPath">栅格文件路径</param>
        /// <returns>包含最小值和最大值的元组</returns>
        public async Task<(double minValue, double maxValue)> GetRasterMinMaxAsync(string rasterPath)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    var parameters = Geoprocessing.MakeValueArray(
                        rasterPath,           // 输入栅格
                        "MINIMUM"             // 获取最小值属性
                    );
                    
                    var resultMin = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters);
                    double minVal = 0;
                    if (!resultMin.IsFailed)
                    {
                        string minStr = resultMin.ReturnValue?.ToString() ?? "0";
                        double.TryParse(minStr, out minVal);
                    }

                    parameters = Geoprocessing.MakeValueArray(
                        rasterPath,           // 输入栅格
                        "MAXIMUM"             // 获取最大值属性
                    );
                    
                    var resultMax = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters);
                    double maxVal = 0;
                    if (!resultMax.IsFailed)
                    {
                        string maxStr = resultMax.ReturnValue?.ToString() ?? "1000";
                        double.TryParse(maxStr, out maxVal);
                    }

                    return (minVal, maxVal);
                }
                catch
                {
                    // 如果无法获取精确统计信息，返回默认值
                    return (0.0, 1000.0); // 默认范围
                }
            });
        }

        /// <summary>
        /// 创建等间隔分类
        /// </summary>
        /// <param name="inputRasterPath">输入栅格路径</param>
        /// <param name="numClasses">分类数量</param>
        /// <param name="poiItem">POI的所有输入内容</param>
        /// <returns>重分类后的栅格路径</returns>
        public async Task<string> CreateEqualIntervalClasses(string inputRasterPath, int numClasses, POIDataItem poiItem)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    string outputRasterPath = _tempFileManager.CreateTempFile("reclassification.tif");
                    _tempFileManager.RegisterTempFile(outputRasterPath);

                    // 直接使用重分类工具进行等间隔分类
                    // 构建等间隔重分类表达式
                    string reclassExpression = await BuildEqualIntervalExpression(inputRasterPath, poiItem, numClasses);

                    var parameters = Geoprocessing.MakeValueArray(
                        inputRasterPath,      // 输入栅格
                        "VALUE",             // 重分类字段（注意是大写）
                        reclassExpression,   // 重映射表达式
                        outputRasterPath,    // 输出栅格
                        "DATA",              // 缺失值处理
                        ""                   // WHERE子句
                    );

                    // 环境变量
                    var environments = Geoprocessing.MakeEnvironmentArray(
                        cellSize: null,
                        extent: null,
                        mask: null,
                        outputCoordinateSystem: null,
                        scratchWorkspace: null,
                        workspace: null
                    );

                    // 执行重分类工具
                    var result = await Geoprocessing.ExecuteToolAsync("sa.Reclassify", parameters, environments, null, null, GPExecuteToolFlags.AddToHistory);

                    if (result.IsFailed)
                    {
                        string errorMessages = string.Join("; ", result.ErrorMessages);
                        System.Diagnostics.Debug.WriteLine($"Equal interval classification failed: {errorMessages}");
                    }

                    return outputRasterPath;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error creating equal interval classification: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// 构建等间隔重分类表达式
        /// </summary>
        private async Task<string> BuildEqualIntervalExpression(string inputRasterPath, POIDataItem poiItem, int numClasses)
        {
            return await QueuedTask.Run(async () =>
            {
                // 获取栅格的实际最小值和最大值
                var (rasterMin, rasterMax) = await GetRasterMinMaxAsync(inputRasterPath);
                
                // 如果无法获取有效的栅格统计信息，回退到使用距离值
                if (rasterMin == 0 && rasterMax == 1000 && poiItem?.Distance.HasValue == true) 
                {
                    int distance = poiItem?.Distance != null ? Math.Abs(poiItem.Distance.Value) : 1000;   // 使用绝对值
                    rasterMin = 0;
                    rasterMax = distance;
                }

                var reclassRules = new List<string>();
                
                double minValue = rasterMin;
                double maxValue = rasterMax;
                double interval = (maxValue - minValue) / numClasses;

                // 确定分类值的顺序：如果距离为正或未知，则反向（高值对应低分类），否则正向
                bool invertValues = poiItem?.Distance != null ? poiItem.Distance > 0 : true; 

                if (invertValues)
                {
                    // 如果距离为正或未知（栅格数据），分类值从高到低（反向评分，值越小分越高）
                    for (int i = 0; i < numClasses; i++)
                    {
                        double fromValue = minValue + (i * interval);
                        double toValue = (i == numClasses - 1) ? maxValue : minValue + ((i + 1) * interval); // 确保最后一个区间包含最大值
                        int classValue = numClasses - i; // 反转分类值

                        reclassRules.Add($"{fromValue} {toValue} {classValue}");
                    }
                }
                else
                {
                    // 如果距离为负，分类值从低到高
                    for (int i = 0; i < numClasses; i++)
                    {
                        double fromValue = minValue + (i * interval);
                        double toValue = (i == numClasses - 1) ? maxValue : minValue + ((i + 1) * interval); // 确保最后一个区间包含最大值
                        int classValue = i + 1;

                        reclassRules.Add($"{fromValue} {toValue} {classValue}");
                    }
                }

                return string.Join(";", reclassRules);
            });
        }

        /// <summary>
        /// 创建自定义分类
        /// </summary>
        /// <param name="inputRasterPath">输入栅格路径</param>
        /// <param name="classItems">分类项列表</param>
        /// <returns>重分类后的栅格路径</returns>
        public async Task<string> CreateCustomClasses(string inputRasterPath, List<IntervalClassItem> classItems)
        {
            // 构建重分类表达式
            string remapExpression = BuildRemapExpression(classItems);

            return await QueuedTask.Run(async () =>
            {
                try
                {
                    string outputRasterPath = _tempFileManager.CreateTempFile("custom_classification.tif");
                    _tempFileManager.RegisterTempFile(outputRasterPath);

                    // 使用重分类工具
                    var parameters = Geoprocessing.MakeValueArray(
                        inputRasterPath,      // 输入栅格
                        "VALUE",             // 重分类字段
                        remapExpression,     // 重映射表达式
                        outputRasterPath,    // 输出栅格
                        "NoDATA",              // 缺失值处理
                        ""                   // WHERE子句
                    );

                    // 执行重分类工具
                    var result = await Geoprocessing.ExecuteToolAsync("sa.Reclassify", parameters,
                        // 设置合适的环境参数
                        Geoprocessing.MakeEnvironmentArray(
                            cellSize: null,
                            extent: null,
                            mask: null,
                            outputCoordinateSystem: null,
                            scratchWorkspace: null,
                            workspace: null
                        ), null, null, GPExecuteToolFlags.AddToHistory);

                    if (result.IsFailed)
                    {
                        string errorMessages = string.Join("; ", result.ErrorMessages);
                        throw new Exception($"Custom classification failed: {errorMessages}");
                    }

                    return outputRasterPath;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error creating custom classification: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// 构建重分类表达式
        /// </summary>
        private string BuildRemapExpression(List<IntervalClassItem> classItems)
        {
            // 为ArcGIS重分类工具构建正确的重映射字符串
            // 格式: "from1 to1 value1;from2 to2 value2;..." 注意使用分号分隔
            var remapPairs = new List<string>();

            foreach (var item in classItems)
            {
                // 检查是否为空项
                if (item.IsEmpty()) return "";

                // 构建正确的重分类规则
                // 每个规则格式: 起始值 结束值 新值
                remapPairs.Add($"{item.StartValue} {item.EndValue} {item.ClassValue}");
            }

            // 用分号连接所有重映射对
            return string.Join(";", remapPairs);
        }
    }
}