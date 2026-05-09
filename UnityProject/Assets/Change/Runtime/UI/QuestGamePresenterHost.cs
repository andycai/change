using System;
using Change.Framework.Application;
using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public sealed class QuestGamePresenterHost : IWindowPresenterHost
    {
        private readonly IQuestWindowPresenterFactory _factory;

        public QuestGamePresenterHost(IQuestWindowPresenterFactory factory)
        {
            _factory = factory;
        }

        public void OnOpened(in WindowRequest request, IWindowView view, out IDisposable windowScope, out IPresenter presenter)
        {
            if (view is FairyGuiWindowView fairy && request.Id == WindowIds.Quest)
            {
                presenter = _factory.Create(fairy.Root);
                windowScope = null;
                return;
            }

            throw new InvalidOperationException($"Unsupported window `{request.Id.Value}` for QuestGamePresenterHost.");
        }

        public void OnClosing(in WindowRequest request, IWindowView view, IPresenter presenter, IDisposable windowScope)
        {
            windowScope?.Dispose();
        }
    }
}
