using System;

namespace Change.Framework.UI
{
    public readonly struct WindowId : IEquatable<WindowId>
    {
        private readonly string _value;

        public WindowId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("WindowId cannot be null or whitespace.", nameof(value));
            }

            _value = value;
        }

        public string Value => _value;

        public bool Equals(WindowId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WindowId other && Equals(other);
        public override int GetHashCode() => Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        public override string ToString() => Value;

        public static bool operator ==(WindowId left, WindowId right) => left.Equals(right);
        public static bool operator !=(WindowId left, WindowId right) => !left.Equals(right);
    }
}
