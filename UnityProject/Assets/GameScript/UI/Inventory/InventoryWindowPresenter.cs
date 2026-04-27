using System;
using Change.Framework.Application;

namespace GameScript.UI.Inventory
{
    public sealed class InventoryWindowPresenter : IPresenter
    {
        private readonly IInventoryWindowView _view;
        private readonly IOpenInventoryUseCase _useCase;
        private readonly int _playerId;

        public InventoryWindowPresenter(IInventoryWindowView view, IOpenInventoryUseCase useCase, int playerId)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
            _playerId = playerId;
        }

        public void OnOpen()
        {
            var request = new OpenInventoryRequest(_playerId);
            var model = _useCase.Execute(in request);
            _view.Apply(in model);
        }

        public void OnClose()
        {
            // No presenter-level state to clean up in MVP. Reserved for future
            // event unsubscribe, cached data clearing, or transition animations.
        }
    }
}
