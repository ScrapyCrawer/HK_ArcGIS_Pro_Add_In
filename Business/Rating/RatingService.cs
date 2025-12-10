using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using HK_AREA_SEARCH.Infrastructure.Services;
using HK_AREA_SEARCH.Common;

namespace HK_AREA_SEARCH.Rating
{
    /// <summary>
    /// 评分计算服务实现 （基于字段评分进行计算）
    /// </summary>
    public class RatingService : IRatingService
    {
        private readonly TempFileManager _tempFileManager;

        public RatingService(TempFileManager tempFileManager)
        {
            _tempFileManager = tempFileManager ?? throw new ArgumentNullException(nameof(tempFileManager));
        }

        /// <summary>
        /// 执行评分计算流程
        /// </summary>
        /// <param name="fieldNames">评分字段名字典 (DistanceService 返回的结果)</param>
        /// <param name="weights">权重字典</param>
        /// <param name="suitableAreaPath">可建设土地路径 (已包含所有评分字段)</param>
        /// <param name="outputPath">输出路径</param>
        /// <param name="minArea">最小面积阈值(平方米)</param>
        /// <returns>最终结果文件路径</returns>
        public async Task<string> ExecuteAsync(
            Dictionary<string, string> fieldNames,  // ✅ 改为字段名
            Dictionary<string, double> weights,
            string suitableAreaPath,
            string outputPath,
            double? minArea = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("========== 评分计算开始 ==========");
                System.Diagnostics.Debug.WriteLine($"输入: {suitableAreaPath}");
                System.Diagnostics.Debug.WriteLine($"输出: {outputPath}");
                System.Diagnostics.Debug.WriteLine($"评分字段数量: {fieldNames.Count}");

                // 1. 添加综合评分字段
                await AddRatingField(suitableAreaPath, Constants.RATING_FIELD);
                System.Diagnostics.Debug.WriteLine($"✅ 添加综合评分字段: {Constants.RATING_FIELD}");

                // 2. 计算综合评分 (加权求和)
                await CalculateWeightedRating(suitableAreaPath, fieldNames, weights);
                System.Diagnostics.Debug.WriteLine($"✅ 综合评分计算完成");

                // 3. 面积筛选 (如果需要)
                string filteredPath = suitableAreaPath;
                if (minArea.HasValue && minArea.Value > 0)
                {
                    filteredPath = await FilterByArea(suitableAreaPath, minArea.Value, outputPath);
                    System.Diagnostics.Debug.WriteLine($"✅ 面积筛选完成: >= {minArea.Value} m²");
                }
                else
                {
                    // 不需要筛选,直接复制到输出路径
                    filteredPath = await CopyFeatures(suitableAreaPath, outputPath);
                    System.Diagnostics.Debug.WriteLine($"✅ 要素已复制到输出路径");
                }

                System.Diagnostics.Debug.WriteLine("========== 评分计算完成 ==========");
                
                return filteredPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"评分计算失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 添加评分字段
        /// </summary>
        private async Task AddRatingField(string featurePath, string fieldName)
        {
            var parameters = Geoprocessing.MakeValueArray(
                featurePath,
                fieldName,
                "DOUBLE",
                null,  // field_precision
                null,  // field_scale
                null,  // field_length
                $"{fieldName}",  // field_alias
                "NULLABLE"
            );

            var result = await Geoprocessing.ExecuteToolAsync("management.AddField", parameters);

            if (result.IsFailed)
            {
                throw new Exception($"添加字段失败: {string.Join("; ", result.ErrorMessages)}");
            }
        }

        /// <summary>
        /// 计算加权评分
        /// </summary>
        private async Task CalculateWeightedRating(
            string featurePath, 
            Dictionary<string, string> fieldNames, 
            Dictionary<string, double> weights)
        {
            // 构建 Python 表达式: (!Score_POI1! * 0.3) + (!Score_POI2! * 0.5) + ...
            var expressionParts = new List<string>();
            
            foreach (var kvp in fieldNames)
            {
                string poiName = kvp.Key;
                string fieldName = kvp.Value;
                
                if (weights.ContainsKey(poiName))
                {
                    double weight = weights[poiName];
                    expressionParts.Add($"(!{fieldName}! * {weight})");
                }
            }

            string expression = string.Join(" + ", expressionParts);
            
            System.Diagnostics.Debug.WriteLine($"计算表达式: {expression}");

            // 执行字段计算器
            var parameters = Geoprocessing.MakeValueArray(
                featurePath,
                Constants.RATING_FIELD,
                expression,
                "PYTHON3"
            );

            var result = await Geoprocessing.ExecuteToolAsync("management.CalculateField", parameters);

            if (result.IsFailed)
            {
                throw new Exception($"字段计算失败: {string.Join("; ", result.ErrorMessages)}");
            }
        }

        /// <summary>
        /// 按面积筛选
        /// </summary>
        private async Task<string> FilterByArea(string inputPath, double minArea, string outputPath)
        {
            // 构建 SQL 查询条件
            string whereClause = $"Shape_Area >= {minArea}";

            var parameters = Geoprocessing.MakeValueArray(
                inputPath,
                outputPath,
                whereClause
            );

            // ⭐ 改为 GPExecuteToolFlags.None 防止自动添加到地图
            var result = await Geoprocessing.ExecuteToolAsync(
                "analysis.Select",
                parameters,
                null,
                null,
                null,
                GPExecuteToolFlags.None  
            );

            if (result.IsFailed)
            {
                throw new Exception($"面积筛选失败: {string.Join("; ", result.ErrorMessages)}");
            }

            return outputPath;
        }

        /// <summary>
        /// 复制要素到输出路径
        /// </summary>
        private async Task<string> CopyFeatures(string inputPath, string outputPath)
        {
            var parameters = Geoprocessing.MakeValueArray(
                inputPath,
                outputPath
            );

            // ⭐ 改为 GPExecuteToolFlags.None 防止自动添加到地图
            var result = await Geoprocessing.ExecuteToolAsync(
                "management.CopyFeatures",
                parameters,
                null,
                null,
                null,
                GPExecuteToolFlags.None  
            );

            if (result.IsFailed)
            {
                throw new Exception($"复制要素失败: {string.Join("; ", result.ErrorMessages)}");
            }

            return outputPath;
        }
    }
}