using System;
using Change.Framework.Collections;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// In-memory context-id to runtime provider.
    /// </summary>
    public sealed class CqrsContextRuntimeProvider : ICqrsRuntimeProvider
    {
        private readonly FastDictionary<string, ICqrsRuntime> _runtimes = new();

        public void Register(string contextId, ICqrsRuntime runtime)
        {
            if (string.IsNullOrWhiteSpace(contextId))
            {
                throw new ArgumentException("Context id must not be null or whitespace.", nameof(contextId));
            }

            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            if (!_runtimes.TryAdd(contextId, runtime))
            {
                throw new DuplicateRegistrationException($"Runtime already registered for context: {contextId}");
            }
        }

        public ICqrsRuntime Get(string contextId)
        {
            if (string.IsNullOrWhiteSpace(contextId))
            {
                throw new ArgumentException("Context id must not be null or whitespace.", nameof(contextId));
            }

            if (!_runtimes.TryGetValue(contextId, out var runtime))
            {
                throw new HandlerNotRegisteredException($"Runtime not registered for context: {contextId}");
            }

            return runtime;
        }
    }
}
