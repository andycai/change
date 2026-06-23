using System;
using VContainer;
using YooAsset;

namespace Change.Runtime.Asset
{
    /// <summary>
    /// Provides extension methods for registering asset management services with a VContainer container builder.
    /// </summary>
    public static class AssetContainerBuilderExtensions
    {
        /// <summary>
        /// Registers <see cref="IAssetManager"/> with the VContainer container builder,
        /// backed by <see cref="YooAssetManager"/> using the specified default resource package.
        /// </summary>
        /// <remarks>
        /// <para>The manager is registered as a singleton, ensuring a single shared instance
        /// across the entire application lifetime.</para>
        /// <para>The <paramref name="defaultPackage"/> is used for all asset loading operations
        /// that do not explicitly specify a package name.</para>
        /// <para>Usage in a <c>LifetimeScope.Configure</c> override:</para>
        /// <code>
        /// builder.RegisterAssetManager(defaultPackage);
        /// </code>
        /// </remarks>
        /// <param name="builder">The VContainer container builder to register with.</param>
        /// <param name="defaultPackage">The default YooAsset resource package for asset loading operations.</param>
        /// <returns>The registration builder for further configuration.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> or <paramref name="defaultPackage"/> is <c>null</c>.
        /// </exception>
        public static RegistrationBuilder RegisterAssetManager(
            this IContainerBuilder builder,
            ResourcePackage defaultPackage)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (defaultPackage == null)
            {
                throw new ArgumentNullException(nameof(defaultPackage));
            }

            return builder.Register<IAssetManager>(
                _ => new YooAssetManager(defaultPackage),
                Lifetime.Singleton);
        }
    }
}
