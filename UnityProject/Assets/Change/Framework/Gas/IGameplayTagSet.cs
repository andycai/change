namespace Change.Framework.Gas
{
    public interface IGameplayTagSet
    {
        void AddTag(GameplayTag tag);
        void RemoveTag(GameplayTag tag);
        bool HasTag(GameplayTag tag);
        int GetTagCount(GameplayTag tag);
    }
}
