using System.Collections.Generic;
using ArcGIS.Core.Geometry;

namespace HK_AREA_SEARCH.Models
{
    /// <summary>
    /// 地块信息模型
    /// </summary>
    public class PlotInfo
    {
        /// <summary>
        /// 对象 ID
        /// </summary>
        public long ObjectID { get; set; }

        /// <summary>
        /// 综合评分 (gridcode 字段值)
        /// </summary>
        public double GridCode { get; set; }

        /// <summary>
        /// 单因子评分字典 (字段名 -> 分数)
        /// </summary>
        public Dictionary<string, double> FactorScores { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// 地块范围
        /// </summary>
        public Envelope Extent { get; set; }

        /// <summary>
        /// 地块面积 (平方米)
        /// </summary>
        public double? Area { get; set; }
    }
}