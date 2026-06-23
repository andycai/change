using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using YooAsset;
using Change.Runtime.Asset;

namespace Change.Runtime.Tests.EditMode.Asset
{
    /// <summary>
    /// YooAssetManager 的集成测试，验证完整的资源加载和实例化流程。
    /// </summary>
    /// <remarks>
    /// <para>这些测试需要真实的 YooAsset 环境和测试资源才能运行。当前所有测试均标记为
    /// Ignore，在配置好测试资源后取消 Ignore 即可运行。</para>
    ///
    /// <para>运行集成测试的前置条件：</para>
    /// <list type="number">
    /// <item>
    /// <description>YooAsset 环境初始化 — 需要在 PlayMode 中调用
    /// <c>YooAssets.Initialize()</c> 并创建名为 <c>"DefaultPackage"</c> 的资源包。
    /// 由于 <c>YooAssets.Initialize()</c> 使用 <c>DontDestroyOnLoad</c>，在 EditMode
    /// 中无法直接运行，因此本测试类在 Setup 中检测环境未就绪时自动 Ignore。</description>
    /// </item>
    /// <item>
    /// <description>YooAsset 测试资源包 — 需要配置包含以下类型测试资源的资源包：
    /// <c>Texture2D</c>（用于纹理加载测试）、<c>GameObject</c>（用于预制体实例化测试）。</description>
    /// </item>
    /// <item>
    /// <description>测试资源准备 — 在 YooAsset 资源收集配置中添加测试资源，确保它们
    /// 可以通过地址（location）加载。</description>
    /// </item>
    /// </list>
    ///
    /// <para>取消 Ignore 的方法：</para>
    /// <para>1. 在 PlayMode 测试中运行（推荐），或确保 YooAsset 的 EditMode 初始化方案就绪。</para>
    /// <para>2. 配置好测试资源包后，移除各测试方法的 <c>[Ignore]</c> 特性。</para>
    /// <para>3. 更新各测试中的 location 字符串为实际配置的资源地址。</para>
    ///
    /// <para>测试覆盖的场景：</para>
    /// <list type="bullet">
    /// <item><description>异步加载 Texture2D 资源</description></item>
    /// <item><description>异步加载并实例化 GameObject 预制体</description></item>
    /// <item><description>加载进度回调机制</description></item>
    /// <item><description>取消令牌（CancellationToken）支持</description></item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public sealed class AssetManagerIntegrationTests
    {
        private IAssetManager _manager;

        /// <summary>
        /// 设置测试环境：尝试获取 YooAsset 的默认资源包。
        /// 如果 YooAsset 环境未初始化或默认包不存在，则跳过所有测试。
        /// </summary>
        /// <remarks>
        /// <para>在 EditMode 中，由于 <c>YooAssets.Initialize()</c> 使用
        /// <c>DontDestroyOnLoad</c>，通常无法完成初始化，因此 Setup 会检测到
        /// 环境未就绪并自动通过 <c>Assert.Ignore()</c> 跳过测试。</para>
        /// <para>在 PlayMode 中，如果 YooAssets 已正确初始化且配置了名为
        /// <c>"DefaultPackage"</c> 的资源包，Setup 将成功创建 <see cref="YooAssetManager"/>
        /// 实例供测试使用。</para>
        /// </remarks>
        [SetUp]
        public void SetUp()
        {
            if (!YooAssets.Initialized)
            {
                Assert.Ignore(
                    "YooAssets is not initialized. " +
                    "Initialize YooAssets with a configured resource package before running integration tests. " +
                    "These tests require a PlayMode environment with real test assets. " +
                    "See the class remarks for detailed setup instructions.");
            }

            var package = YooAssets.TryGetPackage("DefaultPackage");
            if (package == null)
            {
                Assert.Ignore(
                    "DefaultPackage not found. " +
                    "Create and configure a YooAsset package named 'DefaultPackage' with test assets " +
                    "(Texture2D, Prefab, etc.) before running integration tests. " +
                    "See the class remarks for detailed setup instructions.");
            }

            _manager = new YooAssetManager(package);
        }

        /// <summary>
        /// 每个测试后清理资源
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _manager = null;
        }

