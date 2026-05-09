using Change.Framework.Application;
using Change.Runtime.UI;
using FairyGUI;

namespace GameScript.UI.Quest
{
    public sealed class QuestWindowPresenterFactory : IQuestWindowPresenterFactory
    {
        private readonly IOpenQuestPanelUseCase _useCase;

        public QuestWindowPresenterFactory(IOpenQuestPanelUseCase useCase)
        {
            _useCase = useCase;
        }

        public IPresenter Create(GComponent root)
        {
            return new QuestWindowPresenter(new QuestFairyGuiView(root), _useCase);
        }
    }
}
