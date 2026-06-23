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

        // Single-instance windows: only Id matters.
        // Multi-instance windows: Id + InstanceId + Context.
        // Group does NOT participate — it drives mutual exclusion, not identity.
        public bool Equals(WindowRequest other)
        {
            // Id must match in all cases
            if (!Id.Equals(other.Id))
                return false;

            // AllowMultipleInstances must match — single/multi-instance
            // requests cannot be equal. This preserves Equals symmetry and
            // keeps Equals/GetHashCode consistent.
            if (Options.AllowMultipleInstances != other.Options.AllowMultipleInstances)
                return false;

            // Single-instance: Id alone is sufficient
            if (!Options.AllowMultipleInstances)
                return true;

            // Multi-instance: compare InstanceId and Context
            if (Options.InstanceId != other.Options.InstanceId)
                return false;

            if (Context == null && other.Context == null)
                return true;

            if (Context == null || other.Context == null)
                return false;

            return Context.Equals(other.Context);
        }

        public override bool Equals(object obj)
        {
            return obj is WindowRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // Single-instance: only Id contributes
                if (!Options.AllowMultipleInstances)
                {
                    return Id.GetHashCode();
                }

                // Multi-instance: Id + InstanceId + Context (when present)
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ Options.InstanceId;

                if (Context != null)
                {
                    hash = (hash * 397) ^ Context.GetHashCode();
                }

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
