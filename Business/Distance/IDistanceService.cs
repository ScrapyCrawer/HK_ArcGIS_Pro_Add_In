using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HK_AREA_SEARCH.Models;

namespace HK_AREA_SEARCH.Distance
{
    /// <summary>
    /// 距离计算服务接口
    /// </summary>
    public interface IDistanceService
    {
        /// <summary>
        /// 执行距离计算流程
        /// </summary>
        /// <param name="poiItems">POI数据项列表</param>
        /// <param name="analysisAreaPath">分析区域路径，用于提取处理范围</param>
        /// <returns>(字段名映射, 工作副本路径)</returns>
        Task<(Dictionary<string, string> fieldNames, string workingAreaPath)> ExecuteAsync(
            List<POIDataItem> poiItems, 
            string analysisAreaPath = null);
    }
}