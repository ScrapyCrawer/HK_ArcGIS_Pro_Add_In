using ArcGIS.Desktop.Framework.Contracts;

namespace HK_AREA_SEARCH.Models
{
    /// <summary>
    /// 单因子得分项
    /// </summary>
    public class FactorScoreItem : PropertyChangedBase
    {
        private string _factorName;
        /// <summary>
        /// 因子名称（如 "S_Traffic"）
        /// </summary>
        public string FactorName
        {
            get { return _factorName; }
            set { SetProperty(ref _factorName, value, () => FactorName); }
        }

        private string _displayName;
        /// <summary>
        /// 显示名称（如 "Traffic"）
        /// </summary>
        public string DisplayName
        {
            get { return _displayName; }
            set { SetProperty(ref _displayName, value, () => DisplayName); }
        }

        private double _score;
        /// <summary>
        /// 得分值
        /// </summary>
        public double Score
        {
            get { return _score; }
            set 
            { 
                SetProperty(ref _score, value, () => Score);
                // 当得分改变时，自动更新柱状条宽度
                NotifyPropertyChanged(() => BarWidth);
            }
        }

        /// <summary>
        /// 柱状条宽度（基于得分计算，满分10分对应容器宽度）
        /// 实际宽度 = (Score / MaxScore) * 容器宽度
        /// 在 XAML 中容器宽度是 "*"，这里返回百分比
        /// </summary>
        public double BarWidth
        {
            get
            {
                // 假设最大分数是 10
                const double maxScore = 10.0;
                // 计算百分比宽度（0-100）
                return (Score / maxScore) * 100.0;
            }
        }

        /// <summary>
        /// 用于绑定到 Rectangle 的实际宽度（需要容器宽度）
        /// 这个属性在运行时计算
        /// </summary>
        private double _actualBarWidth;
        public double ActualBarWidth
        {
            get { return _actualBarWidth; }
            set { SetProperty(ref _actualBarWidth, value, () => ActualBarWidth); }
        }
    }
}