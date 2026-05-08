using System;
using System.Collections.Generic;

namespace GameScript.GasTemplate
{
    public interface ITemplateSource
    {
        IEnumerable<KeyValuePair<string, Func<TemplateBuildContext, object>>> GetTemplates();
    }
}
