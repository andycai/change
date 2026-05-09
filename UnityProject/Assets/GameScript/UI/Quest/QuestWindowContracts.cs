using Change.Framework.Application;

namespace GameScript.UI.Quest
{
    public readonly struct OpenQuestPanelRequest
    {
        public OpenQuestPanelRequest(int activeTabIndex)
        {
            ActiveTabIndex = activeTabIndex;
        }

        public int ActiveTabIndex { get; }
    }

    public interface IQuestWindowView
    {
        void Apply(in QuestWindowViewModel model);
    }

    public interface IOpenQuestPanelUseCase : IUseCase<OpenQuestPanelRequest, QuestWindowViewModel>
    {
    }

    public readonly struct QuestWindowViewModel
    {
        public QuestWindowViewModel(QuestPanelSnapshot snapshot, int activeTabIndex)
        {
            Snapshot = snapshot;
            ActiveTabIndex = activeTabIndex;
        }

        public QuestPanelSnapshot Snapshot { get; }
        public int ActiveTabIndex { get; }
    }
}
