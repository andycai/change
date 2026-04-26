namespace Change.Framework.Skill
{
    public enum SkillType { Active, Passive }
    public enum ActivationType { Manual, Auto, OnEvent }
    public enum SkillState { Ready, Casting, Executing, Cooldown }
    public enum ModifierPolarity { Buff, Debuff, Neutral }
    public enum ModifierStacking { Refresh, AddStack, Replace, Ignore }
    public enum TargetType { Self, Enemy, Ally, AllEnemies, AllAllies }
    public enum TriggerEventType
    {
        OnDealDamage, OnTakeDamage, OnHeal, OnKill, OnDeath, OnAttack,
        OnBuffApplied, OnBuffRemoved, OnDebuffApplied, OnDebuffRemoved, OnBuffStackChanged,
        OnSkillCast, OnSkillHit, OnSkillMiss, OnSkillCooldownEnd,
        OnBattleStart, OnTurnStart, OnTurnEnd, OnSpawn, OnHPThreshold
    }
    public enum TriggerScope { Self, Source, AllEnemies, AllAllies }
}
