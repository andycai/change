using System;
using Change.Framework.Application;
using Change.Framework.UI;

namespace Change.Runtime.UI
{
    /// <summary>
    /// Optional hook for creating presenter (and optional per-window scope) when a window opens.
    /// Presenter <see cref="IPresenter.OnOpen"/> / <see cref="IPresenter.OnClose"/> are invoked by <see cref="WindowManager"/>.
    /// </summary>
    public interface IWindowPresenterHost
    {
        void OnOpened(in WindowRequest request, IWindowView view, out IDisposable windowScope, out IPresenter presenter);

        void OnClosing(in WindowRequest request, IWindowView view, IPresenter presenter, IDisposable windowScope);
    }
}
