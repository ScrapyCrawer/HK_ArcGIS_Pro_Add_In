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
                // ⭐ 验证输入
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

        #endregion

        #region 命令

        public ICommand BrowseConstraintCommand { get; private set; }
        public ICommand BrowseAnalysisAreaCommand { get; private set; }
        public ICommand BrowsePOICommand { get; private set; }
        public ICommand BrowseOutputCommand { get; private set; }
        public ICommand RunAnalysisCommand { get; private set; }
        public ICommand ClearConstraintsCommand { get; private set; }
        public ICommand ClearPOIsCommand { get; private set; }

        #endregion

        #region 构造函数

        protected main_dockpaneViewModel()
        {
            InitializeCollections();
            InitializeCommands();
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
                    System.Diagnostics.Debug.WriteLine($"=======================================");
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

        #region DockPane方法

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