        #region Integration Tests

        /// <summary>
        /// 验证通过地址异步加载 Texture2D 资源时，能够成功获取非空的纹理对象。
        /// </summary>
        /// <remarks>
        /// <para>前置条件：YooAsset 测试资源包中包含一个可通过地址
        /// <c>"TestTexture"</c> 加载的 Texture2D 资源。</para>
        /// <para>预期行为：</para>
        /// <list type="number">
        /// <item><description><c>LoadAsync&lt;Texture2D&gt;</c> 返回有效的 <see cref="AssetLease{T}"/>。</description></item>
        /// <item><description>句柄中的 <c>Asset</c> 属性不为 null。</description></item>
        /// <item><description>调用 <c>Dispose()</c> 后资源被正确释放。</description></item>
        /// </list>
        /// </remarks>
        [Test]
        [Ignore("Requires YooAsset test resource package configured with a Texture2D asset at location 'TestTexture'.")]
        public async UniTask LoadAsync_Texture2D_LoadsSuccessfully()
        {
            // Arrange
            const string location = "TestTexture";

            // Act
            AssetLease<Texture2D> lease;
            try
            {
                lease = await _manager.LoadAsync<Texture2D>(location);
            }
            catch (InvalidOperationException ex)
            {
                Assert.Fail($"Failed to load Texture2D at '{location}': {ex.Message}");
                return;
            }

            // Assert
            Assert.IsNotNull(lease, "AssetLease should not be null.");
            Assert.IsNotNull(lease.Asset, "Loaded Texture2D should not be null.");
            Assert.IsInstanceOf<Texture2D>(lease.Asset, "Asset should be of type Texture2D.");

            // Cleanup
            lease.Dispose();
        }

        /// <summary>
        /// 验证通过地址异步加载并实例化 GameObject 预制体时，能够成功获取有效的 GameObject 实例。
        /// </summary>
        /// <remarks>
        /// <para>前置条件：YooAsset 测试资源包中包含一个可通过地址
        /// <c>"TestPrefab"</c> 加载的 GameObject 预制体。</para>
        /// <para>预期行为：</para>
        /// <list type="number">
        /// <item><description><c>LoadAndInstantiateAsync</c> 返回有效的 <see cref="GameObjectLease"/>。</description></item>
        /// <item><description>句柄中的 <c>Instance</c> 属性不为 null。</description></item>
        /// <item><description>实例是原始预制体的克隆，名称以 "(Clone)" 结尾。</description></item>
        /// <item><description>调用 <c>Dispose()</c> 后实例被销毁且资源被释放。</description></item>
        /// </list>
        /// </remarks>
        [Test]
        [Ignore("Requires YooAsset test resource package configured with a GameObject prefab at location 'TestPrefab'.")]
        public async UniTask LoadAndInstantiateAsync_GameObject_InstantiatesSuccessfully()
        {
            // Arrange
            const string location = "TestPrefab";

            // Act
            GameObjectLease lease;
            try
            {
                lease = await _manager.LoadAndInstantiateAsync(location);
            }
            catch (InvalidOperationException ex)
            {
                Assert.Fail($"Failed to load and instantiate GameObject at '{location}': {ex.Message}");
                return;
            }

            // Assert
            Assert.IsNotNull(lease, "GameObjectLease should not be null.");
            Assert.IsNotNull(lease.Instance, "Instantiated GameObject should not be null.");
            Assert.IsTrue(lease.Instance.name.Contains("(Clone)"),
                "Instantiated GameObject name should contain '(Clone)' to indicate it was cloned from a prefab.");

            // Cleanup
            lease.Dispose();
        }

