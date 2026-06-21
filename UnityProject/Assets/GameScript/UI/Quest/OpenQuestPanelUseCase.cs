using Change.Framework.Cqrs;

namespace GameScript.UI.Quest
{
    public sealed class OpenQuestPanelUseCase : IOpenQuestPanelUseCase
    {
        private readonly ICqrsBus _bus;
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;

        public OpenQuestPanelUseCase(ICqrsBus bus, QuestSessionState state, QuestRewardWallet wallet)
        {
            _bus = bus;
            _state = state;
            _wallet = wallet;
        }

        public QuestWindowViewModel Execute(in OpenQuestPanelRequest request)
        {
            var snapshot = _bus.Ask<GetQuestPanelQuery, QuestPanelSnapshot>(
                new GetQuestPanelQuery(_state, _wallet));
            return new QuestWindowViewModel(snapshot, request.ActiveTabIndex);
        }
    }
}
