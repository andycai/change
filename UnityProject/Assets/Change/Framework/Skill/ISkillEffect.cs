namespace Change.Framework.Skill
{
    public interface ISkillEffect
    {
        void Execute(IAbilitySystem source, IAbilitySystem target);
    }
}
