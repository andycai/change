namespace GameScript.UI.Inventory
{
    public readonly struct InventoryWindowViewModel
    {
        public InventoryWindowViewModel(int itemCount, int gold)
        {
            ItemCount = itemCount;
            Gold = gold;
        }

        public int ItemCount { get; }
        public int Gold { get; }
    }
}
