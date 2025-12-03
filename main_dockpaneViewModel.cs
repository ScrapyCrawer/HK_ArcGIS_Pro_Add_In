using System;
using System.Collections.Generic; 
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using HK_AREA_SEARCH.Models;
using HK_AREA_SEARCH.Divide;
using HK_AREA_SEARCH.Distance;
using HK_AREA_SEARCH.Rating;
using HK_AREA_SEARCH.Infrastructure.Services;
using HK_AREA_SEARCH.Infrastructure.Helpers;
using HK_AREA_SEARCH.Common;
using HK_AREA_SEARCH.Views;
using HK_AREA_SEARCH.ViewModels;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System.Text;
using ProgressDialog = HK_AREA_SEARCH.Views.ProgressDialog;

namespace HK_AREA_SEARCH
{
    internal class main_dockpaneViewModel : DockPane, IProgressReporter
    {
        private const string _dockPaneID = "HK_AREA_SEARCH_main_dockpane";
        private ProgressDialog _progressDialog;

        #region 属性

        private string _heading = "Site Analysis";
        public string Heading
        {
            get { return _heading; }
            set { SetProperty(ref _heading, value, () => Heading); }
        }

        private ObservableCollection<ConstraintDataItem> _constraintItems;
        public ObservableCollection<ConstraintDataItem> ConstraintItems
        {
            get { return _constraintItems; }
            set { SetProperty(ref _constraintItems, value, () => ConstraintItems); }
        }

        private string _analysisAreaPath;
        public string AnalysisAreaPath
        {
            get { return _analysisAreaPath; }
            set { SetProperty(ref _analysisAreaPath, value, () => AnalysisAreaPath); }
        }

        private ObservableCollection<POIDataItem> _poiItems;
        public ObservableCollection<POIDataItem> POIItems
        {
            get { return _poiItems; }
            set { SetProperty(ref _poiItems, value, () => POIItems); }
        }

        private string _outputPath;
        public string OutputPath
        {
            get { return _outputPath; }
            set { SetProperty(ref _outputPath, value, () => OutputPath); }
        }

        private string _weightSumValidationMessage;
        public string WeightSumValidationMessage
        {
            get { return _weightSumValidationMessage; }
            set { SetProperty(ref _weightSumValidationMessage, value, () => WeightSumValidationMessage); }
        }

        private bool _isWeightSumValid;
        public bool IsWeightSumValid
        {
            get { return _isWeightSumValid; }
            set { SetProperty(ref _isWeightSumValid, value, () => IsWeightSumValid); }
        }

        //  面积筛选功能属性
        private bool _enableAreaFilter;
        public bool EnableAreaFilter
        {
            get { return _enableAreaFilter; }
            set { SetProperty(ref _enableAreaFilter, value, () => EnableAreaFilter); }
        }

        private double? _minArea;
        public double? MinArea
        {
            get { return _minArea; }
            set 
            { 
                // ⭐ 验证输入
                if (value.HasValue && value.Value < 0)
                {
                    MessageBox.Show("Minimum area cannot be negative", "Invalid Input");
                    return;
                }
                
                if (MaxArea.HasValue && value.HasValue && value.Value > MaxArea.Value)
                {
                    MessageBox.Show("Minimum area cannot be greater than maximum area", "Invalid Input");
                    return;
                }

                SetProperty(ref _minArea, value, () => MinArea); 
            }
        }

        private double? _maxArea;
        public double? MaxArea
        {
            get { return _maxArea; }
            set 
            { 
                if (value.HasValue && value.Value <= 0)
                {
                    MessageBox.Show("Maximum area must be greater than 0", "Invalid Input");
                    return;
                }
                
                if (MinArea.HasValue && value.HasValue && value.Value < MinArea.Value)
                {
                    MessageBox.Show("Maximum area cannot be less than minimum area", "Invalid Input");
                    return;
                }

                SetProperty(ref _maxArea, value, () => MaxArea); 
            }
        }

        // ⭐ 添加地块详情属性
        #region 地块详情属性

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get { return _selectedTabIndex; }
            set { SetProperty(ref _selectedTabIndex, value, () => SelectedTabIndex); }
        }

        private ObservableCollection<FactorScoreItem> _factorScores;
        public ObservableCollection<FactorScoreItem> FactorScores
        {
            get { return _factorScores; }
            set { SetProperty(ref _factorScores, value, () => FactorScores); }
        }

