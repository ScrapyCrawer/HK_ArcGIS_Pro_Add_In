using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using HK_AREA_SEARCH.Infrastructure.Services;

namespace HK_AREA_SEARCH.Rating
{
    public class FactorScoreExtractor
    {
        private readonly TempFileManager _tempFileManager;

        public FactorScoreExtractor(TempFileManager tempFileManager)
        {
            _tempFileManager = tempFileManager ?? throw new ArgumentNullException(nameof(tempFileManager));
        }

        public async Task<string> ExtractToFeaturesAsync(string featurePath, Dictionary<string, string> rasterPaths)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("========== 提取单因子评分开始 ==========");
                    System.Diagnostics.Debug.WriteLine($"目标要素: {Path.GetFileName(featurePath)}");
                    System.Diagnostics.Debug.WriteLine($"单因子数量: {rasterPaths.Count}");

                    // ⭐ 添加唯一ID字段 (用于连接)
                    await AddUniqueIDField(featurePath);

                    // 为每个单因子栅格提取评分
                    foreach (var kvp in rasterPaths)
                    {
                        string factorName = kvp.Key;
                        string rasterPath = kvp.Value;

                        System.Diagnostics.Debug.WriteLine($"\n>>> 提取因子: {factorName}");
                        System.Diagnostics.Debug.WriteLine($"    栅格路径: {Path.GetFileName(rasterPath)}");

                        string fieldName = GenerateFieldName(factorName);
                        await ExtractZonalStatistics(featurePath, rasterPath, fieldName);
                    }

                    // ⭐ 删除临时 UniqueID 字段
                    await DeleteField(featurePath, "UniqueID");

