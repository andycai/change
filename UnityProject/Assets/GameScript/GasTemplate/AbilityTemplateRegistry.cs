using System;
using System.Collections.Generic;

namespace GameScript.GasTemplate
{
    public sealed class AbilityTemplateRegistry
    {
        private readonly Dictionary<string, Func<TemplateBuildContext, object>> _factories =
            new Dictionary<string, Func<TemplateBuildContext, object>>(StringComparer.Ordinal);

        public AbilityTemplateRegistry()
        {
        }

        public AbilityTemplateRegistry(params ITemplateSource[] sources)
        {
            if (sources == null)
            {
                return;
            }

            for (var i = 0; i < sources.Length; i++)
            {
                Register(sources[i]);
            }
        }

        public void Register(ITemplateSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            foreach (var pair in source.GetTemplates())
            {
                Register(pair.Key, pair.Value);
            }
        }

        public void Register(string key, Func<TemplateBuildContext, object> factory)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Template key cannot be null or whitespace.", nameof(key));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (_factories.ContainsKey(key))
            {
                throw new InvalidOperationException($"Template key `{key}` is already registered.");
            }

            _factories.Add(key, factory);
        }

        public object Build(string key, TemplateBuildContext context)
        {
            if (!_factories.TryGetValue(key, out var factory))
            {
                throw new InvalidOperationException($"Template key `{key}` is not registered.");
            }

            return factory.Invoke(context);
        }
    }
}
