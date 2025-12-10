using System;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using HK_AREA_SEARCH.Infrastructure.Services;

namespace HK_AREA_SEARCH.Business.Distance
{
    public class ZonalStatisticsService
    {
        private readonly TempFileManager _tempFileManager;

        public ZonalStatisticsService(TempFileManager tempFileManager)
        {
            _tempFileManager = tempFileManager ?? throw new ArgumentNullException(nameof(tempFileManager));
        }

        public async Task<string> ExecuteZonalStatsAsync(
            string zoneLayer,
            string zoneField,
            string valueRaster,
            string statisticType = "MEAN")
        {
            string statsTablePath = _tempFileManager.CreateTempFile($"ZonalStats_{Guid.NewGuid()}.dbf");

            System.Diagnostics.Debug.WriteLine($"ZonalStats 参数: zoneLayer={zoneLayer}, zoneField={zoneField}, valueRaster={valueRaster}, output={statsTablePath}");

            var parameters = Geoprocessing.MakeValueArray(
                zoneLayer,
                zoneField,
                valueRaster,
                statsTablePath,
                "DATA",
                statisticType
            );

            // ✅ 使用 GPExecuteToolFlags.None 阻止输出添加到地图
            var result = await Geoprocessing.ExecuteToolAsync(
                "sa.ZonalStatisticsAsTable", 
                parameters,
                null,
                null,
                null,
                GPExecuteToolFlags.None
            );

            if (result.IsFailed)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ZonalStatisticsAsTable 失败:");
                foreach (var msg in result.Messages)
                {
                    System.Diagnostics.Debug.WriteLine($"   {msg}");
                }
                throw new Exception($"分区统计失败: {string.Join("; ", result.ErrorMessages)}");
            }

            System.Diagnostics.Debug.WriteLine($"✅ ZonalStatisticsAsTable 成功: {statsTablePath}");
            return statsTablePath;
        }

        public async Task JoinFieldAsync(
            string targetLayer,
            string targetField,
            string joinTable,
            string joinField,
            string fieldsToJoin)
        {
            System.Diagnostics.Debug.WriteLine($"JoinField 参数:");
            System.Diagnostics.Debug.WriteLine($"  targetLayer={targetLayer}");
            System.Diagnostics.Debug.WriteLine($"  targetField={targetField}");
            System.Diagnostics.Debug.WriteLine($"  joinTable={joinTable}");
            System.Diagnostics.Debug.WriteLine($"  joinField={joinField}");
            System.Diagnostics.Debug.WriteLine($"  fieldsToJoin={fieldsToJoin}");

            var parameters = Geoprocessing.MakeValueArray(
                targetLayer,
                targetField,
                joinTable,
                joinField,
                fieldsToJoin
            );

            // ✅ 使用 GPExecuteToolFlags.None 阻止输出添加到地图
            var result = await Geoprocessing.ExecuteToolAsync(
                "management.JoinField", 
                parameters,
                null,
                null,
                null,
                GPExecuteToolFlags.None
            );

            if (result.IsFailed)
            {
                System.Diagnostics.Debug.WriteLine($"❌ JoinField 失败详情:");
                foreach (var msg in result.Messages)
                {
                    System.Diagnostics.Debug.WriteLine($"   {msg}");
                }
                throw new Exception($"字段连接失败: {string.Join("; ", result.ErrorMessages)}");
            }

            System.Diagnostics.Debug.WriteLine($"✅ JoinField 成功");
        }
    }
}