using System;
using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public readonly struct WindowRequest : IEquatable<WindowRequest>
    {
        // Backward-compatible constructor
        public WindowRequest(WindowId id, WindowOpenOptions options)
            : this(id, options, null, null)
        {
        }

        // New constructor with Group and Context
        public WindowRequest(WindowId id, WindowOpenOptions options, string group, object context)
        {
            Id = id;
            Options = options;
            Group = group;
            Context = context;
        }

        public WindowId Id { get; }
        public WindowOpenOptions Options { get; }
        public string Group { get; }
        public object Context { get; }

        // Identity for cache/inflight dedup intentionally ignores layer/reuse flags.
        // Only Id + instance semantics participate in equality/hashing.
        public bool Equals(WindowRequest other)
        {
            return Id.Equals(other.Id)
                && Options.AllowMultipleInstances == other.Options.AllowMultipleInstances
                && Options.InstanceId == other.Options.InstanceId;
        }

        public override bool Equals(object obj)
        {
            return obj is WindowRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Id.GetHashCode();
                hash = (hash * 397) ^ (Options.AllowMultipleInstances ? 1 : 0);
                hash = (hash * 397) ^ Options.InstanceId;
                return hash;
            }
        }

        public static bool operator ==(WindowRequest left, WindowRequest right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WindowRequest left, WindowRequest right)
        {
            return !left.Equals(right);
        }
    }
}
