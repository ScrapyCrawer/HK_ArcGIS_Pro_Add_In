using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HK_AREA_SEARCH.Rating
{
    /// <summary>
    /// 评分计算服务接口
    /// </summary>
    public interface IRatingService
    {
        /// <summary>
        /// 执行评分计算流程
        /// </summary>
        /// <param name="rasterPaths">栅格路径字典</param>
        /// <param name="weights">权重字典</param>
        /// <param name="suitableAreaPath">可建设土地路径</param>
        /// <param name="outputPath">输出路径</param>
        /// <param name="minArea">最小面积阈值(平方米)</param>
        /// <returns>最终结果文件路径</returns>
        Task<string> ExecuteAsync(
            Dictionary<string, string> rasterPaths,
            Dictionary<string, double> weights,
            string suitableAreaPath,
            string outputPath,
            double? minArea = null);  // 新增参数
    }
}