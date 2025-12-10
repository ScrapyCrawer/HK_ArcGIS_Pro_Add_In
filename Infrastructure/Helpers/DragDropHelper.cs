using System;
using System.Windows;

namespace HK_AREA_SEARCH.Infrastructure.Helpers
{
    /// <summary>
    /// 拖拽辅助
    /// </summary>
    public static class DragDropHelper
    {
        /// <summary>
        /// 执行拖拽操作
        /// </summary>
        /// <param name="data">拖拽的数据</param>
        public static void DoDragDrop(object data)
        {
            if (data != null)
            {
                DataObject dataObject = new DataObject(data);
                DragDrop.DoDragDrop((DependencyObject)data, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
            }
        }

        /// <summary>
        /// 验证拖拽数据
        /// </summary>
        /// <param name="e">拖拽事件参数</param>
        /// <param name="allowedTypes">允许的数据类型</param>
        /// <returns>是否有效</returns>
        public static bool ValidateDragData(DragEventArgs e, params Type[] allowedTypes)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return false;

            if (allowedTypes == null || allowedTypes.Length == 0)
                return true; // 如果没有指定类型限制，则允许所有类型

            // 验证数据类型
            foreach (Type type in allowedTypes)
            {
                if (e.Data.GetDataPresent(type))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 验证拖放数据是否有效（特定于GIS图层）
        /// </summary>
        /// <param name="e">拖放事件参数</param>
        /// <returns>是否有效</returns>
        public static bool IsValidDragDropData(DragEventArgs e)
        {
            // 检查是否有文件拖放数据
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return false;

            // 获取文件路径
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
                return false;

            // 检查文件扩展名是否为支持的GIS格式
            string extension = System.IO.Path.GetExtension(files[0])?.ToLower();
            if (string.IsNullOrEmpty(extension))
                return false;

            // 支持的GIS文件扩展名
            string[] supportedExtensions = { ".shp", ".tif", ".tiff", ".img", ".gdb", ".dbf", ". lyr", ".kml", ".geojson" };
            
            return Array.Exists(supportedExtensions, ext => ext == extension);
        }

        /// <summary>
        /// 从拖放操作中获取图层路径
        /// </summary>
        /// <param name="e">拖放事件参数</param>
        /// <returns>图层路径</returns>
        public static string GetLayerPathFromDragDrop(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return null;

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
                return null;

            return files[0]; // 返回第一个文件路径
        }
    }
}