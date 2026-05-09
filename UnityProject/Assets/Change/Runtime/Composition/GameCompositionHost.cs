using System;
using VContainer.Unity;

namespace Change.Runtime.Composition
{
    public sealed class GameCompositionHost
    {
        private readonly LifetimeScope _engine;

        public GameCompositionHost(LifetimeScope engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public LifetimeScope CreateGameRoot(IHotfixGameInstaller installer)
        {
            if (installer == null) throw new ArgumentNullException(nameof(installer));
            var child = _engine.CreateChild(builder => installer.Install(builder));
            var parentRef = child.parentReference;
            parentRef.Object = _engine;
            child.parentReference = parentRef;
            if (child.Container == null)
            {
                child.Build();
            }

            return child;
        }
    }
}
