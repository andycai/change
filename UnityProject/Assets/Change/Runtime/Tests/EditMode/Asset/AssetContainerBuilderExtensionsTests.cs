using System;
using System.Reflection;
using NUnit.Framework;
using VContainer;
using YooAsset;
using Change.Runtime.Asset;

namespace Change.Runtime.Tests.EditMode.Asset
{
    /// <summary>
    /// Tests for <see cref="AssetContainerBuilderExtensions"/> verifying VContainer registration of <see cref="IAssetManager"/>.
    /// </summary>
    /// <remarks>
    /// <para>Uses reflection to create <see cref="ResourcePackage"/> instances,
    /// bypassing <c>YooAssets.Initialize()</c> which is not available in EditMode.</para>
    /// </remarks>
    [TestFixture]
    public sealed class AssetContainerBuilderExtensionsTests
    {
        private ResourcePackage _testPackage;

        /// <summary>
        /// Creates a ResourcePackage via reflection for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            var ctor = typeof(ResourcePackage).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(string) },
                null);

            Assert.IsNotNull(ctor, "Unable to find ResourcePackage constructor via reflection.");
            _testPackage = (ResourcePackage)ctor.Invoke(new object[] { $"TestPkg_{Guid.NewGuid():N}" });
        }

        /// <summary>
        /// Cleans up the test package after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _testPackage = null;
        }

        #region Parameter Validation

        /// <summary>
        /// Verifies that a null <see cref="IContainerBuilder"/> throws <see cref="ArgumentNullException"/>.
        /// </summary>
        [Test]
        public void RegisterAssetManager_WithNullBuilder_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                AssetContainerBuilderExtensions.RegisterAssetManager(null, _testPackage);
            });
        }

        /// <summary>
        /// Verifies that a null <see cref="ResourcePackage"/> throws <see cref="ArgumentNullException"/>.
        /// </summary>
        [Test]
        public void RegisterAssetManager_WithNullDefaultPackage_ThrowsArgumentNullException()
        {
            var builder = new ContainerBuilder();

            Assert.Throws<ArgumentNullException>(() =>
            {
                builder.RegisterAssetManager(null);
            });
        }

        #endregion

        #region Registration

        /// <summary>
        /// Verifies that <see cref="IAssetManager"/> can be resolved after registration
        /// and that the resolved instance is a <see cref="YooAssetManager"/>.
        /// </summary>
        [Test]
        public void RegisterAssetManager_Resolves_IAssetManager()
        {
            var builder = new ContainerBuilder();
            builder.RegisterAssetManager(_testPackage);
            var container = builder.Build();

            var manager = container.Resolve<IAssetManager>();

            Assert.IsNotNull(manager);
            Assert.IsInstanceOf<YooAssetManager>(manager);
        }

        /// <summary>
        /// Verifies that <see cref="IAssetManager"/> is registered as a singleton,
        /// so multiple resolves return the same instance.
        /// </summary>
        [Test]
        public void RegisterAssetManager_SingletonLifetime_ReturnsSameInstance()
        {
            var builder = new ContainerBuilder();
            builder.RegisterAssetManager(_testPackage);
            var container = builder.Build();

            var first = container.Resolve<IAssetManager>();
            var second = container.Resolve<IAssetManager>();

            Assert.AreSame(first, second);
        }

        #endregion
    }
}
