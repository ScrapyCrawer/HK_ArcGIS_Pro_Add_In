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
        /// 差集运算 (Erase)
        /// </summary>
        public static async Task<string> Difference(string inputPath1, string inputPath2, string outputPath)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    LogService.LogInfo("=== Erase Operation ===");
                    LogService.LogInfo($"Input Features: {Path.GetFileName(inputPath1)}");
                    LogService.LogInfo($"Erase Features: {Path.GetFileName(inputPath2)}");
                    LogService.LogInfo($"Output: {Path.GetFileName(outputPath)}");
                    
                    var environment = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);
                    var parameters = Geoprocessing.MakeValueArray(inputPath1, inputPath2, outputPath);

                    LogService.LogInfo("Executing analysis.Erase...");
                    
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
                                LogService.LogError($"  Code: {error.ErrorCode}, Type: {error.Type}, Text: {error.Text}");
                            }
                        }
                        else
                        {
                            LogService.LogError("No error messages returned from GP tool");
                        }
                        
                        if (result.Messages != null && result.Messages.Any())
                        {
                            LogService.LogError("All Messages:");
                            foreach (var msg in result.Messages)
                            {
                                LogService.LogError($"  {msg.Text}");
                            }
                        }

                        string errorDetails = "Erase tool failed";
                        if (result.ErrorMessages != null && result.ErrorMessages.Any())
                        {
                            errorDetails = string.Join("; ", result.ErrorMessages.Select(e => e.Text));
                        }
                        
                        throw new Exception($"差集运算失败: {errorDetails}");
                    }

                    LogService.LogInfo("✓ Erase completed successfully");
                    return outputPath;
                }
                catch (Exception ex)
                {
                    LogService.LogError($"Erase Exception: {ex.Message}");
                    LogService.LogError($"Stack Trace: {ex.StackTrace}");
                    throw new Exception($"几何差集运算失败: {ex.Message}", ex);
                }
            });
        }
    }
}