using VContainer;

namespace Change.Runtime.Composition
{
    public interface IHotfixGameInstaller
    {
        void Install(IContainerBuilder builder);
    }
}
