namespace GameScript.GameFlow.Orchestration
{
    public sealed class GameFlowContext
    {
        public string PlayerId { get; set; }

        public string MatchId { get; set; }

        public bool IsBattleActive { get; set; }
    }
}
