using System;
using System.Collections.Generic;

namespace GameScript.GasTemplate.Demo
{
    public sealed class DemoTemplateSource : ITemplateSource
    {
        public IEnumerable<KeyValuePair<string, Func<TemplateBuildContext, object>>> GetTemplates()
        {
            yield return new KeyValuePair<string, Func<TemplateBuildContext, object>>(
                "demo.single-cast-chain",
                _ => new DemoScenarioBuilder());
        }
    }
}
