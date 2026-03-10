using System;
using System.Linq;  
using System.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Core.Data;  
using HK_AREA_SEARCH.Infrastructure.Services;
using HK_AREA_SEARCH.Common;
using System.IO;  

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
                    System.Diagnostics.Debug.WriteLine($"========== Area Filtering Started ==========");
                    System.Diagnostics.Debug.WriteLine($"Input: {inputPath}");
                    
                    // 1. 确保输入文件有 Shape_Area 字段
                    await EnsureAreaFieldAsync(inputPath);

                    string outputPath = _tempFileManager.CreateTempFile("area_filtered.shp");
                    _tempFileManager.RegisterTempFile(outputPath);

                    // 2. 构建SQL查询条件
                    string whereClause = BuildWhereClause(minArea, maxArea);

                    System.Diagnostics.Debug.WriteLine($"Area filter WHERE clause: {whereClause}");

                    // 3. 使用 Select 工具根据面积筛选
                    var parameters = Geoprocessing.MakeValueArray(
                        inputPath,
                        outputPath,
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
                        var errorMessages = string.Join("; ", 
                            result.ErrorMessages.Select(m => m.Text));
                        
                        System.Diagnostics.Debug.WriteLine($"[ERROR] Select tool failed: {errorMessages}");
                        
                        foreach (var msg in result.Messages)
                        {
                            System.Diagnostics.Debug.WriteLine($"[GP Message] {msg.Type}: {msg.Text}");
                        }
                        
                        throw new Exception($"Area filtering failed: {errorMessages}");
                    }

                    System.Diagnostics.Debug.WriteLine($"[INFO] ✓ Area filtering completed: {outputPath}");
                    return outputPath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Area filtering error: {ex.Message}");
                    throw new Exception($"Area filtering error: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// 确保文件有 Shape_Area 字段并已计算面积
        /// </summary>
        private async Task EnsureAreaFieldAsync(string inputPath)
        {
            System.Diagnostics.Debug.WriteLine($"Checking Shape_Area field...");
            
            try
            {
                string directory = Path.GetDirectoryName(inputPath);
                string filename = Path.GetFileNameWithoutExtension(inputPath);

                var fileConnection = new FileSystemConnectionPath(new Uri(directory), FileSystemDatastoreType.Shapefile);
                using (var datastore = new FileSystemDatastore(fileConnection))
                using (var table = datastore.OpenDataset<Table>(filename))
                {
                    var definition = table.GetDefinition();
                    var fields = definition.GetFields();

                    // 检查是否已有 Shape_Area 字段
                    var areaField = fields.FirstOrDefault(f => 
                        f.Name.Equals("Shape_Area", StringComparison.OrdinalIgnoreCase) ||
                        f.Name.Equals("Shape_Ar", StringComparison.OrdinalIgnoreCase));

                    if (areaField != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"✓ Found existing area field: {areaField.Name}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Field check failed: {ex.Message}");
            }

            // 如果没有字段，添加并计算
            System.Diagnostics.Debug.WriteLine($"Adding Shape_Area field...");

            // 1. 添加字段 (Shapefile 字段名最多10个字符，所以用 Shape_Area)
            var addFieldParams = Geoprocessing.MakeValueArray(
                inputPath,
                "Shape_Area",
                "DOUBLE",
                null,
                null,
                null,
                "",
                "NULLABLE"
            );

            var addFieldResult = await Geoprocessing.ExecuteToolAsync(
                "AddField_management",
                addFieldParams,
                null,
                null,
                null,
                GPExecuteToolFlags.AddToHistory
            );

            if (addFieldResult.IsFailed)
            {
                var errorMsg = string.Join("; ", addFieldResult.ErrorMessages.Select(m => m.Text));
                throw new Exception($"Failed to add Shape_Area field: {errorMsg}");
            }

            System.Diagnostics.Debug.WriteLine($"✓ Shape_Area field added");

            // 2. 计算面积（使用 !shape.area! 获取面积，单位取决于数据的坐标系）
            var calcParams = Geoprocessing.MakeValueArray(
                inputPath,
                "Shape_Area",
                "!shape.area!",
                "PYTHON3",
                "",
                "DOUBLE"
            );

            var calcResult = await Geoprocessing.ExecuteToolAsync(
                "CalculateField_management",
                calcParams,
                null,
                null,
                null,
                GPExecuteToolFlags.AddToHistory
            );

            if (calcResult.IsFailed)
            {
                var errorMsg = string.Join("; ", calcResult.ErrorMessages.Select(m => m.Text));
                throw new Exception($"Failed to calculate area: {errorMsg}");
            }

            System.Diagnostics.Debug.WriteLine($"✓ Area calculated successfully");
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

            // 使用 InvariantCulture 格式化数字，避免区域设置问题
            var minAreaStr = actualMinArea.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var maxAreaStr = maxArea?.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (maxArea.HasValue)
            {
                // 有最大面积限制
                if (actualMinArea > 0)
                {
                    // 有最小和最大限制
                    return $"Shape_Area >= {minAreaStr} AND Shape_Area <= {maxAreaStr}";
                }
                else
                {
                    // 只有最大限制
                    return $"Shape_Area <= {maxAreaStr}";
                }
            }
            else
            {
                // 没有最大面积限制
                if (actualMinArea > 0)
                {
                    // 只有最小限制
                    return $"Shape_Area >= {minAreaStr}";
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