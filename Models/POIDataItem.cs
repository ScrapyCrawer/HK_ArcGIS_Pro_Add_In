using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HK_AREA_SEARCH.Models
{
    /// <summary>
    /// POI数据项模型(支持动态空行)
    /// </summary>
    public class POIDataItem : INotifyPropertyChanged
    {
        private string _dataPath;
        private string _dataName;
        private int? _distance;
        private double? _weight;
        private bool _weightHasBeenSetByUser = false;
        private bool _customInterval;
        private bool _customIntervalConfigured = false;
        private bool _isRasterData;
        
        // ⭐ 新增: 存储自定义间隔配置
        private List<IntervalClassItem> _customIntervalClasses;

        /// <summary>
        /// 输入数据路径
        /// </summary>
        public string DataPath
        {
            get => _dataPath;
            set
            {
                if (_dataPath != value)
                {
                    _dataPath = value;
                    OnPropertyChanged();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        DataName = System.IO.Path.GetFileNameWithoutExtension(value);

                        var ext = System.IO.Path.GetExtension(value)?.ToLower();
                        IsRasterData = ext == ".tif" || ext == ".tiff" || ext == ".img";
                    }
                    else
                    {
                        DataName = string.Empty;
                        IsRasterData = false;
                    }
                }
            }
        }

        public string DataName
        {
            get => _dataName;
            set
            {
                if (_dataName != value)
                {
                    _dataName = value;
                    OnPropertyChanged();
                }
            }
        }

        public int? Distance
        {
            get => _distance;
            set
            {
                if (IsRasterData)
                {
                    _distance = null;
                }
                else
                {
                    _distance = value;
                }
                OnPropertyChanged();
            }
        }

        public double? Weight
        {
            get => _weight;
            set
            {
                if (value.HasValue)
                {
                    _weight = Math.Round(value.Value, 2);
                    _weightHasBeenSetByUser = true;
                }
                else
                {
                    _weight = value;
                    _weightHasBeenSetByUser = false;
                }
                OnPropertyChanged();
            }
        }

        public bool WeightHasBeenSetByUser => _weightHasBeenSetByUser;

        public bool CustomInterval
        {
            get => _customInterval;
            set
            {
                // ⭐ 移除条件判断，确保每次都触发属性变化通知
                var oldValue = _customInterval;
                _customInterval = value;
                OnPropertyChanged();
                
                // 如果从 false 变为 true,重置配置标志
                if (value && !_customIntervalConfigured)
                {
                    OnPropertyChanged(nameof(NeedsCustomIntervalDialog));
                }
                
                // 如果取消自定义间隔,清除保存的配置
                if (!value)
                {
                    _customIntervalClasses = null;
                    _customIntervalConfigured = false;  // ⭐ 同时重置配置状态
                }
            }
        }

        /// <summary>
        /// ⭐ 新增: 是否需要弹出自定义间隔对话框
        /// </summary>
        public bool NeedsCustomIntervalDialog
        {
            get
            {
                return CustomInterval && !_customIntervalConfigured && !IsEmpty;
            }
        }

        /// <summary>
        /// ⭐ 新增: 存储的自定义间隔配置
        /// </summary>
        public List<IntervalClassItem> CustomIntervalClasses
        {
            get => _customIntervalClasses;
            set
            {
                _customIntervalClasses = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// ⭐ 新增: 标记自定义间隔已配置
        /// </summary>
        public void MarkCustomIntervalConfigured()
        {
            _customIntervalConfigured = true;
            OnPropertyChanged(nameof(NeedsCustomIntervalDialog));
        }

        /// <summary>
        /// ⭐ 新增: 重置自定义间隔配置状态
        /// </summary>
        public void ResetCustomIntervalConfiguration()
        {
            _customIntervalConfigured = false;
            _customIntervalClasses = null;
            OnPropertyChanged(nameof(NeedsCustomIntervalDialog));
        }

        public bool IsRasterData
        {
            get => _isRasterData;
            private set
            {
                if (_isRasterData != value)
                {
                    _isRasterData = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDistanceEnabled));

                    if (_isRasterData)
                    {
                        Distance = null;
                    }
                }
            }
        }

        public bool IsDistanceEnabled => !IsRasterData;
        public bool IsEmpty => string.IsNullOrWhiteSpace(DataPath);

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}