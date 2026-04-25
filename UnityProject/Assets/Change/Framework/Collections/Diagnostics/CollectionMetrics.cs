namespace Change.Framework.Collections
{
    public static class CollectionMetrics
    {
        public static int FastListGrowCount { get; private set; }
        public static int FastDictionaryGrowCount { get; private set; }
        public static int FastPriorityQueueGrowCount { get; private set; }

        public static void RecordFastListGrow()
        {
            FastListGrowCount++;
        }

        public static void RecordFastDictionaryGrow()
        {
            FastDictionaryGrowCount++;
        }

        public static void RecordFastPriorityQueueGrow()
        {
            FastPriorityQueueGrowCount++;
        }

        public static void Reset()
        {
            FastListGrowCount = 0;
            FastDictionaryGrowCount = 0;
            FastPriorityQueueGrowCount = 0;
        }
    }
}
