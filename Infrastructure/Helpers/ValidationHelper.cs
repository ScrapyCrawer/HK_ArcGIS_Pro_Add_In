using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HK_AREA_SEARCH.Models;
using HK_AREA_SEARCH.Common;

namespace HK_AREA_SEARCH.Infrastructure.Helpers
{
    /// <summary>
    /// 验证辅助
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// 验证权重和是否等于1.0
        /// </summary>
        /// <param name="weights">权重列表</param>
        /// <returns>是否有效</returns>
        public static bool ValidateWeightSum(List<double> weights)
        {
            if (weights == null || weights.Count == 0)
                return true; // 空权重列表视为有效

            double sum = weights.Sum();
            return Math.Abs(sum - 1.0) < 0.001; // 允许小的浮点误差
        }

        /// <summary>
        /// 验证权重和是否等于1.0
        /// </summary>
        /// <param name="weightDictionary">权重字典</param>
        /// <returns>是否有效</returns>
        public static bool ValidateWeightSum(Dictionary<string, double> weightDictionary)
        {
            if (weightDictionary == null || weightDictionary.Count == 0)
                return true; // 空权重字典视为有效

            double sum = weightDictionary.Values.Sum();
            return Math.Abs(sum - 1.0) < 0.001; // 允许小的浮点误差
        }

        /// <summary>
        /// 验证数据类型
        /// </summary>
        /// <param name="dataPath">数据路径</param>
        /// <returns>数据类型</returns>
        public static DataType ValidateDataType(string dataPath)
        {
            if (string.IsNullOrWhiteSpace(dataPath))
                return DataType.Unknown;

            string extension = Path.GetExtension(dataPath)?.ToLower();

            // 常见的栅格文件扩展名
            if (extension == ".tif" || extension == ".tiff" || extension == ".img" || 
                extension == ".bil" || extension == ".jpg" || extension == ".jp2" || 
                extension == ".png" || extension == ".gif")
            {
                return DataType.Raster;
            }

            // 常见的矢量文件扩展名
            if (extension == ".shp" || extension == ".gdb" || extension == ".dbf" || 
                extension == ".lyr" || extension == ".kml" || extension == ".geojson")
            {
                return DataType.Vector;
            }

            return DataType.Unknown;
        }

        /// <summary>
        /// 验证几何有效性
        /// </summary>
        /// <param name="dataPath">数据路径</param>
        /// <returns>是否有效</returns>
        public static bool ValidateGeometry(string dataPath)
        {
            // 基本文件存在性检查
            if (!File.Exists(dataPath))
                return false;

            // 文件扩展名校验
            string extension = Path.GetExtension(dataPath)?.ToLower();
            if (string.IsNullOrEmpty(extension))
                return false;

            // 检查是否为支持的GIS格式
            var supportedExtensions = new[] { ".shp", ".gdb", ".tif", ".tiff", ".img", ".dbf", ".lyr", ".kml", ".geojson" };
            return supportedExtensions.Contains(extension);
        }

        /// <summary>
        /// 验证输入参数
        /// </summary>
        /// <param name="analysisAreaPath">分析区域路径</param>
        /// <param name="constraintItems">约束条件项列表</param>
        /// <param name="poiItems">POI项列表</param>
        /// <param name="outputPath">输出路径</param>
        /// <returns>验证结果</returns>
        public static ValidationResult ValidateInputs(
            string analysisAreaPath,
            List<ConstraintDataItem> constraintItems,
            List<POIDataItem> poiItems,
            string outputPath)
        {
            var result = new ValidationResult { IsValid = true };

            // 验证分析区域路径
            if (string.IsNullOrWhiteSpace(analysisAreaPath))
            {
                result.IsValid = false;
                result.ErrorMessage = "分析区域路径不能为空";
                return result;
            }

            if (!File.Exists(analysisAreaPath))
            {
                result.IsValid = false;
                result.ErrorMessage = $"分析区域文件不存在: {analysisAreaPath}";
                return result;
            }

            // 验证约束条件
            if (constraintItems == null || constraintItems.Count == 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "至少需要一个约束条件";
                return result;
            }

            // 检查所有约束条件文件是否存在
            foreach (var item in constraintItems)
            {
                if (!string.IsNullOrWhiteSpace(item.DataPath) && !File.Exists(item.DataPath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"约束条件文件不存在: {item.DataPath}";
                    return result;
                }
            }

            // 验证POI项目
            if (poiItems == null || poiItems.Count == 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "至少需要一个POI项目";
                return result;
            }

            // 检查所有POI文件是否存在
            foreach (var item in poiItems.Where(p => !p.IsEmpty))
            {
                if (!File.Exists(item.DataPath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"POI文件不存在: {item.DataPath}";
                    return result;
                }

                // 验证权重（如果提供了权重）
                if (item.Weight.HasValue && (item.Weight.Value < 0 || item.Weight.Value > 1))
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"POI权重必须在0到1之间: {item.DataName}";
                    return result;
                }

                // 对于矢量数据（非栅格数据），必须提供距离
                if (!item.IsRasterData && !item.Distance.HasValue)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"POI {item.DataName} 需要设置距离参数";
                    return result;
                }

                // 验证权重不能为空和必须由用户设置
                if (!item.Weight.HasValue || !item.WeightHasBeenSetByUser)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"POI {item.DataName} 需要设置权重参数";
                    return result;
                }
            }

            // 验证输出路径
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                result.IsValid = false;
                result.ErrorMessage = "输出路径不能为空";
                return result;
            }

            // 验证权重和
            var weights = poiItems.Where(p => !p.IsEmpty && p.Weight.HasValue).Select(p => p.Weight.Value).ToList();
            if (weights.Count > 0 && !ValidateWeightSum(weights))
            {
                result.IsValid = false;
                result.ErrorMessage = "POI权重总和必须等于1.0";
                return result;
            }

            // 验证输出文件夹是否存在
            string outputDir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(outputDir))
            {
                result.IsValid = false;
                result.ErrorMessage = $"输出文件夹不存在: {outputDir}";
                return result;
            }

            return result;
        }
    }

    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }
}