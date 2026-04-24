namespace Fun.Framework.Collections
{
    public static class CollectionMetrics
    {
        public static int FastListGrowCount { get; private set; }
        public static int FastDictionaryGrowCount { get; private set; }

        public static void RecordFastListGrow()
        {
            FastListGrowCount++;
        }

        public static void RecordFastDictionaryGrow()
        {
            FastDictionaryGrowCount++;
        }

        public static void Reset()
        {
            FastListGrowCount = 0;
            FastDictionaryGrowCount = 0;
        }
    }
}
