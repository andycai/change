using System;
using Change.Framework.Application;
using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public sealed class NullWindowPresenterHost : IWindowPresenterHost
    {
        public static readonly NullWindowPresenterHost Instance = new NullWindowPresenterHost();

        private NullWindowPresenterHost()
        {
        }

        public void OnOpened(in WindowRequest request, IWindowView view, out IDisposable windowScope, out IPresenter presenter)
        {
            windowScope = null;
            presenter = null;
        }

        public void OnClosing(in WindowRequest request, IWindowView view, IPresenter presenter, IDisposable windowScope)
        {
        }
    }
}
