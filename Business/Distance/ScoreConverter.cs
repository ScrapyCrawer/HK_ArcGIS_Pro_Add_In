using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using HK_AREA_SEARCH.Models;
using HK_AREA_SEARCH.Common;

namespace HK_AREA_SEARCH.Business.Distance
{
    public class ScoreConverter
    {
        public const string RATING_FIELD = "Rating";

        public double ConvertDistanceToScore(double distance, int numClasses, double maxDistance, bool invert = true)
        {
            double interval = maxDistance / numClasses;

            if (invert)
            {
                for (int i = 0; i < numClasses; i++)
                {
                    if (distance >= i * interval && distance < (i + 1) * interval)
                        return numClasses - i;
                }
            }
            else
            {
                for (int i = 0; i < numClasses; i++)
                {
                    if (distance >= i * interval && distance < (i + 1) * interval)
                        return i + 1;
                }
            }

            return 0;
        }

        public double ConvertUsingCustomIntervals(double value, List<IntervalClassItem> intervals)
        {
            foreach (var interval in intervals)
            {
                if (value >= interval.StartValue && value < interval.EndValue)
                    return interval.ClassValue;
            }
            return 0;
        }

        public async Task<(double min, double max)> GetRasterMinMaxAsync(string rasterPath)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    // 获取最小值
                    var minResult = await Geoprocessing.ExecuteToolAsync(
                        "management.GetRasterProperties",
                        Geoprocessing.MakeValueArray(rasterPath, "MINIMUM")
                    );

                    // 获取最大值
                    var maxResult = await Geoprocessing.ExecuteToolAsync(
                        "management.GetRasterProperties",
                        Geoprocessing.MakeValueArray(rasterPath, "MAXIMUM")
                    );

                    if (!minResult.IsFailed && !maxResult.IsFailed &&
                        minResult.Values.Count > 0 && maxResult.Values.Count > 0)
                    {
                        return (Convert.ToDouble(minResult.Values[0]), 
                                Convert.ToDouble(maxResult.Values[0]));
                    }

                    return (0, 1000);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"获取栅格统计失败: {ex.Message}");
                    return (0, 1000);
                }
            });
        }
    }
}