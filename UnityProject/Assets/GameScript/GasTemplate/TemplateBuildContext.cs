using System;

namespace GameScript.GasTemplate
{
    public readonly struct TemplateBuildContext
    {
        public TemplateBuildContext(IServiceProvider services = null)
        {
            Services = services;
        }

        public IServiceProvider Services { get; }
    }
}
