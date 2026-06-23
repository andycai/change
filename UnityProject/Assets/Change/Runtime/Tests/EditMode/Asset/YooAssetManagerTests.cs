using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using YooAsset;
using Change.Runtime.Asset;

namespace Change.Runtime.Tests.EditMode.Asset
{
    /// <summary>
    /// YooAssetManager 的基本契约测试，验证参数验证、异常处理和取消令牌支持
    /// </summary>
    /// <remarks>
    /// <para>由于 YooAsset 的 Mock 复杂（需要模拟 AssetHandle、ResourcePackage 等），
    /// 本测试类只编写基本的契约测试。真实的资源加载测试将在集成测试中进行。</para>
    /// <para>使用反射创建 ResourcePackage 实例以绕过 YooAssets 的 EditMode 限制
    /// （YooAssets.Initialize() 使用 DontDestroyOnLoad 在编辑模式下不可用）。</para>
    /// </remarks>
    [TestFixture]
    public sealed class YooAssetManagerTests
    {
        private ResourcePackage _testPackage;

        /// <summary>
        /// 使用反射创建 ResourcePackage 实例，绕过 YooAssets.Initialize() 的 EditMode 限制
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
            _testPackage = (ResourcePackage)ctor.Invoke(new object[] { $"ReflectionPkg_{Guid.NewGuid():N}" });
        }

        /// <summary>
        /// 每个测试后清理
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _testPackage = null;
        }

        #region Constructor Tests

        /// <summary>
        /// 验证传入 null ResourcePackage 时构造函数抛出 ArgumentNullException
        /// </summary>
        [Test]
        public void Constructor_WithNullPackage_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = new YooAssetManager(null);
            });
        }

        /// <summary>
        /// 验证传入有效的 ResourcePackage 时构造函数正常创建实例
        /// </summary>
        [Test]
        public void Constructor_WithValidPackage_Succeeds()
        {
            var manager = new YooAssetManager(_testPackage);

            Assert.IsNotNull(manager);
            Assert.IsNotNull(manager.DefaultPackage);
            Assert.AreSame(_testPackage, manager.DefaultPackage);
        }

        #endregion

        #region LoadAsync Tests

        /// <summary>
        /// 验证传入 null location 时 LoadAsync 返回的 UniTask 包含 ArgumentException
        /// </summary>
        [Test]
        public void LoadAsync_WithNullLocation_ThrowsException()
        {
            var manager = new YooAssetManager(_testPackage);

            try
            {
                manager.LoadAsync<UnityEngine.Object>(null)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (ArgumentException)
            {
                // Expected — location validation occurs before async operations begin.
            }
        }

        /// <summary>
        /// 验证传入空白 location 时 LoadAsync 返回的 UniTask 包含 ArgumentException
        /// </summary>
        [Test]
        public void LoadAsync_WithEmptyLocation_ThrowsException()
        {
            var manager = new YooAssetManager(_testPackage);

            try
            {
                manager.LoadAsync<UnityEngine.Object>(string.Empty)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (ArgumentException)
            {
                // Expected — empty location triggers argument validation.
            }
        }

        /// <summary>
        /// 验证传入空白 location 时 LoadAsync 返回的 UniTask 包含 ArgumentException
        /// </summary>
        [Test]
        public void LoadAsync_WithWhitespaceLocation_ThrowsException()
        {
            var manager = new YooAssetManager(_testPackage);

            try
            {
                manager.LoadAsync<UnityEngine.Object>("   ")
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (ArgumentException)
            {
                // Expected — whitespace-only location triggers argument validation.
            }
        }

        /// <summary>
        /// 验证使用预取消的 CancellationToken 时操作抛出 OperationCanceledException
        /// </summary>
        /// <remarks>
        /// <para>此测试需要 YooAsset 包完成初始化后才能调用 LoadAssetAsync 创建 AssetHandle，
        /// 进而通过 AttachExternalCancellation 触发取消。由于 YooAssets.Initialize() 使用
        /// DontDestroyOnLoad 在 EditMode 中不可用，此测试标记为 Ignore。
        /// 取消令牌支持的完整验证在集成测试（PlayMode）中进行。</para>
        /// </remarks>
        [Test]
        [Ignore("Requires initialized YooAsset package; YooAssets.Initialize() uses DontDestroyOnLoad (PlayMode-only)")]
        public void LoadAsync_WithCancellation_ThrowsOperationCanceledException()
        {
            var manager = new YooAssetManager(_testPackage);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                manager.LoadAsync<UnityEngine.Object>(
                    "test_location",
                    cancellationToken: cts.Token)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                Assert.Fail("Expected OperationCanceledException was not thrown.");
            }
            catch (OperationCanceledException)
            {
                // Expected — the pre-cancelled token caused the operation to cancel.
            }
        }

        /// <summary>
        /// 验证 LoadAsync 的进度回调机制
        /// </summary>
        /// <remarks>
        /// <para>此测试需要真实的 YooAsset 环境和可加载的资源包，在编辑模式下无法可靠运行，
        /// 因此标记为 Ignore。进度回调的完整验证在集成测试中进行。</para>
        /// </remarks>
        [Test]
        [Ignore("Requires initialized YooAsset environment with loadable assets; use integration tests instead")]
        public void LoadAsync_WithProgress_CallsProgressReport()
        {
            var manager = new YooAssetManager(_testPackage);
            var progressValues = new System.Collections.Generic.List<float>();
            var progress = new Progress<float>(value => progressValues.Add(value));

            // 此测试在真实 YooAsset 环境中应验证:
            // 1. 进度回调被调用
            // 2. 进度值在 [0.0, 1.0] 范围内
            // 3. 最后报告 1.0
            Assert.Ignore("Requires initialized YooAsset environment with loadable assets.");
        }

        #endregion
    }
}
