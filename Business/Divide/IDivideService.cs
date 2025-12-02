using System.Collections.Generic;
using System.Threading.Tasks;

namespace HK_AREA_SEARCH.Divide
{
    /// <summary>
    /// 可建设土地划分服务接口
    /// </summary>
    public interface IDivideService
    {
        /// <summary>
        /// 执行可建设土地划分
        /// </summary>
        /// <param name="analysisAreaPath">分析区域路径</param>
        /// <param name="constraintPaths">约束条件路径列表</param>
        /// <param name="outputPath">输出路径</param>
        /// <param name="minArea">最小面积阈值(平方米)，null表示无下限</param>
        /// <param name="maxArea">最大面积阈值(平方米)，null表示无上限</param>
        /// <returns>生成的可建设土地文件路径</returns>
        Task<string> ExecuteAsync(
            string analysisAreaPath, 
            List<string> constraintPaths, 
            string outputPath,
            double? minArea = null,
            double? maxArea = null);
    }
}