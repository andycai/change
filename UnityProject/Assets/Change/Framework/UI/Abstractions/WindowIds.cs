namespace Change.Framework.UI
{
    /// <summary>
    /// 预定义的窗口 ID 常量。
    /// P2 修复：提供集中管理和类型安全的 ID 定义。
    /// </summary>
    public static class WindowIds
    {
        public static readonly WindowId Inventory = new("Inventory");
        public static readonly WindowId Loading = new("Loading");
        
        // 可以在此处添加更多系统级窗口 ID
    }
}
