using System;
using Change.Framework.Application;

namespace GameScript.UI.Inventory
{
    public sealed class InventoryWindowPresenter : IPresenter
    {
        private readonly IInventoryWindowView _view;
        private readonly IOpenInventoryUseCase _useCase;
        private int _playerId;

        public InventoryWindowPresenter(IInventoryWindowView view, IOpenInventoryUseCase useCase)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
        }

        /// <summary>
        /// P3 修复：将业务数据与构造函数分离，支持 Presenter 复用。
        /// </summary>
        public void Setup(int playerId)
        {
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
            // 以后可以在这里进行取消订阅等清理工作
        }
    }
}
