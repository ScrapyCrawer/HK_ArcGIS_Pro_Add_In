using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Core;
using ArcGIS.Desktop.Mapping;
using HK_AREA_SEARCH.Common;
using HK_AREA_SEARCH.Infrastructure.Helpers;
using HK_AREA_SEARCH.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HK_AREA_SEARCH.Business.Divide
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
        public static async Task<string> Union(List<string> inputPaths, TempFileManager tempFileManager)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    LogService.LogInfo("=== Union Operation ===");
                    LogService.LogInfo($"Machine: {Environment.MachineName}, User: {Environment.UserName}");
                    LogService.LogInfo($"Input count: {inputPaths?.Count ?? 0}");

                    // 如果只有一个输入，直接返回
                    if (inputPaths.Count == 1)
                        return inputPaths[0];

                    // 创建临时输出文件路径
                    string outputPath = tempFileManager.CreateTempFile("MergedConstraints.shp");
                    tempFileManager.RegisterTempFile(outputPath);
                    
                    LogService.LogInfo($"Output: {outputPath}");

                    // 执行arcgispro的union 工具
                    LogService.LogInfo("Executing analysis.Union...");

                    var environment = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);
                    var parameters = Geoprocessing.MakeValueArray(inputPaths, outputPath);
                    var result = await Geoprocessing.ExecuteToolAsync(
                        "analysis.Union", 
                        parameters,
                        environment,
                        null, 
                        null,
                        GPExecuteToolFlags.AddToHistory);

                    if (result.IsFailed)
                    {
                        LogService.LogError("=== UNION FAILED ===");
                        LogService.LogError($"Return Code: {result.ReturnValue}");
                        
                        // 记录所有错误消息
                        if (result.ErrorMessages != null && result.ErrorMessages.Any())
                        {
                            LogService.LogError("Error Messages:");
                            foreach (var error in result.ErrorMessages)
                            {
                                LogService.LogError($"  {error}");
                            }
                        }
                        
                        // 记录所有附加消息（可能包含更多线索）
                        if (result.Messages != null && result.Messages.Any())
                        {
                            LogService.LogError("All Messages:");
                            foreach (var msg in result.Messages)
                            {
                                LogService.LogError($"  {msg}");
                            }
                        }

                        string errorMessages = string.Join("; ", result.ErrorMessages);
                        throw new Exception($"合并约束条件失败: {errorMessages}");
                    }

                    LogService.LogInfo("Union completed successfully");
                    return outputPath;
                }
                catch (Exception ex)
                {
                    LogService.LogError($"Union Exception: {ex.Message}");
                    throw new Exception($"几何合并操作失败: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// 差集运算
        /// </summary>
        public static async Task<string> Difference(string inputPath1, string inputPath2, string outputPath)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    LogService.LogInfo("=== Difference Operation ===");
                    LogService.LogInfo("Executing analysis.Erase...");
                    
                    var environment = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);
                    var parameters = Geoprocessing.MakeValueArray(inputPath1, inputPath2, outputPath);

                    var result = await Geoprocessing.ExecuteToolAsync(
                        "analysis.Erase", 
                        parameters, 
                        environment, 
                        null, 
                        null, 
                        GPExecuteToolFlags.AddToHistory);

                    if (result.IsFailed)
                    {
                        LogService.LogError("=== ERASE FAILED ===");
                        LogService.LogError($"Return Code: {result.ReturnValue}");
                        
                        if (result.ErrorMessages != null && result.ErrorMessages.Any())
                        {
                            LogService.LogError("Error Messages:");
                            foreach (var error in result.ErrorMessages)
                            {
                                LogService.LogError($"  {error}");
                            }
                        }
                        
                        if (result.Messages != null && result.Messages.Any())
                        {
                            LogService.LogError("All Messages:");
                            foreach (var msg in result.Messages)
                            {
                                LogService.LogError($"  {msg}");
                            }
                        }

                        string errorMessages = string.Join("; ", result.ErrorMessages);
                        throw new Exception($"差集运算失败: {errorMessages}");
                    }

                    LogService.LogInfo("Erase completed successfully");
                    return outputPath;
                }
                catch (Exception ex)
                {
                    LogService.LogError($"Difference Exception: {ex.Message}");
                    throw new Exception($"几何差集运算失败: {ex.Message}", ex);
                }
            });
        }
    }
}