                    System.Diagnostics.Debug.WriteLine("\n========== 提取单因子评分完成 ==========");
                    return featurePath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"\n❌ 提取单因子评分失败: {ex.Message}");
                    return featurePath;
                }
            });
        }

        /// <summary>
        /// ⭐ 添加唯一ID字段并计算序号
        /// </summary>
        private async Task AddUniqueIDField(string featurePath)
        {
            try
            {
                // 添加 UniqueID 字段
                var addFieldParams = Geoprocessing.MakeValueArray(
                    featurePath,
                    "UniqueID",
                    "LONG"
                );

                await Geoprocessing.ExecuteToolAsync(
                    "AddField_management",
                    addFieldParams,
                    null, null, null,
                    GPExecuteToolFlags.AddToHistory
                );

                // 计算 UniqueID = FID (从0开始的序号)
                var calcParams = Geoprocessing.MakeValueArray(
                    featurePath,
                    "UniqueID",
                    "!FID!",
                    "PYTHON3"
                );

                await Geoprocessing.ExecuteToolAsync(
                    "CalculateField_management",
                    calcParams,
                    null, null, null,
                    GPExecuteToolFlags.AddToHistory
                );

                System.Diagnostics.Debug.WriteLine($"    ✅ UniqueID 字段已添加并计算");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"    ⚠️ 添加 UniqueID 失败: {ex.Message}");
            }
        }

        private async Task ExtractZonalStatistics(string featurePath, string rasterPath, string fieldName)
        {
            try
            {
                // 步骤1: 先添加目标字段
                await AddField(featurePath, fieldName);

                // 步骤2: 创建临时统计表
                string statsTable = _tempFileManager.CreateTempFile($"stats_{fieldName}.dbf");
                _tempFileManager.RegisterTempFile(statsTable);

                // 步骤3: 执行 Zonal Statistics as Table (⭐ 使用 UniqueID)
                var statsParams = Geoprocessing.MakeValueArray(
                    featurePath,
                    "UniqueID",     // ⭐ 改用 UniqueID
                    rasterPath,
                    statsTable,
                    "DATA",
                    "MEAN"
                );

                System.Diagnostics.Debug.WriteLine($"    执行 Zonal Statistics (使用 UniqueID)...");
                var statsResult = await Geoprocessing.ExecuteToolAsync(
                    "ZonalStatisticsAsTable_sa",
                    statsParams,
                    null, null, null,
                    GPExecuteToolFlags.AddToHistory
                );

                if (statsResult.IsFailed)
                {
                    var errors = string.Join("; ", statsResult.ErrorMessages?.Select(e => e.Text) ?? new string[0]);
                    System.Diagnostics.Debug.WriteLine($"    ⚠️ Zonal Statistics 失败: {errors}");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"    ✅ 统计表已生成");

                // 步骤4: 使用 Join Field 连接 (⭐ 使用 UniqueID 连接)
                var joinParams = Geoprocessing.MakeValueArray(
                    featurePath,
                    "UniqueID",     // ⭐ 改用 UniqueID
                    statsTable,
                    "UniqueID",     // ⭐ 统计表的 UniqueID 字段
                    new string[] { "MEAN" }  // ⭐ 明确指定要传递的字段
                );

                System.Diagnostics.Debug.WriteLine($"    执行 Join Field (UniqueID -> UniqueID)...");
                var joinResult = await Geoprocessing.ExecuteToolAsync(
                    "JoinField_management",
                    joinParams,
                    null, null, null,
                    GPExecuteToolFlags.AddToHistory
                );

                if (joinResult.IsFailed)
                {
                    var errors = string.Join("; ", joinResult.ErrorMessages?.Select(e => e.Text) ?? new string[0]);
                    System.Diagnostics.Debug.WriteLine($"    ⚠️ Join Field 失败: {errors}");
                    
                    // ⭐ 打印统计表的字段信息用于调试
                    await PrintTableSchema(statsTable);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"    ✅ 字段已连接");

                // 步骤5: 使用 Calculate Field 将 MEAN 复制到目标字段
                await CopyMeanToField(featurePath, fieldName);

                // 步骤6: 删除临时 MEAN 字段
                await DeleteField(featurePath, "MEAN");

                System.Diagnostics.Debug.WriteLine($"    ✅ 因子 {fieldName} 提取完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"    ❌ 提取 {fieldName} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// ⭐ 打印表结构用于调试
        /// </summary>
        private async Task PrintTableSchema(string tablePath)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    using (var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(Path.GetDirectoryName(tablePath)))))
                    using (var table = gdb.OpenDataset<Table>(Path.GetFileNameWithoutExtension(tablePath)))
                    {
                        var definition = table.GetDefinition();
                        System.Diagnostics.Debug.WriteLine($"    >>> 统计表字段:");
                        foreach (var field in definition.GetFields())
                        {
                            System.Diagnostics.Debug.WriteLine($"        - {field.Name} ({field.FieldType})");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"    ⚠️ 无法读取表结构: {ex.Message}");
            }
        }

        private async Task AddField(string featurePath, string fieldName)
        {
            try
            {
                var addFieldParams = Geoprocessing.MakeValueArray(
                    featurePath,
                    fieldName,
                    "DOUBLE",
                    "",
                    "",
                    "",
                    fieldName,
                    "NULLABLE"
                );

                await Geoprocessing.ExecuteToolAsync(
                    "AddField_management",
                    addFieldParams,
                    null, null, null,
                    GPExecuteToolFlags.AddToHistory
                );

                System.Diagnostics.Debug.WriteLine($"    ✅ 字段 {fieldName} 已添加");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"    ⚠️ 添加字段失败 (可能已存在): {ex.Message}");
            }
        }

        private async Task CopyMeanToField(string featurePath, string targetField)
        {
            try
            {
                // ⭐ 使用 round() 函数保留2位小数
                var calcParams = Geoprocessing.MakeValueArray(
                    featurePath,
                    targetField,
                    "round(!MEAN!, 2)",  // ⭐ 保留2位小数
                    "PYTHON3"
                );

                await Geoprocessing.ExecuteToolAsync(
                    "CalculateField_management",
                    calcParams,
                    null, null, null,
                    GPExecuteToolFlags.AddToHistory
                );

                System.Diagnostics.Debug.WriteLine($"    ✅ MEAN 已复制到 {targetField} (保留2位小数)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"    ⚠️ 复制 MEAN 失败: {ex.Message}");
            }
        }

        private async Task DeleteField(string featurePath, string fieldName)
        {
            try
            {
                var deleteParams = Geoprocessing.MakeValueArray(
                    featurePath,
                    fieldName
                );

                await Geoprocessing.ExecuteToolAsync(
                    "DeleteField_management",
                    deleteParams,
                    null, null, null,
                    GPExecuteToolFlags.AddToHistory
                );

                System.Diagnostics.Debug.WriteLine($"    ✅ 字段 {fieldName} 已删除");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"    ⚠️ 删除字段失败: {ex.Message}");
            }
        }

        private string GenerateFieldName(string factorName)
        {
            string sanitized = new string(factorName
                .Where(c => char.IsLetterOrDigit(c) || c == '_')
                .ToArray());

            if (string.IsNullOrEmpty(sanitized))
                sanitized = "Factor";

            if (char.IsDigit(sanitized[0]))
                sanitized = "F" + sanitized;

            if (sanitized.Length > 8)
                sanitized = sanitized.Substring(0, 8);

            string fieldName = "S_" + sanitized;

            System.Diagnostics.Debug.WriteLine($"    字段名映射: {factorName} -> {fieldName}");
            return fieldName;
        }
    }
}