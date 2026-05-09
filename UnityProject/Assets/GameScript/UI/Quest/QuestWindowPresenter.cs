using System;
using Change.Framework.Application;

namespace GameScript.UI.Quest
{
    public sealed class QuestWindowPresenter : IPresenter
    {
        private readonly IQuestWindowView _view;
        private readonly IOpenQuestPanelUseCase _openPanel;
        private int _activeTab;

        public QuestWindowPresenter(IQuestWindowView view, IOpenQuestPanelUseCase openPanel)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _openPanel = openPanel ?? throw new ArgumentNullException(nameof(openPanel));
        }

        public void SetActiveTab(int index)
        {
            _activeTab = index;
        }

        public void OnOpen()
        {
            var vm = _openPanel.Execute(new OpenQuestPanelRequest(_activeTab));
            _view.Apply(in vm);
        }

        public void OnClose()
        {
        }
    }
}
