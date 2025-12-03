using System.Windows;
using System.Windows.Controls;
using HK_AREA_SEARCH.Models;
using HK_AREA_SEARCH.Helpers;

namespace HK_AREA_SEARCH
{
    /// <summary>
    /// main_dockpaneView.xaml 的交互逻辑
    /// </summary>
    public partial class main_dockpaneView : UserControl
    {
        public main_dockpaneView()
        {
            InitializeComponent();
        }

        
        #region 约束条件拖放事件

        private void ConstraintItem_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (DragDropHelper.IsValidDragDropData(e))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void ConstraintItem_Drop(object sender, DragEventArgs e)
        {
            var textBox = sender as TextBox;
            var item = textBox?.DataContext as ConstraintDataItem;

            if (item != null)
            {
                string path = DragDropHelper.GetLayerPathFromDragDrop(e);
                if (!string.IsNullOrEmpty(path))
                {
                    item.DataPath = path;
                }
            }

            e.Handled = true;
        }

        #endregion

        #region 分析区域拖放事件

        private void AnalysisArea_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (DragDropHelper.IsValidDragDropData(e))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void AnalysisArea_Drop(object sender, DragEventArgs e)
        {
            var viewModel = DataContext as main_dockpaneViewModel;
            if (viewModel != null)
            {
                string path = DragDropHelper.GetLayerPathFromDragDrop(e);
                if (!string.IsNullOrEmpty(path))
                {
                    viewModel.AnalysisAreaPath = path;
                }
            }

            e.Handled = true;
        }

        #endregion

        #region POI拖放事件

        private void POIItem_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (DragDropHelper.IsValidDragDropData(e))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void POIItem_Drop(object sender, DragEventArgs e)
        {
            var textBox = sender as TextBox;
            var item = textBox?.DataContext as POIDataItem;

            if (item != null)
            {
                string path = DragDropHelper.GetLayerPathFromDragDrop(e);
                if (!string.IsNullOrEmpty(path))
                {
                    item.DataPath = path;
                }
            }

            e.Handled = true;
        }

        #endregion
    }
}
