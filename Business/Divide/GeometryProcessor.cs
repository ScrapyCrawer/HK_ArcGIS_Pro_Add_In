using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Internal.Core;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using HK_AREA_SEARCH.Common;
using HK_AREA_SEARCH.Infrastructure.Services;
using HK_AREA_SEARCH.Infrastructure.Helpers;

namespace HK_AREA_SEARCH.Divide
{
    /// <summary>
    /// 几何处理器
    /// </summary>
    public class GeometryProcessor
    {
        /// <summary>
        /// 合并多个要素
        /// </summary>
        /// <param name="inputPaths">输入要素路径列表</param>
        /// <param name="tempFileManager">临时文件管理器</param>
        /// <returns>合并后要素路径</returns>
        public async Task<string> Union(List<string> inputPaths, TempFileManager tempFileManager)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    // 如果只有一个输入，直接返回
                    if (inputPaths.Count == 1)
                        return inputPaths[0];

                    // 创建临时输出文件路径
                    string outputPath = tempFileManager.CreateTempFile("MergedConstraints.shp");
                    tempFileManager.RegisterTempFile(outputPath);

                    // 使用ArcGIS Pro的Union工具，设置环境参数以防止自动添加到地图
                    var environment = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);
                    var parameters = Geoprocessing.MakeValueArray(inputPaths, outputPath);
                    var result = await Geoprocessing.ExecuteToolAsync("analysis.Union", parameters, environment, null, null, GPExecuteToolFlags.AddToHistory);

                    if (result.IsFailed)
                    {
                        // 将错误消息列表转换为字符串
                        string errorMessages = string.Join("; ", result.ErrorMessages);
                        throw new Exception($"合并约束条件失败: {errorMessages}");
                    }

                    return outputPath;
                }
                catch (Exception ex)
                {
                    throw new Exception($"几何合并操作失败: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// 差集运算
        /// </summary>
        /// <param name="inputPath1">被减要素路径（分析区域）</param>
        /// <param name="inputPath2">减数要素路径（约束条件）</param>
        /// <param name="outputPath">输出路径</param>
        /// <returns>差集运算结果路径</returns>
        public async Task<string> Difference(string inputPath1, string inputPath2, string outputPath)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    // 使用ArcGIS Pro的Erase工具
                    var environment = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);
                    var parameters = Geoprocessing.MakeValueArray(inputPath1, inputPath2, outputPath);

                    var result = await Geoprocessing.ExecuteToolAsync(
                        "analysis.Erase", 
                        parameters, 
                        environment, 
                        null, 
                        null, 
                        GPExecuteToolFlags.None); 

                    if (result.IsFailed)
                    {
                        string errorMessages = string.Join("; ", result.ErrorMessages);
                        throw new Exception($"差集运算失败: {errorMessages}");
                    }

                    // 执行 Multipart To Singlepart 确保结果一致性
                    // 如果不需要拆分多部件几何，可以注释掉这段
                    // string singlepartOutput = outputPath.Replace(".shp", "_single.shp");
                    // await ConvertMultipartToSinglepart(outputPath, singlepartOutput);
                    // return singlepartOutput;

                    return outputPath;
                }
                catch (Exception ex)
                {
                    throw new Exception($"几何差集运算失败: {ex.Message}", ex);
                }
            });
        }
    }
}