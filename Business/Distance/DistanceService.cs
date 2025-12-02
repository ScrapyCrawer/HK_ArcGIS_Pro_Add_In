using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using HK_AREA_SEARCH.Models;
using HK_AREA_SEARCH.Infrastructure.Services;
using HK_AREA_SEARCH.Common;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using HK_AREA_SEARCH.Views;
using HK_AREA_SEARCH.ViewModels;
using ArcGIS.Desktop.Framework.Dialogs;


namespace HK_AREA_SEARCH.Distance
{
    /// <summary>
    /// 距离计算服务实现
    /// </summary>
    public class DistanceService : IDistanceService
    {
        private readonly TempFileManager _tempFileManager;

        public DistanceService(TempFileManager tempFileManager)
        {
            _tempFileManager = tempFileManager ?? throw new ArgumentNullException(nameof(tempFileManager));
        }

        /// <summary>
        /// 执行距离计算流程
        /// </summary>
        /// <param name="poiItems">POI数据项列表</param>
        /// <param name="analysisAreaPath">分析区域路径，用于提取处理范围</param>
        /// <returns>处理后的栅格路径字典</returns>
        public async Task<Dictionary<string, string>> ExecuteAsync(List<POIDataItem> poiItems, string analysisAreaPath = null)
        {
            var result = new Dictionary<string, string>();

            //获取分析区域的边界范围
            //描述：输入analysisAreaPath，输出边界Envelope类型
            Envelope extentEnvelope = await GetFeatureExtent(analysisAreaPath);

            try
            {
                // 处理每个POI数据项
                foreach (var item in poiItems)
                {
                    // 跳过空路径
                    if (string.IsNullOrWhiteSpace(item.DataPath))
                        continue;

                    // 依次处理每个POI项，返回处理后的栅格路径
                    string processedRasterPath = await ProcessPOIItem(item, extentEnvelope);
                    result[item.DataName] = processedRasterPath;
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"POI距离计算失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 处理单个POI项
        /// </summary>
        private async Task<string> ProcessPOIItem(POIDataItem poiItem, Envelope extentEnvelope)
        {
            try
            {
                string inputRasterPath;

                // 判断数据类型并执行相应操作
                DataType dataType = DetermineDataType(poiItem.DataPath);

                if (dataType == DataType.Vector)
                {
                    // 如果是矢量数据，计算欧氏距离
                    inputRasterPath = await CalculateEuclideanDistance(poiItem.DataPath, poiItem, extentEnvelope);
                }
                else if (dataType == DataType.Raster)
                {
                    // 如果是栅格数据，直接使用原路径（不进行欧氏距离计算）
                    // 用户可能已经提供了距离栅格或其他预计算的栅格
                    inputRasterPath = poiItem.DataPath;
                }
                else
                {
                    throw new Exception($"无法识别数据类型: {poiItem.DataPath}");
                }

                // 执行重分类
                string reclassPath = await ReclassifyRaster(inputRasterPath, poiItem);
                
                // 处理NoData值（根据距离的正负决定处理方式）
                string finalPath = await ProcessNoDataValues(reclassPath, poiItem.Distance ?? 0);   // distance不为null时使用其值，否则使用0

                return finalPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"处理POI项 '{poiItem?.DataName}' 失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 判断数据类型
        /// </summary>
        private DataType DetermineDataType(string dataPath)
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
        /// 计算欧氏距离
        /// </summary>
        private async Task<string> CalculateEuclideanDistance(string vectorPath, POIDataItem poiItem = null, Envelope extentEnvelope = null)
        {
            try
            {
                var calculator = new EuclideanDistanceCalculator(_tempFileManager);
                // 设置合适的最大距离值（米），取POI项中的距离绝对值，如果未指定则使用默认值
                double maxDistance = poiItem?.Distance != null ? Math.Abs(poiItem.Distance.Value) : 1000;
                calculator.SetMaxDistance(maxDistance);

                return await calculator.CalculateAsync(vectorPath, extentEnvelope);
            }
            catch (Exception ex)
            {
                throw new Exception($"计算欧氏距离失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 对栅格进行重分类
        /// </summary>
        private async Task<string> ReclassifyRaster(string inputRasterPath, POIDataItem poiItem = null)
        {
            var reclassifier = new RasterReclassifier(_tempFileManager);

            try
            {
                // 使用自定义间隔
                if (poiItem?.CustomInterval == true)
                {
                    // ⭐ 检查是否已有保存的自定义间隔配置
                    if (poiItem.CustomIntervalClasses != null && poiItem.CustomIntervalClasses.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"========== Using Saved Custom Intervals ==========");
                        System.Diagnostics.Debug.WriteLine($"POI: {poiItem.DataName}");
                        System.Diagnostics.Debug.WriteLine($"Intervals count: {poiItem.CustomIntervalClasses.Count}");
                        
                        // ⭐ 使用已保存的配置,不再弹出对话框
                        return await reclassifier.CreateCustomClasses(
                            inputRasterPath,
                            poiItem.CustomIntervalClasses
                        );
                    }
                    else
                    {
                        // ⭐ 如果没有保存的配置,弹出对话框(理论上不应该发生)
                        System.Diagnostics.Debug.WriteLine($"⚠️ No saved custom intervals, showing dialog...");
                        
                        var customIntervalClass = await ShowCustomIntervalDialogAsync(inputRasterPath, poiItem);

                        if (customIntervalClass != null && customIntervalClass.Count > 0)
                        {
                            return await reclassifier.CreateCustomClasses(
                                inputRasterPath,
                                customIntervalClass
                            );
                        }
                        else
                        {
                            // 用户取消对话框,使用默认等间隔分类
                            return await reclassifier.CreateEqualIntervalClasses(
                                inputRasterPath,
                                Constants.NUM_CLASSES,
                                poiItem
                            );
                        }
                    }
                }
                else
                {
                    // 使用等间隔分类
                    return await reclassifier.CreateEqualIntervalClasses(
                        inputRasterPath,
                        Constants.NUM_CLASSES,
                        poiItem
                    );
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"创建分类失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 显示自定义间隔对话框（在UI线程中）
        /// </summary>
        private async Task<List<IntervalClassItem>> ShowCustomIntervalDialogAsync(string inputRasterPath, POIDataItem poiItem)
        {
            return await ArcGIS.Desktop.Framework.FrameworkApplication.Current.Dispatcher.InvokeAsync(() =>
            {
                var customIntervalDialog = new Views.CustomIntervalDialog(inputRasterPath, poiItem);
                bool? result = customIntervalDialog.ShowDialog();

                if (result == true)
                {
                    var customIntervalViewModel = (ViewModels.CustomIntervalDialogViewModel)customIntervalDialog.DataContext;
                    return new List<IntervalClassItem>(customIntervalViewModel.ClassItems);
                }
                else
                {
                    // 用户取消对话框
                    return null;
                }
            }).Task;
        }

        /// <summary>
        /// 处理NoData值
        /// </summary>
        private async Task<string> ProcessNoDataValues(string inputRasterPath, int distance)
        {
            var noDataHandler = new NoDataHandler(_tempFileManager);

            // 如果距离为正，NoData值替换为0；如果距离为负，NoData值替换为10
            double noDataValue = distance >= 0 ? 0.0 : Constants.NUM_CLASSES;

            return await noDataHandler.ReplaceNoDataAsync(inputRasterPath, noDataValue);
        }

        /// <summary>
        /// 获取要素类的边界范围
        /// </summary>
        /// <param name="analysisAreaPath">分析区域shapefile路径</param>
        /// <returns>边界范围</returns>
        public async Task<Envelope> GetFeatureExtent(string analysisAreaPath)
        {
            if (string.IsNullOrWhiteSpace(analysisAreaPath))
            {
                return null;
            }

            // 验证文件路径是否存在
            if (!File.Exists(analysisAreaPath))
            {
                throw new FileNotFoundException($"分析区域文件不存在: {analysisAreaPath}");
            }

            // 文件系统链接--链接到路径下
            var filesystemconnetion = new FileSystemConnectionPath(new Uri(Path.GetDirectoryName(analysisAreaPath)), FileSystemDatastoreType.Shapefile);
            // 数据存储器
            FileSystemDatastore filesystemdatastore = null;
            // 数据集
            FeatureClass featureclass = null;

            return await QueuedTask.Run(() =>
            {
                try
                {
                    filesystemdatastore = new FileSystemDatastore(filesystemconnetion);
                    featureclass = filesystemdatastore.OpenDataset<FeatureClass>(Path.GetFileNameWithoutExtension(analysisAreaPath));   // 输入名称无后缀

                    //获取边界
                    Envelope envelope = featureclass.GetExtent();

                    return envelope;
                }
                catch (Exception ex)
                {
                    throw new Exception($"获取要素类边界失败: {ex.Message}", ex);
                }
                finally
                {
                    // 释放
                    featureclass?.Dispose();
                    filesystemdatastore?.Dispose();
                }

            });
        }
    }
}