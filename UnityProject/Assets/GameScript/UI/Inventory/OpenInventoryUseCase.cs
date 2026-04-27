using Change.Framework.Cqrs;

namespace GameScript.UI.Inventory
{
    public sealed class OpenInventoryUseCase : IOpenInventoryUseCase
    {
        private readonly ICqrsBus _bus;

        public OpenInventoryUseCase(ICqrsBus bus)
        {
            _bus = bus;
        }

        public InventoryWindowViewModel Execute(in OpenInventoryRequest request)
        {
            var query = new GetInventorySummaryQuery(request.PlayerId);
            var result = _bus.Query<GetInventorySummaryQuery, InventorySummaryResult>(in query);
            return new InventoryWindowViewModel(result.ItemCount, result.Gold);
        }
    }

    public readonly struct GetInventorySummaryQuery : IQuery<InventorySummaryResult>
    {
        public GetInventorySummaryQuery(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }
    }

    public readonly struct InventorySummaryResult
    {
        public InventorySummaryResult(int itemCount, int gold)
        {
            ItemCount = itemCount;
            Gold = gold;
        }

        public int ItemCount { get; }
        public int Gold { get; }
    }
}
