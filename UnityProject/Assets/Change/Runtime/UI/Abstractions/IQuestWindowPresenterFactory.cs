using Change.Framework.Application;
using FairyGUI;

namespace Change.Runtime.UI
{
    public interface IQuestWindowPresenterFactory
    {
        IPresenter Create(GComponent root);
    }
}
