using System;
using System.IO;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace HK_AREA_SEARCH.Infrastructure.Services
{
    /// <summary>
    /// 图层管理器 - 改进版
    /// </summary>
    public class LayerManager
    {
        /// <summary>
        /// ⭐ 改进: 添加图层到地图并缩放
        /// </summary>
        /// <param name="dataPath">数据路径</param>
        /// <param name="zoomToLayer">是否缩放到图层</param>
        /// <returns>图层对象</returns>
        public async Task<Layer> AddLayerToMap(string dataPath, bool zoomToLayer = true)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"========== 添加图层到地图 ==========");
                    System.Diagnostics.Debug.WriteLine($"文件路径: {dataPath}");

                    var map = MapView.Active?.Map;
                    if (map == null)
                        throw new Exception("没有活动的地图");

                    var mapView = MapView.Active;
                    if (mapView == null)
                        throw new Exception("没有活动的地图视图");

                    // ⭐ 验证文件存在
                    if (!File.Exists(dataPath))
                        throw new FileNotFoundException($"文件不存在: {dataPath}");

                    Uri dataUri = new Uri(dataPath);
                    
                    // ⭐ 创建图层
                    Layer layer = LayerFactory.Instance.CreateLayer(dataUri, map);

                    if (layer == null)
                        throw new Exception("图层创建失败");

                    System.Diagnostics.Debug.WriteLine($"✅ 图层已创建: {layer.Name}");

                    // ⭐ 等待图层完全加载
                    await WaitForLayerLoad(layer);

                    // ⭐ 缩放到图层范围
                    if (zoomToLayer && layer is FeatureLayer featureLayer)
                    {
                        await ZoomToFeatureLayer(featureLayer, mapView);
                    }

                    // ⭐ 强制刷新地图视图
                    mapView.Redraw(true);

                    System.Diagnostics.Debug.WriteLine($"========== 图层添加完成 ==========");
                    return layer;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 添加图层失败: {ex.Message}");
                    throw new Exception($"添加图层失败: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// ⭐ 新增: 等待图层加载完成
        /// </summary>
        private async Task WaitForLayerLoad(Layer layer, int maxWaitSeconds = 10)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("⏳ 等待图层加载...");
                
                int waited = 0;
                while (!layer.IsVisible && waited < maxWaitSeconds * 10)
                {
                    await Task.Delay(100); // 每100ms检查一次
                    waited++;
                }

                if (layer.IsVisible)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ 图层加载完成 (耗时: {waited * 100}ms)");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 图层加载超时 ({maxWaitSeconds}秒)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 等待图层加载异常: {ex.Message}");
            }
        }

        /// <summary>
        /// ⭐ 新增: 缩放到要素图层
        /// </summary>
        private async Task ZoomToFeatureLayer(FeatureLayer featureLayer, MapView mapView)
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("🔍 缩放到图层范围...");

                    // 获取图层范围
                    var extent = featureLayer.QueryExtent();
                    
                    if (extent != null && !extent.IsEmpty)
                    {
                        // ⭐ 扩展10%,留出边距
                        var expandedExtent = extent.Expand(1.1, 1.1, true);
                        
                        // ⭐ 缩放并设置动画时长
                        mapView.ZoomTo(expandedExtent, TimeSpan.FromSeconds(1.5));
                        
                        System.Diagnostics.Debug.WriteLine($"✅ 已缩放到图层");
                        System.Diagnostics.Debug.WriteLine($"   范围: {extent}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ 图层范围为空或无效");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 缩放失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// ⭐ 新增: 刷新地图视图
        /// </summary>
        public async Task RefreshMapView()
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    var mapView = MapView.Active;
                    if (mapView != null)
                    {
                        mapView.Redraw(true);
                        System.Diagnostics.Debug.WriteLine("✅ 地图视图已刷新");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 刷新地图视图失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 移除图层
        /// </summary>
        public async Task RemoveLayer(Layer layer)
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                        throw new Exception("没有活动的地图");

                    if (map.FindLayers(layer.Name).Count > 0)
                    {
                        map.RemoveLayer(layer);
                        System.Diagnostics.Debug.WriteLine($"✅ 图层已移除: {layer.Name}");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"移除图层失败: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// 获取活动地图
        /// </summary>
        public Map GetActiveMap()
        {
            return MapView.Active?.Map;
        }

        /// <summary>
        /// 刷新图层
        /// </summary>
        public async Task RefreshLayer(Layer layer)
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    if (layer is FeatureLayer featureLayer)
                    {
                        // 清除选择并刷新
                        featureLayer.ClearSelection();
                        System.Diagnostics.Debug.WriteLine($"✅ 图层已刷新: {layer.Name}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 刷新图层失败: {ex.Message}");
                }
            });
        }
    }
}