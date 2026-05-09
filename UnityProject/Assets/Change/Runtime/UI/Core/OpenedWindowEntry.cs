using System;
using Change.Framework.Application;

namespace Change.Runtime.UI
{
    internal sealed class OpenedWindowEntry
    {
        public OpenedWindowEntry(IWindowView view, IPresenter presenter, IDisposable windowScope)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            Presenter = presenter;
            WindowScope = windowScope;
        }

        public IWindowView View { get; }
        public IPresenter Presenter { get; }
        public IDisposable WindowScope { get; }
    }
}
