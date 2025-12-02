using System.Windows;

namespace HK_AREA_SEARCH.Views
{
    public partial class ProgressDialog : Window
    {
        public ProgressDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 更新进度
        /// </summary>
        public void UpdateProgress(string message, int percent)
        {
            StatusText.Text = message;
            ProgressBar.Value = percent;
        }
    }
}