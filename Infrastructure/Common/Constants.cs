namespace HK_AREA_SEARCH.Common
{
    /// <summary>
    /// 系统常量
    /// </summary>
    public static class Constants
    {
        // 文件名常量
        public const string SUITABLE_AREA_FILENAME = "Suitable_Area.shp";
        public const string RESULT_FILENAME = "Result_Rating_Suitable_Area.shp";

        // 字段名常量
        public const string RATING_FIELD = "Rating";
        public const string VALUE_FIELD = "VALUE";
        public const string AREA_FIELD = "AREA_M2";

        // 分类常量
        public const int NUM_CLASSES = 10;
        public const string CLASSIFICATION_METHOD = "EQUAL_INTERVAL";

        // 临时文件夹
        public const string TEMP_FOLDER_NAME = "HK_SEARCH_TEMP";

        // 文件后缀
        public const string DISTANCE_SUFFIX = "_distance.tif";
        public const string RECLASS_SUFFIX = "_reclassification.tif";
        public const string NODATA_SUFFIX = "_reclassification_ND.tif";
        public const string RATING_SUFFIX = "_rating.tif";
    }
}