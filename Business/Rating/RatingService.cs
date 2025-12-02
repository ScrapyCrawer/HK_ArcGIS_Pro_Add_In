using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HK_AREA_SEARCH.Infrastructure.Services;
using HK_AREA_SEARCH.Common;

namespace HK_AREA_SEARCH.Rating
{
    /// <summary>
    /// 评分计算服务实现
    /// </summary>
    public class RatingService : IRatingService
    {
        private readonly TempFileManager _tempFileManager;
        private double? _minArea;  // ⭐ 新增: 存储最小面积

        public RatingService(TempFileManager tempFileManager)
        {
            _tempFileManager = tempFileManager ?? throw new ArgumentNullException(nameof(tempFileManager));
        }

        /// <summary>
        /// 执行评分计算流程
        /// </summary>
        /// <param name="rasterPaths">栅格路径字典</param>
        /// <param name="weights">权重字典</param>
        /// <param name="suitableAreaPath">可建设土地路径</param>
        /// <param name="outputPath">输出路径</param>
        /// <param name="minArea">最小面积阈值(平方米)</param>
        /// <returns>最终结果文件路径</returns>
        public async Task<string> ExecuteAsync(
            Dictionary<string, string> rasterPaths,
            Dictionary<string, double> weights,
            string suitableAreaPath,
            string outputPath,
            double? minArea = null)  // ⭐ 新增参数
        {
            try
            {
                _minArea = minArea;  // ⭐ 保存参数
                
                System.Diagnostics.Debug.WriteLine("========== 评分计算开始 ==========");
                
                // 1. 计算加权求和得到评分栅格
                string ratingRasterPath = await CalculateWeightedSum(rasterPaths, weights);
                System.Diagnostics.Debug.WriteLine($"✅ 评分栅格已生成: {Path.GetFileName(ratingRasterPath)}");
                System.Diagnostics.Debug.WriteLine($"⚠️ 注意: 评分栅格为中间文件,不会显示在地图中");

                // 2. 将评分栅格转换为矢量
                string ratingVectorPath = await ConvertToVector(ratingRasterPath);
                System.Diagnostics.Debug.WriteLine($"✅ 栅格已转换为矢量: {Path.GetFileName(ratingVectorPath)}");
              
                // 3. 与可建设土地进行相交分析
                string resultPath = await IntersectWithSuitableArea(ratingVectorPath, suitableAreaPath, outputPath);
                System.Diagnostics.Debug.WriteLine($"✅ 相交分析完成: {Path.GetFileName(resultPath)}");
                
                System.Diagnostics.Debug.WriteLine("========== 评分计算完成 ==========");
                
                return resultPath;

            }
            catch (Exception ex)
            {
                throw new Exception($"评分计算失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 计算加权求和
        /// </summary>
        private async Task<string> CalculateWeightedSum(Dictionary<string, string> rasterPaths, Dictionary<string, double> weights)
        {
            var calculator = new RasterCalculator(_tempFileManager);
            return await calculator.WeightedSumAsync(rasterPaths, weights);
        }

        /// <summary>
        /// 转换为矢量
        /// </summary>
        private async Task<string> ConvertToVector(string inputRasterPath)
        {
            var converter = new RasterToVectorConverter(_tempFileManager);
            return await converter.ConvertAsync(inputRasterPath);
        }

        /// <summary>
        /// 与可建设土地相交
        /// </summary>
        private async Task<string> IntersectWithSuitableArea(string ratingVectorPath, string suitableAreaPath, string outputPath)
        {
            var analyzer = new IntersectionAnalyzer(_tempFileManager);
            return await analyzer.IntersectAsync(ratingVectorPath, suitableAreaPath, outputPath, _minArea);  // ⭐ 传递参数
        }
    }
}