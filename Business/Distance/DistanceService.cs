using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using HK_AREA_SEARCH.Business.Distance;
using HK_AREA_SEARCH.Infrastructure.Common;
using HK_AREA_SEARCH.Infrastructure.Services;
using HK_AREA_SEARCH.Models;
using HK_AREA_SEARCH.ViewModels;
using HK_AREA_SEARCH.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HK_AREA_SEARCH.Distance
{
    /// <summary>
    /// 距离计算服务实现
    /// </summary>
    public class DistanceService : IDistanceService
    {
        private readonly TempFileManager _tempFileManager;
        private const string PLOT_ID_FIELD = "PLOT_ID";
        
        // ⭐ 用于跟踪已使用的字段名，避免重复
        private readonly HashSet<string> _usedFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public DistanceService(TempFileManager tempFileManager)
        {
            _tempFileManager = tempFileManager ?? throw new ArgumentNullException(nameof(tempFileManager));
        }

        /// <summary>
        /// ⭐ 核心修改: 使用工作副本,避免污染原始文件
        /// 返回: (字段名映射, 工作副本路径)
        /// </summary>
        public async Task<(Dictionary<string, string> fieldNames, string workingAreaPath)> ExecuteAsync(
            List<POIDataItem> poiItems, 
            string analysisAreaPath)
        {
            var result = new Dictionary<string, string>();
            
            // ⭐ 清空已使用的字段名
            _usedFieldNames.Clear();

            // 🔥 关键修改: 创建工作副本,避免污染原文件
            string workingAreaPath = await CreateWorkingCopy(analysisAreaPath);
            
            System.Diagnostics.Debug.WriteLine($"========== Working on Copy ==========");
            System.Diagnostics.Debug.WriteLine($"Original: {Path.GetFileName(analysisAreaPath)}");
            System.Diagnostics.Debug.WriteLine($"Working:  {Path.GetFileName(workingAreaPath)}");
            System.Diagnostics.Debug.WriteLine($"=====================================");

            await EnsurePlotIdFieldAsync(workingAreaPath);

            // ⭐ 使用索引确保字段名唯一
            int index = 1;
            foreach (var item in poiItems)
            {
                if (string.IsNullOrWhiteSpace(item.DataPath))
                    continue;

                string fieldName = await ProcessPOIItemV2(item, workingAreaPath, null, index);
                result[item.DataName] = fieldName;
                index++;
            }

            // 🔥 返回字段名和工作副本路径
            return (result, workingAreaPath);
        }

        /// <summary>
        /// 🔥 新增: 创建分析区域的工作副本
        /// </summary>
        private async Task<string> CreateWorkingCopy(string originalPath)
        {
            try
            {
                // 生成唯一的工作副本文件名
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string workingFileName = $"Working_Analysis_Area_{timestamp}.shp";
                string workingPath = _tempFileManager.CreateTempFile(workingFileName);

                System.Diagnostics.Debug.WriteLine($"🔄 Creating working copy...");
                System.Diagnostics.Debug.WriteLine($"   From: {originalPath}");
                System.Diagnostics.Debug.WriteLine($"   To:   {workingPath}");

                // 使用 CopyFeatures 工具复制
                var copyParams = Geoprocessing.MakeValueArray(originalPath, workingPath);
                
                // ⭐ 添加 GPExecuteToolFlags.None 防止自动添加到地图
                var result = await Geoprocessing.ExecuteToolAsync(
                    "management.CopyFeatures", 
                    copyParams,
                    null,                      // environments
                    null,                      // CancelableProgressor
                    null,                      // GPToolExecuteEventHandler
                    GPExecuteToolFlags.None    // ⭐ 关键修改: 阻止添加到地图
                );

                if (result.IsFailed)
                {
                    throw new Exception($"创建工作副本失败: {string.Join("; ", result.ErrorMessages)}");
                }

                // 注册为临时文件 (会在清理时删除)
                _tempFileManager.RegisterTempFile(workingPath);

                System.Diagnostics.Debug.WriteLine($"✅ Working copy created successfully (not added to map)");
                
                return workingPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"创建工作副本失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 确保 PLOT_ID 字段存在并填充唯一值
        /// </summary>
        private async Task EnsurePlotIdFieldAsync(string featurePath)
        {
            // 尝试添加字段（如果已存在会报错，忽略即可）
            try
            {
                await AddField(featurePath, PLOT_ID_FIELD, "LONG", skipPrefixCheck: true);  // ⭐ 跳过前缀检查
                System.Diagnostics.Debug.WriteLine($"✅ 已添加字段: {PLOT_ID_FIELD}");
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"ℹ️ 字段 {PLOT_ID_FIELD} 可能已存在，跳过添加");
            }

            // ✅ 使用 autoIncrement() 填充 PLOT_ID
            string codeBlock = "rec = 0\ndef autoIncrement():\n    global rec\n    rec += 1\n    return rec";
            string expression = "autoIncrement()";

            var parameters = Geoprocessing.MakeValueArray(
                featurePath, PLOT_ID_FIELD, expression, "PYTHON3", codeBlock
            );

            var calcResult = await Geoprocessing.ExecuteToolAsync("management.CalculateField", parameters);

            if (calcResult.IsFailed)
            {
                // 回退到 Arcade
                System.Diagnostics.Debug.WriteLine("⚠️ Python 代码块方式失败，尝试 Arcade 表达式...");
                
                var arcadeParams = Geoprocessing.MakeValueArray(
                    featurePath, PLOT_ID_FIELD, "$feature.OBJECTID", "ARCADE"
                );
                
                calcResult = await Geoprocessing.ExecuteToolAsync("management.CalculateField", arcadeParams);
                
                if (calcResult.IsFailed)
                {
                    throw new Exception($"计算 PLOT_ID 失败: {string.Join("; ", calcResult.ErrorMessages)}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"✅ PLOT_ID 字段已填充");
        }

        /// <summary>
        /// 生成唯一字段名: S_ + 文件名(不含扩展名)
        /// </summary>
        /// <param name="poiName">POI 名称(通常是文件名)</param>
        /// <param name="index">POI 序号(用于保底)</param>
        /// <returns>格式为 S_文件名 的字段名,最长 10 字符(Shapefile DBF限制)</returns>
        private string GenerateUniqueFieldName(string poiName, int index)
        {
            // ⚠️ Shapefile DBF 字段名限制:最长 10 字符,以字母开头,只能包含字母、数字、下划线
            
            string baseName;
            
            // 1. 提取文件名(去除扩展名和路径)
            if (!string.IsNullOrWhiteSpace(poiName))
            {
                // 移除路径和扩展名
                string fileName = Path.GetFileNameWithoutExtension(poiName);
                
                // 修复: 移除所有非字母数字字符(包括空格、中文、特殊符号)
                baseName = Regex.Replace(fileName, @"[^a-zA-Z0-9]", "");
                
                // 如果提取后为空(纯中文文件名或只有特殊字符),使用序号
                if (string.IsNullOrEmpty(baseName))
                {
                    baseName = $"POI{index}";
                }
            }
            else
            {
                // 名称为空,使用序号
                baseName = $"POI{index}";
            }
            
            // 2. 确保不以数字开头
            if (baseName.Length > 0 && char.IsDigit(baseName[0]))
            {
                baseName = "P" + baseName;
            }
            
            // 3. 转大写
            baseName = baseName.ToUpper();
            
            // 4. ⚠️ 截断到 8 字符(给 S_ 前缀留空间,总长度不超过 10)
            if (baseName.Length > 8)
            {
                baseName = baseName.Substring(0, 8);
            }
            
            // 5. 加上 S_ 前缀
            string fieldName = $"S_{baseName}";
            
            // 6. 确保唯一性
            string uniqueFieldName = fieldName;
            int suffix = 1;
            while (_usedFieldNames.Contains(uniqueFieldName))
            {
                // 如果重复,加数字后缀
                string suffixStr = suffix.ToString();
                int maxBaseLen = 10 - 2 - suffixStr.Length; // 10 - "S_" - suffix
                string truncatedBase = baseName.Length > maxBaseLen 
                    ? baseName.Substring(0, maxBaseLen) 
                    : baseName;
                uniqueFieldName = $"S_{truncatedBase}{suffix}";
                suffix++;
                
                // ⚠️ 防止无限循环
                if (suffix > 999)
                {
                    throw new InvalidOperationException($"无法为 '{poiName}' 生成唯一字段名");
                }
            }
            
            _usedFieldNames.Add(uniqueFieldName);
            
            // 验证字段名合法性
            if (!IsValidFieldName(uniqueFieldName))
            {
                throw new InvalidOperationException($"生成的字段名 '{uniqueFieldName}' 不符合 Shapefile 规范 (原始名称: '{poiName}')");
            }
            
            System.Diagnostics.Debug.WriteLine($"字段名生成: '{poiName}' -> '{uniqueFieldName}' (长度: {uniqueFieldName.Length})");
            
            return uniqueFieldName;
        }

        /// <summary>
        /// 验证字段名是否符合 Shapefile 规范
        /// </summary>
        private bool IsValidFieldName(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                return false;
            
            // 1. 长度不超过 10
            if (fieldName.Length > 10)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 字段名 '{fieldName}' 长度超过 10");
                return false;
            }
            
            // 2. 必须以字母开头,只能包含字母、数字、下划线
            if (!Regex.IsMatch(fieldName, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
            {
                System.Diagnostics.Debug.WriteLine($"❌ 字段名 '{fieldName}' 包含非法字符");
                return false;
            }
            
            // 3. 不能包含空格
            if (fieldName.Contains(" "))
            {
                System.Diagnostics.Debug.WriteLine($"❌ 字段名 '{fieldName}' 包含空格");
                return false;
            }
            
            return true;
        }

        private async Task<string> ProcessPOIItemV2(POIDataItem poiItem, string suitableAreaPath, Envelope extentEnvelope, int index)
        {
            string distRaster;

            if (DetermineDataType(poiItem.DataPath) == DataType.Vector)
            {
                distRaster = await CalculateEuclideanDistance(poiItem.DataPath, poiItem, null);
            }
            else
            {
                distRaster = poiItem.DataPath;
            }

            // ✅ 使用 PLOT_ID 进行分区统计
            var zonalService = new ZonalStatisticsService(_tempFileManager);
            string statsTable = await zonalService.ExecuteZonalStatsAsync(
                suitableAreaPath, PLOT_ID_FIELD, distRaster, "MEAN"
            );

            System.Diagnostics.Debug.WriteLine($"✅ 分区统计完成: {statsTable}");

            // 使用新的字段名生成方法
            string scoreFieldName = GenerateUniqueFieldName(poiItem.DataName, index);
            
            await ConvertDistanceToScore(statsTable, scoreFieldName, poiItem);

            // ✅ 使用 PLOT_ID 进行字段连接
            await zonalService.JoinFieldAsync(
                suitableAreaPath, PLOT_ID_FIELD, statsTable, PLOT_ID_FIELD, scoreFieldName
            );

            System.Diagnostics.Debug.WriteLine($"✅ 字段连接完成: {scoreFieldName}");

            return scoreFieldName;
        }

        /// <summary>
        /// 距离/值 → 评分转换
        /// </summary>
        private async Task ConvertDistanceToScore(string statsTable, string scoreFieldName, POIDataItem poiItem)
        {
            // 1. 添加评分字段
            await AddField(statsTable, scoreFieldName, "DOUBLE");

            // 2. 构建计算表达式
            string expression;
            if (poiItem.CustomInterval && poiItem.CustomIntervalClasses != null)
            {
                expression = BuildCustomIntervalExpression(poiItem.CustomIntervalClasses, "MEAN");
            }
            else
            {
                expression = BuildEqualIntervalExpression("MEAN", poiItem);
            }

            System.Diagnostics.Debug.WriteLine($"评分表达式: {expression}");

            // 执行字段计算
            var parameters = Geoprocessing.MakeValueArray(
                statsTable, scoreFieldName, expression, "PYTHON3"
            );
            
            var result = await Geoprocessing.ExecuteToolAsync("management.CalculateField", parameters);
            
            if (result.IsFailed)
            {
                throw new Exception($"评分计算失败: {string.Join("; ", result.ErrorMessages)}");
            }
            
            System.Diagnostics.Debug.WriteLine($"✅ 评分计算完成");
        }

        /// <summary>
        /// 判断数据类型
        /// </summary>
        internal DataType DetermineDataType(string dataPath)
        {
            string ext = Path.GetExtension(dataPath)?.ToLower();

            if (ext == ".shp" || ext == ".kml" || ext == ".geojson")
                return DataType.Vector;

            if (ext == ".tif" || ext == ".tiff" || ext == ".img" || ext == ".jp2")
                return DataType.Raster;

            throw new NotSupportedException($"不支持的数据格式: {ext}");
        }

        /// <summary>
        /// 添加字段(支持跳过前缀检查)
        /// </summary>
        private async Task AddField(string tablePath, string fieldName, string fieldType, bool skipPrefixCheck = false)
        {
            // ⚠️ 验证字段名长度
            if (fieldName.Length > 10)
            {
                throw new ArgumentException($"字段名 '{fieldName}' 长度 {fieldName.Length} 超过 Shapefile 限制 (10字符)");
            }
            
            // ⚠️ 验证字段名前缀 (可选跳过)
            if (!skipPrefixCheck && 
                !fieldName.StartsWith("S_") && 
                fieldName != PLOT_ID_FIELD && 
                fieldName != "Rating" &&
                fieldName != "MEAN" &&  // ⭐ 允许临时字段
                fieldName != "COUNT")
            {
                throw new ArgumentException($"字段名 '{fieldName}' 前缀不正确,应以 'S_' 开头");
            }
            
            System.Diagnostics.Debug.WriteLine($"🔧 添加字段: {fieldName} ({fieldType}) 到 {Path.GetFileName(tablePath)}");
            
            var parameters = Geoprocessing.MakeValueArray(tablePath, fieldName, fieldType);
            var result = await Geoprocessing.ExecuteToolAsync("management.AddField", parameters);

            if (result.IsFailed)
            {
                string errorDetails = string.Join("; ", result.ErrorMessages);
                
                // ⚠️ 检查是否是字段已存在的错误
                if (errorDetails.Contains("already exists") || errorDetails.Contains("已存在"))
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 字段 {fieldName} 已存在,跳过添加");
                    return;
                }
                
                throw new Exception($"添加字段失败: {fieldName}\n表: {tablePath}\n错误: {errorDetails}");
            }
            
            System.Diagnostics.Debug.WriteLine($"✅ 字段 {fieldName} 添加成功");
        }

        /// <summary>
        /// 构建等间隔表达式
        /// </summary>
        private string BuildEqualIntervalExpression(string fieldName, POIDataItem poiItem)
        {
            int numClasses = 10;
            double maxDistance = Math.Abs(poiItem.Distance ?? 1000);  // 取绝对值
            double interval = maxDistance / numClasses;

            // ⭐ 判断是否为厌恶型设施 (Distance < 0)
            bool isUndesirable = poiItem.Distance.HasValue && poiItem.Distance.Value < 0;

            System.Diagnostics.Debug.WriteLine($"========== 评分表达式构建 ==========");
            System.Diagnostics.Debug.WriteLine($"设施类型: {(isUndesirable ? "厌恶型 (距离越远越好)" : "吸引型 (距离越近越好)")}");
            System.Diagnostics.Debug.WriteLine($"最大距离: {maxDistance} 米");
            System.Diagnostics.Debug.WriteLine($"区间大小: {interval:F2} 米");

            var conditions = new List<string>();
            
            // 构建 Distance 内的评分区间
            for (int i = 0; i < numClasses; i++)
            {
                double lower = i * interval;
                double upper = (i + 1) * interval;
                
                // 关键修改: 厌恶型设施反向评分
                int score;
                if (isUndesirable)
                {
                    // 距离越近,分数越低 (0-100米得1分, 900-1000米得10分)
                    score = i + 1;
                }
                else
                {
                    // 距离越近,分数越高 (0-100米得10分, 900-1000米得1分)
                    score = numClasses - i;
                }
                
                conditions.Add($"{lower} <= !{fieldName}! < {upper}: {score}");
                
                System.Diagnostics.Debug.WriteLine($"  区间 [{lower:F1}, {upper:F1}) -> 分数 {score}");
            }

            // 构建嵌套三元表达式 (从最后一个条件开始)
            string expression;
            
            if (isUndesirable)
            {
                //厌恶型设施: Distance 外得 10 分 (越远越好)
                expression = "10";  // 默认值改为 10
                System.Diagnostics.Debug.WriteLine($"  距离 >= {maxDistance} 米 -> 分数 10 (超出范围,最高分)");
            }
            else
            {
                //吸引型设施: Distance 外得 0 分 (越近越好)
                expression = "0";  // 默认值保持 0
                System.Diagnostics.Debug.WriteLine($"  距离 >= {maxDistance} 米 -> 分数 0 (超出范围,最低分)");
            }
            
            // 从后往前构建表达式
            for (int i = conditions.Count - 1; i >= 0; i--)
            {
                var parts = conditions[i].Split(':');
                expression = $"{parts[1].Trim()} if {parts[0].Trim()} else {expression}";
            }
            
            System.Diagnostics.Debug.WriteLine($"========================================");
            
            return expression;
        }

        /// <summary>
        /// 构建自定义间隔表达式
        /// </summary>
        private string BuildCustomIntervalExpression(List<IntervalClassItem> intervals, string fieldName)
        {
            var conditions = intervals
                .OrderBy(x => x.StartValue)
                .Select(i => $"{i.StartValue} <= !{fieldName}! < {i.EndValue}: {i.ClassValue}")
                .ToList();

            string expression = "0";
            for (int i = conditions.Count - 1; i >= 0; i--)
            {
                var parts = conditions[i].Split(':');
                expression = $"{parts[1].Trim()} if {parts[0].Trim()} else {expression}";
            }
            return expression;
        }

        /// <summary>
        /// 计算欧氏距离
        /// </summary>
        private async Task<string> CalculateEuclideanDistance(string inputVector, POIDataItem poiItem, Envelope extent)
        {
            var calculator = new EuclideanDistanceCalculator(_tempFileManager);
            
            //修复: 无论正负都传递绝对值
            if (poiItem.Distance.HasValue && poiItem.Distance.Value != 0)
            {
                calculator.SetMaxDistance(Math.Abs(poiItem.Distance.Value));
            }
            
            return await calculator.CalculateAsync(inputVector, extent);
        }
    }

    // 数据类型枚举
    internal enum DataType { Vector, Raster }
}