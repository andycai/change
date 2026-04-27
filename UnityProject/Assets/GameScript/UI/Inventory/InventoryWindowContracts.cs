using Change.Framework.Application;

namespace GameScript.UI.Inventory
{
    public readonly struct OpenInventoryRequest
    {
        public OpenInventoryRequest(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }
    }

    public interface IInventoryWindowView
    {
        void Apply(in InventoryWindowViewModel model);
    }

    public interface IOpenInventoryUseCase : IUseCase<OpenInventoryRequest, InventoryWindowViewModel>
    {
    }
}
