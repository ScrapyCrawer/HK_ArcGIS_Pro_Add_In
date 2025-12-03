using ArcGIS.Core.Geometry;
using System.Collections.Generic;

namespace HK_AREA_SEARCH.Models
{
    /// <summary>
    /// 地块信息模型
    /// </summary>
    public class PlotInfo
    {
        /// <summary>
        /// 要素的 ObjectID
        /// </summary>
        public long ObjectID { get; set; }

        /// <summary>
        /// 综合评分（gridcode 字段）
        /// </summary>
        public double GridCode { get; set; }

        /// <summary>
        /// 单因子得分字典 (字段名 -> 得分值)
        /// 例如: { "S_Traffic": 8.5, "S_Facility": 7.2 }
        /// </summary>
        public Dictionary<string, double> FactorScores { get; set; }

        /// <summary>
        /// 地块的空间范围
        /// </summary>
        public Envelope Extent { get; set; }

        /// <summary>
        /// 地块面积（可选）
        /// </summary>
        public double? Area { get; set; }

        public PlotInfo()
        {
            FactorScores = new Dictionary<string, double>();
        }
    }
}