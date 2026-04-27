using System;

namespace Change.Framework.Gas
{
    public readonly struct GameplayTag : IEquatable<GameplayTag>, IComparable<GameplayTag>
    {
        public string Value { get; }
        public int Hash { get; }

        public GameplayTag(string value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Hash = Value.GetHashCode();
        }

        public bool Equals(GameplayTag other) => Hash == other.Hash && Value == other.Value;
        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
        public override int GetHashCode() => Hash;
        public override string ToString() => Value;
        public int CompareTo(GameplayTag other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

        public static bool operator ==(GameplayTag left, GameplayTag right) => left.Equals(right);
        public static bool operator !=(GameplayTag left, GameplayTag right) => !left.Equals(right);

        public static implicit operator GameplayTag(string value) => new GameplayTag(value);
    }
}
