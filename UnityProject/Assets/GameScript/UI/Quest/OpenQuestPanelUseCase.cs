using Change.Framework.Cqrs;

namespace GameScript.UI.Quest
{
    public sealed class OpenQuestPanelUseCase : IOpenQuestPanelUseCase
    {
        private readonly ICqrsBus _bus;

        public OpenQuestPanelUseCase(ICqrsBus bus)
        {
            _bus = bus;
        }

        public QuestWindowViewModel Execute(in OpenQuestPanelRequest request)
        {
            var snapshot = _bus.Ask<GetQuestPanelQuery, QuestPanelSnapshot>(new GetQuestPanelQuery());
            return new QuestWindowViewModel(snapshot, request.ActiveTabIndex);
        }
    }
}
