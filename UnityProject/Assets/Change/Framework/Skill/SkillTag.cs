using System;

namespace Change.Framework.Skill
{
    public readonly struct SkillTag : IEquatable<SkillTag>, IComparable<SkillTag>
    {
        public string Value { get; }

        public SkillTag(string value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool Equals(SkillTag other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SkillTag other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;
        public int CompareTo(SkillTag other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

        public static bool operator ==(SkillTag left, SkillTag right) => left.Equals(right);
        public static bool operator !=(SkillTag left, SkillTag right) => !left.Equals(right);

        public static implicit operator SkillTag(string value) => new SkillTag(value);
    }
}