        /// <summary>
        /// 验证异步加载资源时，进度回调被正确调用并报告有效的进度值。
        /// </summary>
        /// <remarks>
        /// <para>前置条件：YooAsset 测试资源包中包含一个可通过地址
        /// <c>"TestTexture"</c> 加载的 Texture2D 资源。</para>
        /// <para>预期行为：</para>
        /// <list type="number">
        /// <item><description>进度回调在加载过程中被调用至少一次。</description></item>
        /// <item><description>每次报告的进度值在 [0.0, 1.0] 范围内。</description></item>
        /// <item><description>加载完成时最后报告的进度值为 1.0。</description></item>
        /// </list>
        /// </remarks>
        [Test]
        [Ignore("Requires YooAsset test resource package configured with a Texture2D asset at location 'TestTexture'.")]
        public async UniTask LoadAsync_WithProgress_ReportsProgress()
        {
            // Arrange
            const string location = "TestTexture";
            var progressValues = new List<float>();
            var progress = new Progress<float>(value => progressValues.Add(value));

            // Act
            AssetLease<Texture2D> lease;
            try
            {
                lease = await _manager.LoadAsync<Texture2D>(location, progress);
            }
            catch (InvalidOperationException ex)
            {
                Assert.Fail($"Failed to load Texture2D at '{location}': {ex.Message}");
                return;
            }

            // Assert
            Assert.IsNotNull(lease, "AssetLease should not be null.");
            Assert.IsNotNull(lease.Asset, "Loaded Texture2D should not be null.");

            // Verify progress was reported
            Assert.IsTrue(progressValues.Count > 0,
                "Progress callback should have been invoked at least once.");

            // Verify all progress values are within [0.0, 1.0]
            foreach (var value in progressValues)
            {
                Assert.IsTrue(value >= 0.0f && value <= 1.0f,
                    $"Progress value {value:F3} should be in range [0.0, 1.0].");
            }

            // Verify final progress is 1.0
            var lastValue = progressValues[progressValues.Count - 1];
            Assert.AreEqual(1.0f, lastValue, 0.001f,
                "Final progress value should be 1.0 (fully loaded).");

            // Cleanup
            lease.Dispose();
        }

        /// <summary>
        /// 验证使用预取消的 CancellationToken 时，异步加载操作正确抛出
        /// <see cref="OperationCanceledException"/>。
        /// </summary>
        /// <remarks>
        /// <para>前置条件：YooAsset 测试资源包中包含一个可通过地址
        /// <c>"TestTexture"</c> 加载的 Texture2D 资源。</para>
        /// <para>预期行为：</para>
        /// <list type="number">
        /// <item><description>传入已取消的 <see cref="CancellationToken"/> 后，
        /// <c>LoadAsync</c> 抛出 <see cref="OperationCanceledException"/>。</description></item>
        /// <item><description>资源在取消后被正确释放，不泄漏句柄。</description></item>
        /// </list>
        /// <para>注意：YooAsset 的取消行为依赖于其内部 handle 对
        /// <c>AttachExternalCancellation</c> 的响应。在真正可加载资源的 PlayMode 环境中，
        /// 也可以测试"加载中途取消"的场景。</para>
        /// </remarks>
        [Test]
        [Ignore("Requires YooAsset test resource package configured with a Texture2D asset at location 'TestTexture'.")]
        public async UniTask LoadAsync_WithCancellation_CancelsSuccessfully()
        {
            // Arrange
            const string location = "TestTexture";
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel the token

            // Act & Assert — the pre-cancelled token should cause the operation to throw.
            try
            {
                await _manager.LoadAsync<Texture2D>(location, cancellationToken: cts.Token);
                Assert.Fail("Expected OperationCanceledException was not thrown.");
            }
            catch (OperationCanceledException)
            {
                // Expected — the pre-cancelled token caused the operation to cancel.
                Assert.Pass("Operation was correctly cancelled via CancellationToken.");
            }
        }

        #endregion
    }
}
