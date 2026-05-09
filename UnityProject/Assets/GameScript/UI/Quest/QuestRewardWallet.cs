namespace GameScript.UI.Quest
{
    public sealed class QuestRewardWallet
    {
        public int Gold { get; private set; }

        public void AddGold(int amount)
        {
            if (amount < 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            Gold += amount;
        }
    }
}