        private string _plotDescription;
        public string PlotDescription
        {
            get { return _plotDescription; }
            set { SetProperty(ref _plotDescription, value, () => PlotDescription); }
        }

        private PlotInfo _selectedPlotInfo;
        public PlotInfo SelectedPlotInfo
        {
            get { return _selectedPlotInfo; }
            set { SetProperty(ref _selectedPlotInfo, value, () => SelectedPlotInfo); }
        }

        private string _resultShapefilePath;
        public string ResultShapefilePath
        {
            get { return _resultShapefilePath; }
            set 
            { 
                SetProperty(ref _resultShapefilePath, value, () => ResultShapefilePath);
                
                // 通知 ResultShapefileName 也改变了
                NotifyPropertyChanged(() => ResultShapefileName);
                
                if (!string.IsNullOrEmpty(value))
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Result Shapefile Path set: {value}");
                    SelectedTabIndex = 1;
                }
            }
        }

        public string ResultShapefileName
        {
            get
            {
                if (string.IsNullOrEmpty(ResultShapefilePath))
                    return string.Empty;
                
                return System.IO.Path.GetFileName(ResultShapefilePath);
            }
        }

        private bool _isPlotListeningEnabled;
        /// <summary>
        /// 是否启用地块监听
        /// </summary>
        public bool IsPlotListeningEnabled
        {
            get { return _isPlotListeningEnabled; }
            set 
            { 
                SetProperty(ref _isPlotListeningEnabled, value, () => IsPlotListeningEnabled);
                NotifyPropertyChanged(() => PlotListeningButtonText);
                NotifyPropertyChanged(() => PlotListeningButtonColor);
            }
        }

        /// <summary>
        /// 浏览地块按钮文字
        /// </summary>
        public string PlotListeningButtonText
        {
            get { return IsPlotListeningEnabled ? "🔴 Stop Browsing" : "🟢 Browse Plots"; }
        }

        /// <summary>
        /// 浏览地块按钮颜色
        /// </summary>
        public string PlotListeningButtonColor
        {
            get { return IsPlotListeningEnabled ? "#FFDC143C" : "#FF32CD32"; }
        }

        #endregion

        #endregion

        #region 命令

        public ICommand BrowseConstraintCommand { get; private set; }
        public ICommand BrowseAnalysisAreaCommand { get; private set; }
        public ICommand BrowsePOICommand { get; private set; }
        public ICommand BrowseOutputCommand { get; private set; }
        public ICommand RunAnalysisCommand { get; private set; }
        public ICommand ClearConstraintsCommand { get; private set; }
        public ICommand ClearPOIsCommand { get; private set; }
        public ICommand BrowseResultShapefileCommand { get; private set; }  // ⭐ 添加命令
        public ICommand TogglePlotListeningCommand { get; private set; }  // ⭐ 新增

        #endregion

        #region 构造函数

        protected main_dockpaneViewModel()
        {
            InitializeCollections();
            InitializeCommands();
            
            // ⭐ 订阅地图选择变化事件
            MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
            
            System.Diagnostics.Debug.WriteLine("✅ Map selection event subscribed");
        }

        private void InitializeCollections()
        {
            ConstraintItems = new ObservableCollection<ConstraintDataItem>();
            AddEmptyConstraintRow();

            POIItems = new ObservableCollection<POIDataItem>();
            AddEmptyPOIRow();
        }

        private void InitializeCommands()
        {
            BrowseConstraintCommand = new RelayCommand(BrowseConstraintData, (object parameter) => true);
            BrowseAnalysisAreaCommand = new RelayCommand(BrowseAnalysisArea, (object parameter) => true);
            BrowsePOICommand = new RelayCommand(BrowsePOIData, (object parameter) => true);
            BrowseOutputCommand = new RelayCommand(BrowseOutputPath, (object parameter) => true);
            RunAnalysisCommand = new RelayCommand(async (param) => await RunAnalysisAsync(param), CanRunAnalysis);
            ClearConstraintsCommand = new RelayCommand(ClearConstraints, (object parameter) => true);
            ClearPOIsCommand = new RelayCommand(ClearPOIs, (object parameter) => true);
            BrowseResultShapefileCommand = new RelayCommand(BrowseResultShapefile, (object parameter) => true);  // ⭐ 初始化命令
            TogglePlotListeningCommand = new RelayCommand(TogglePlotListening, (object parameter) => true);  // ⭐ 初始化命令
        }

        #endregion

        #region 进度报告

        /// <summary>
        /// 实现 IProgressReporter 接口
        /// </summary>
        public void ReportProgress(string message, int percent)
        {
            // 在 UI 线程更新进度对话框
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _progressDialog?.UpdateProgress(message, percent);
                System.Diagnostics.Debug.WriteLine($"Progress: {percent}% - {message}");
            });
        }

        #endregion

        #region 动态空行管理

        private void AddEmptyConstraintRow()
        {
            var newItem = new ConstraintDataItem();
            newItem.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(ConstraintDataItem.DataPath))
                {
                    CheckAndAddEmptyConstraintRow();
                }
            };
            ConstraintItems.Add(newItem);
        }

        private void AddEmptyPOIRow()
        {
            var newItem = new POIDataItem();
            newItem.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(POIDataItem.DataPath))
                {
                    CheckAndAddEmptyPOIRow();
                }

                if (e.PropertyName == nameof(POIDataItem.Weight))
                {
                    ValidateWeightSum();
                }

                if (e.PropertyName == nameof(POIDataItem.NeedsCustomIntervalDialog))
                {
                    var poiItem = sender as POIDataItem;
                    if (poiItem != null && poiItem.NeedsCustomIntervalDialog)
                    {
                        ShowCustomIntervalDialog(poiItem);
                    }
                }
            };
            POIItems.Add(newItem);
        }

        private void CheckAndAddEmptyConstraintRow()
        {
            var lastItem = ConstraintItems.LastOrDefault();
            if (lastItem != null && !lastItem.IsEmpty)
            {
                AddEmptyConstraintRow();
            }
        }

        private void CheckAndAddEmptyPOIRow()
        {
            var lastItem = POIItems.LastOrDefault();
            if (lastItem != null && !lastItem.IsEmpty)
            {
                AddEmptyPOIRow();
            }
        }

        #endregion

        #region 方法

        private void ShowCustomIntervalDialog(POIDataItem poiItem)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"========== Opening Custom Interval Dialog ==========");
                System.Diagnostics.Debug.WriteLine($"POI: {poiItem.DataName}");
                System.Diagnostics.Debug.WriteLine($"Data Path: {poiItem.DataPath}");

                var dialog = new CustomIntervalDialog(poiItem.DataPath, poiItem);
                
                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    System.Diagnostics.Debug.WriteLine("User confirmed custom intervals");
                    
                    var viewModel = dialog.DataContext as CustomIntervalDialogViewModel;
                    if (viewModel != null && viewModel.ClassItems != null)
                    {
                        poiItem.CustomIntervalClasses = new List<IntervalClassItem>(viewModel.ClassItems);
                        System.Diagnostics.Debug.WriteLine($"Saved {poiItem.CustomIntervalClasses.Count} custom intervals");
                    }
                    
                    poiItem.MarkCustomIntervalConfigured();
                    
                    MessageBox.Show($"Custom intervals configured for {poiItem.DataName}", "Success");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("User cancelled custom intervals");
                    
                    // 用户取消,将 CustomInterval 改回 false 并重置配置状态
                    poiItem.CustomInterval = false;
                    poiItem.ResetCustomIntervalConfiguration();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening custom interval dialog: {ex.Message}");
                MessageBox.Show($"Error opening custom interval dialog: {ex.Message}", "Error");
                
                // 出错也要改回 false
                poiItem.CustomInterval = false;
                poiItem.ResetCustomIntervalConfiguration();
            }
        }

        private void ValidateWeightSum()
        {
            var nonEmptyItems = POIItems.Where(p => !p.IsEmpty && p.Weight.HasValue).ToList();

            if (nonEmptyItems.Count == 0)
            {
                IsWeightSumValid = false;
                WeightSumValidationMessage = "";
                return;
            }

            double sum = nonEmptyItems.Sum(p => p.Weight.Value);

            if (Math.Abs(sum - 1.0) > 0.001)
            {
                IsWeightSumValid = true;
                WeightSumValidationMessage = $"Warning: Weight sum is {sum:F2}, should be 1.0";
            }
            else
            {
                IsWeightSumValid = false;
                WeightSumValidationMessage = "";
            }
        }

        /// <summary>
        /// ⭐ 改进: 浏览约束条件数据 - 支持多选
        /// </summary>
        private void BrowseConstraintData(object parameter)
        {
            var item = parameter as ConstraintDataItem;
            if (item == null) return;

            var dialog = new OpenFileDialog
            {
                Filter = "Shapefile (*.shp)|*.shp|GeoDatabase Feature Class|*.gdb|All Files (*.*)|*.*",
                Title = "Select Constraint Data - Hold Ctrl/Shift for Multiple Selection",  // ⭐ 友好提示
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                // ⭐ 获取所有选中的文件
                string[] selectedFiles = dialog.FileNames;

                if (selectedFiles.Length == 0)
                    return;

                // ⭐ 第一个文件填入当前行
                item.DataPath = selectedFiles[0];

                // ⭐ 如果选择了多个文件，添加额外的行
                if (selectedFiles.Length > 1)
                {
                    // 找到当前项的索引
                    int currentIndex = ConstraintItems.IndexOf(item);
                    
                    // 从第二个文件开始，插入新行
                    for (int i = 1; i < selectedFiles.Length; i++)
                    {
                        var newItem = new ConstraintDataItem
                        {
                            DataPath = selectedFiles[i]
                        };

                        // 添加属性变化监听
                        newItem.PropertyChanged += (sender, e) =>
                        {
                            if (e.PropertyName == nameof(ConstraintDataItem.DataPath))
                            {
                                CheckAndAddEmptyConstraintRow();
                            }
                        };

                        // ⭐ 在当前项后面插入新行
                        ConstraintItems.Insert(currentIndex + i, newItem);
                    }

                    System.Diagnostics.Debug.WriteLine($"========== Batch Add Constraints ==========");
                    System.Diagnostics.Debug.WriteLine($"Added {selectedFiles.Length} constraint files");
                    foreach (var file in selectedFiles)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {Path.GetFileName(file)}");
                    }
                    System.Diagnostics.Debug.WriteLine($"==========================================");
                }
            }
        }

        private void BrowseAnalysisArea(object parameter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Shapefile (*.shp)|*.shp|GeoDatabase Feature Class|*.gdb|All Files (*.*)|*.*",
                Title = "Select Analysis Area"
            };

            if (dialog.ShowDialog() == true)
            {
                AnalysisAreaPath = dialog.FileName;
            }
        }

        private void BrowsePOIData(object parameter)
        {
            var item = parameter as POIDataItem;
            if (item == null) return;

            var dialog = new OpenFileDialog
            {
                Filter = "All Supported Formats|*.shp;*.tif;*.tiff;*.img|Shapefile (*.shp)|*.shp|Raster (*.tif;*.tiff;*.img)|*.tif;*.tiff;*.img|All Files (*.*)|*.*",
                Title = "Select POI Data - Ctrl/Shift for Multiple Selection",
                Multiselect = true  // ⭐ 启用多选
            };

            if (dialog.ShowDialog() == true)
            {
                // ⭐ 获取所有选中的文件
                string[] selectedFiles = dialog.FileNames;

                if (selectedFiles.Length == 0)
                    return;

                // ⭐ 第一个文件填入当前行
                item.DataPath = selectedFiles[0];

                // ⭐ 如果选择了多个文件，添加额外的行
                if (selectedFiles.Length > 1)
                {
                    // 找到当前项的索引
                    int currentIndex = POIItems.IndexOf(item);
                    
                    // 从第二个文件开始，插入新行
                    for (int i = 1; i < selectedFiles.Length; i++)
                    {
                        var newItem = new POIDataItem
                        {
                            DataPath = selectedFiles[i]
                        };

                        // 添加属性变化监听
                        newItem.PropertyChanged += (sender, e) =>
                        {
                            if (e.PropertyName == nameof(POIDataItem.DataPath))
                            {
                                CheckAndAddEmptyPOIRow();
                            }

                            if (e.PropertyName == nameof(POIDataItem.Weight))
                            {
                                ValidateWeightSum();
                            }

                            // ⭐ 修改: 监听 NeedsCustomIntervalDialog 而不是 CustomInterval
                            if (e.PropertyName == nameof(POIDataItem.NeedsCustomIntervalDialog))
                            {
                                var poiItem = sender as POIDataItem;
                                if (poiItem != null && poiItem.NeedsCustomIntervalDialog)
                                {
                                    ShowCustomIntervalDialog(poiItem);
                                }
                            }
                        };

                        // ⭐ 在当前项后面插入新行
                        POIItems.Insert(currentIndex + i, newItem);
                    }

                    System.Diagnostics.Debug.WriteLine($"========== Batch Add POI Data ==========");
                    System.Diagnostics.Debug.WriteLine($"Added {selectedFiles.Length} POI files");
                    foreach (var file in selectedFiles)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {Path.GetFileName(file)}");
                    }
                    System.Diagnostics.Debug.WriteLine($"========================================");
                }
            }
        }

        private void BrowseOutputPath(object parameter)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Shapefile (*.shp)|*.shp",
                Title = "Select Output Path",
                FileName = "Result_Rating_Suitable_Area.shp"
            };

            if (dialog.ShowDialog() == true)
            {
                OutputPath = dialog.FileName;
            }
        }

        private bool CanRunAnalysis(object parameter)
        {
            if (string.IsNullOrWhiteSpace(AnalysisAreaPath) ||
                !POIItems.Any(p => !p.IsEmpty) ||
                string.IsNullOrWhiteSpace(OutputPath))
            {
                return false;
            }

            foreach (var item in POIItems.Where(p => !p.IsEmpty))
            {
                if (!item.IsRasterData && !item.Distance.HasValue)
                {
                    return false;
                }

                if (!item.Weight.HasValue || !item.WeightHasBeenSetByUser)
                {
                    return false;
                }
            }

            return true;
        }

        private async System.Threading.Tasks.Task RunAnalysisAsync(object parameter)
        {
            try
            {
                //  运行前清理旧的临时文件夹 (可选,清理7天前的)
                TempFileManager.CleanupOldTempFolders(7);
                
                // 创建并显示进度对话框
                _progressDialog = new ProgressDialog();
                _progressDialog.Owner = System.Windows.Application.Current.MainWindow;
                _progressDialog.Show();

                ReportProgress("Validating inputs...", 0);

                // 1. 数据验证
                var validationResult = ValidationHelper.ValidateInputs(
                    AnalysisAreaPath,
                    ConstraintItems.Where(c => !c.IsEmpty).ToList(),
                    POIItems.Where(p => !p.IsEmpty).ToList(),
                    OutputPath);

                if (!validationResult.IsValid)
                {
                    _progressDialog?.Close();
                    MessageBox.Show(validationResult.ErrorMessage, "Validation Failed");
                    return;
                }

                ReportProgress("Initializing...", 5);
                
                // ⭐ 创建新的临时文件管理器 (每次都是新文件夹)
                var tempFileManager = new TempFileManager();
                
                System.Diagnostics.Debug.WriteLine($"Using temp directory: {tempFileManager.GetTempDirectory()}");

                // 2. 执行模块1: 划分可建设土地
                ReportProgress("Step 1/4: Dividing suitable land areas...", 10);
                var divideService = new DivideService(tempFileManager);

                // 根据用户设置传递面积筛选参数
                double? minAreaFilter = EnableAreaFilter ? MinArea : null;
                double? maxAreaFilter = EnableAreaFilter ? MaxArea : null;

                var suitableAreaPath = await divideService.ExecuteAsync(
                    AnalysisAreaPath,
                    ConstraintItems.Where(c => !c.IsEmpty).Select(c => c.DataPath).ToList(),
                    tempFileManager.CreateTempFile(Constants.SUITABLE_AREA_FILENAME),
                    minAreaFilter,
                    maxAreaFilter  //  新增最大面积参数
                );
                
                // 3. 执行模块2: 距离计算
                ReportProgress("Step 2/4: Calculating distance rasters...", 30);
                var distanceService = new DistanceService(tempFileManager);
                var processedRasters = await distanceService.ExecuteAsync(
                    POIItems.Where(p => !p.IsEmpty).ToList(),
                    AnalysisAreaPath
                );

                // 4. 执行模块3: 评分计算
                ReportProgress("Step 3/4: Calculating ratings...", 60);
                var ratingService = new RatingService(tempFileManager);
                var weights = POIItems.Where(p => !p.IsEmpty)
                    .ToDictionary(p => p.DataName, p => p.Weight.Value);
                // ⭐ 传递最小面积参数到 RatingService
                var resultPath = await ratingService.ExecuteAsync(
                    processedRasters,
                    weights,
                    suitableAreaPath,
                    OutputPath,
                    minAreaFilter  // 传递最小面积参数
                );

                // 5. 加载结果
                ReportProgress("Step 4/4: Loading results to map...", 80);
                var layerManager = new LayerManager();
                var layer = await layerManager.AddLayerToMap(resultPath);

                ReportProgress("Applying symbology...", 90);
                var symbologyManager = new SymbologyManager();
                await symbologyManager.ApplyGraduatedColors(layer, Constants.RATING_FIELD);

                ReportProgress("Cleaning up temporary files...", 95);
                
                // ⭐ 添加延迟,确保ArcGIS释放资源
                await System.Threading.Tasks.Task.Delay(1000);
                
                tempFileManager.CleanupAll();

                // ⭐ 强制垃圾回收 (可选)
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                ReportProgress("Analysis completed!", 100);

                // 关闭进度对话框
                _progressDialog?.Close();
                _progressDialog = null;

                MessageBox.Show("Site selection analysis completed!", "Success");
            }
            catch (Exception ex)
            {
                // 关闭进度对话框
                _progressDialog?.Close();
                _progressDialog = null;

                LogService.LogError($"Analysis failed: {ex.Message}");
                MessageBox.Show($"Analysis failed: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// 清除所有约束条件（保留一个空行）
        /// </summary>
        private void ClearConstraints(object parameter)
        {
            // 确认对话框
            var result = MessageBox.Show(
                "Are you sure you want to clear all constraint items?",
                "Clear Constraints",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                ConstraintItems.Clear();
                AddEmptyConstraintRow();
                
                System.Diagnostics.Debug.WriteLine("========== Cleared All Constraints ==========");
            }
        }

        /// <summary>
        /// 清除所有 POI 数据（保留一个空行）
        /// </summary>
        private void ClearPOIs(object parameter)
        {
            // 确认对话框
            var result = MessageBox.Show(
                "Are you sure you want to clear all POI items?",
                "Clear POI Data",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                POIItems.Clear();
                AddEmptyPOIRow();
                
                // 清除权重验证消息
                WeightSumValidationMessage = "";
                IsWeightSumValid = false;
                
                System.Diagnostics.Debug.WriteLine("========== Cleared All POI Items ==========");
            }
        }

        #endregion

        /// <summary>
        /// 浏览结果 Shapefile
        /// </summary>
        private void BrowseResultShapefile(object parameter) 
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Shapefile (*.shp)|*.shp|All Files (*.*)|*.*",
                    Title = "Select Result Shapefile"
                };

                if (dialog.ShowDialog() == true)
                {
                    ResultShapefilePath = dialog.FileName;
                    System.Diagnostics.Debug.WriteLine($"✅ Selected Result Shapefile: {ResultShapefilePath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ BrowseResultShapefile failed: {ex.Message}");
                MessageBox.Show($"Failed to load shapefile: {ex.Message}",
                               "Error",
                               System.Windows.MessageBoxButton.OK,
                               System.Windows.MessageBoxImage.Error);
            }
        }

        #region 地块监听功能

        /// <summary>
        /// 切换地块监听状态
        /// </summary>
        private void TogglePlotListening(object parameter)
        {
            IsPlotListeningEnabled = !IsPlotListeningEnabled;
            
            if (IsPlotListeningEnabled)
            {
                // 检查是否已选择结果 Shapefile
                if (string.IsNullOrEmpty(ResultShapefilePath))
                {
                    MessageBox.Show("Please select a result shapefile first!", 
                                  "No Shapefile Selected", 
                                  System.Windows.MessageBoxButton.OK, 
                                  System.Windows.MessageBoxImage.Warning);
                    IsPlotListeningEnabled = false;
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine("✅ Plot listening ENABLED");
                MessageBox.Show("Plot browsing mode enabled!\nClick on any plot on the map to view details.", 
                               "Browse Mode ON", 
                               System.Windows.MessageBoxButton.OK, 
                               System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⛔ Plot listening DISABLED");
            }
        }

        /// <summary>
        /// 地图选择变化事件处理
        /// </summary>
        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            // ⭐ 只有启用监听时才处理
            if (!IsPlotListeningEnabled)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Plot listening is disabled, ignoring selection");
                return;
            }
            
            // ⭐ 检查是否已设置结果 Shapefile 路径
            if (string.IsNullOrEmpty(ResultShapefilePath))
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Result Shapefile Path not set, ignoring selection");
                return;
            }

            _ = QueuedTask.Run(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("========== Map Selection Changed ==========");
                    
                    var selection = args.Selection;
                    if (selection == null || selection.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("No selection");
                        return;
                    }

                    // ⭐ 修复：将 SelectionSet 转换为 Dictionary
                    var layers = selection.ToDictionary();
                    
                    // ⭐ 查找匹配结果 Shapefile 的图层
                    FeatureLayer resultLayer = null;
                    string resultFileName = System.IO.Path.GetFileNameWithoutExtension(ResultShapefilePath);
                    
                    foreach (var kvp in layers)
                    {
                        var layer = kvp.Key as FeatureLayer;
                        if (layer != null && layer.Name.Contains(resultFileName))
                        {
                            resultLayer = layer;
                            break;
                        }
                    }

                    if (resultLayer == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Result layer not found. Looking for: {resultFileName}");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"Result layer found: {resultLayer.Name}");

                    var selectedOIDs = resultLayer.GetSelection().GetObjectIDs();
                    if (selectedOIDs.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("No features selected");
                        return;
                    }

                    long firstOID = selectedOIDs.First();
                    System.Diagnostics.Debug.WriteLine($"Selected OID: {firstOID}");

                    await ExtractPlotDataAsync(resultLayer, firstOID);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error in OnMapSelectionChanged: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 提取选中地块的数据
        /// </summary>
        private async System.Threading.Tasks.Task ExtractPlotDataAsync(FeatureLayer layer, long oid)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($">>> Extracting data for OID: {oid}");

                var plotInfo = new PlotInfo { ObjectID = oid };

                // 创建查询过滤器
                var queryFilter = new QueryFilter
                {
                    ObjectIDs = new List<long> { oid }
                };

                // 查询要素
                using (var rowCursor = layer.Search(queryFilter))
                {
                    if (rowCursor.MoveNext())
                    {
                        using (var row = rowCursor.Current)
                        {
                            // 1. 提取 gridcode（综合评分）
                            try
                            {
                                plotInfo.GridCode = Convert.ToDouble(row["gridcode"]);
                                System.Diagnostics.Debug.WriteLine($"GridCode: {plotInfo.GridCode}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ gridcode 字段不存在或无效: {ex.Message}");
                                plotInfo.GridCode = 0;
                            }

                            // 2. 提取所有 S_ 开头的单因子得分字段
                            var definition = row.GetTable().GetDefinition();
                            foreach (var field in definition.GetFields())
                            {
                                if (field.Name.StartsWith("S_") && 
                                    (field.FieldType == FieldType.Double || field.FieldType == FieldType.Single))
                                {
                                    try
                                    {
                                        var value = Convert.ToDouble(row[field.Name]);
                                        plotInfo.FactorScores[field.Name] = value;
                                        System.Diagnostics.Debug.WriteLine($"  {field.Name}: {value:F2}");
                                    }
                                    catch
                                    {
                                        // 忽略无效字段
                                    }
                                }
                            }

                            // 3. 提取几何范围
                            if (row is Feature feature)
                            {
                                var geometry = feature.GetShape();
                                if (geometry != null)
                                {
                                    plotInfo.Extent = geometry.Extent;
                                    
                                    // 计算面积（如果是多边形）
                                    if (geometry is Polygon polygon)
                                    {
                                        plotInfo.Area = polygon.Area;
                                        System.Diagnostics.Debug.WriteLine($"Area: {plotInfo.Area:F2} m²");
                                    }
                                }
                            }
                        }
                    }
                }

                // 4. 更新 UI（必须在 UI 线程）
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdatePlotDetails(plotInfo);
                });

                // 5. 缩放到地块
                await ZoomToPlotAsync(plotInfo.Extent);

                System.Diagnostics.Debug.WriteLine("✅ Plot data extracted successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ExtractPlotDataAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新地块详情界面
        /// </summary>
        private void UpdatePlotDetails(PlotInfo plotInfo)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(">>> Updating plot details UI");

                // 保存当前地块信息
                SelectedPlotInfo = plotInfo;

                // 1. 生成表格数据
                FactorScores = new ObservableCollection<FactorScoreItem>();
                
                foreach (var kvp in plotInfo.FactorScores.OrderByDescending(x => x.Value))
                {
                    var item = new FactorScoreItem
                    {
                        FactorName = kvp.Key,
                        DisplayName = ConvertFactorNameToDisplay(kvp.Key),
                        Score = kvp.Value
                    };
                    
                    FactorScores.Add(item);
                }

                System.Diagnostics.Debug.WriteLine($"Factor scores count: {FactorScores.Count}");

                // 2. 生成文字描述
                PlotDescription = GeneratePlotDescription(plotInfo);

                // 3. 自动切换到地块详情选项卡
                SelectedTabIndex = 1;

                System.Diagnostics.Debug.WriteLine("✅ Plot details updated");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ UpdatePlotDetails failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成地块描述文字
        /// </summary>
        private string GeneratePlotDescription(PlotInfo plotInfo)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"📍 Plot Analysis Report");
            sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine();

            sb.AppendLine($"🎯 Overall Score: {plotInfo.GridCode:F2} / 10");
            sb.AppendLine();

            string rating = plotInfo.GridCode >= 8 ? "Excellent (极佳)" :
                            plotInfo.GridCode >= 6 ? "Good (适宜)" :
                            plotInfo.GridCode >= 4 ? "Fair (一般)" :
                            "Poor (较差)";
            sb.AppendLine($"📊 Overall Rating: {rating}");
            sb.AppendLine();

            if (plotInfo.Area.HasValue)
            {
                sb.AppendLine($"📐 Area: {plotInfo.Area.Value:N2} m²");
                sb.AppendLine();
            }

            if (plotInfo.FactorScores.Count > 0)
            {
                var orderedScores = plotInfo.FactorScores.OrderByDescending(x => x.Value).ToList();
                var maxFactor = orderedScores.First();
                var minFactor = orderedScores.Last();

                sb.AppendLine($"✅ Main Advantages:");
                sb.AppendLine($"   • {ConvertFactorNameToDisplay(maxFactor.Key)}: {maxFactor.Value:F2} / 10");
                sb.AppendLine();

                sb.AppendLine($"⚠️ Areas for Attention:");
                sb.AppendLine($"   • {ConvertFactorNameToDisplay(minFactor.Key)}: {minFactor.Value:F2} / 10");
                sb.AppendLine();

                sb.AppendLine($"📋 Detailed Scores:");
                foreach (var score in orderedScores)
                {
                    string bar = new string('█', (int)(score.Value / 2));
                    sb.AppendLine($"   • {ConvertFactorNameToDisplay(score.Key),-20} {bar} {score.Value:F2}");
                }
            }
            else
            {
                sb.AppendLine("⚠️ No factor score data available.");
            }

            sb.AppendLine();
            sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }

        /// <summary>
        /// 转换因子字段名为显示名称
        /// </summary>
        private string ConvertFactorNameToDisplay(string factorName)
        {
            string name = factorName.StartsWith("S_") ? factorName.Substring(2) : factorName;

            var nameMap = new Dictionary<string, string>
            {
                { "Traffic", "Traffic Accessibility" },
                { "Facility", "Nearby Facilities" },
                { "Noise", "Noise Level" },
                { "Green", "Green Space" },
                { "School", "School Proximity" },
                { "Hospital", "Hospital Proximity" },
                { "Metro", "Metro Station Proximity" },
                { "Park", "Park Proximity" }
            };

            return nameMap.ContainsKey(name) ? nameMap[name] : name;
        }

        /// <summary>
        /// 缩放到选中地块
        /// </summary>
        private async System.Threading.Tasks.Task ZoomToPlotAsync(Envelope extent)
        {
            if (extent == null)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Extent is null, cannot zoom");
                return;
            }

            await QueuedTask.Run(() =>
            {
                try
                {
                    var mapView = MapView.Active;
                    if (mapView == null)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ No active map view");
                        return;
                    }

                    var expandedExtent = extent.Expand(1.2, 1.2, true);
                    mapView.ZoomTo(expandedExtent, TimeSpan.FromSeconds(0.8));

                    System.Diagnostics.Debug.WriteLine($"✅ Zoomed to plot extent");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ ZoomToPlotAsync failed: {ex.Message}");
                }
            });
        }

        #endregion

        #region DockPane方法

/// <summary>
/// 显示 DockPane
/// </summary>
internal static void Show()
{
    DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
    if (pane == null)
        return;

    pane.Activate();
}

#endregion
    }

    internal class main_dockpane_ShowButton : Button
    {
        protected override void OnClick()
        {
            main_dockpaneViewModel.Show();
        }
    }
}
