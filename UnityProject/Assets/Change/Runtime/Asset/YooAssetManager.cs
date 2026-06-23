using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace Change.Runtime.Asset
{
    /// <summary>
    /// YooAsset 实现的资源管理器，封装异步加载与实例化操作
    /// </summary>
    /// <remarks>
    /// <para>本类不负责 YooAsset 的初始化，仅封装资源加载逻辑。</para>
    /// <para>所有公共方法均为异步方法，支持进度报告和取消操作。</para>
    /// <para>通过 <see cref="AssetLease{T}"/> 和 <see cref="GameObjectLease"/> 管理资源生命周期。</para>
    /// </remarks>
    public sealed class YooAssetManager : IAssetManager
    {
        private readonly ResourcePackage _defaultPackage;

        /// <summary>
        /// 构造 YooAssetManager 实例
        /// </summary>
        /// <param name="defaultPackage">默认的资源包，用于无包名重载的加载操作</param>
        /// <exception cref="ArgumentNullException">当 defaultPackage 为 null 时抛出</exception>
        public YooAssetManager(ResourcePackage defaultPackage)
        {
            _defaultPackage = defaultPackage ?? throw new ArgumentNullException(nameof(defaultPackage));
        }

        /// <summary>
        /// 获取默认资源包（用于单元测试访问）
        /// </summary>
        internal ResourcePackage DefaultPackage => _defaultPackage;

        #region IAssetManager — LoadAsync

        /// <inheritdoc />
        public UniTask<AssetLease<T>> LoadAsync<T>(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            return LoadAsyncInternal<T>(_defaultPackage, location, progress, cancellationToken);
        }

        /// <inheritdoc />
        public UniTask<AssetLease<T>> LoadAsync<T>(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("Package name is required.", nameof(packageName));
            }

            var package = YooAssets.GetPackage(packageName);
            if (package == null)
            {
                throw new InvalidOperationException(
                    $"YooAsset package not found: '{packageName}'. Ensure the package is initialized before loading assets.");
            }

            return LoadAsyncInternal<T>(package, location, progress, cancellationToken);
        }

        #endregion

        #region IAssetManager — LoadAndInstantiateAsync

        /// <inheritdoc />
        public async UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await LoadAndInstantiateAsyncInternal(_defaultPackage, location, progress, cancellationToken);
        }

        /// <inheritdoc />
        public async UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("Package name is required.", nameof(packageName));
            }

            var package = YooAssets.GetPackage(packageName);
            if (package == null)
            {
                throw new InvalidOperationException(
                    $"YooAsset package not found: '{packageName}'. Ensure the package is initialized before loading assets.");
            }

            return await LoadAndInstantiateAsyncInternal(package, location, progress, cancellationToken);
        }

        #endregion

        #region Private — Core Implementation

        /// <summary>
        /// 异步加载资源的核心实现
        /// </summary>
        /// <typeparam name="T">要加载的资源类型，必须是 UnityEngine.Object 的子类</typeparam>
        /// <param name="package">YooAsset 资源包</param>
        /// <param name="location">资源地址</param>
        /// <param name="progress">可选的进度报告器（当前版本暂未实现进度轮询）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>资源的租约句柄</returns>
        /// <exception cref="ArgumentException">当 location 为 null 或空白时抛出</exception>
        /// <exception cref="InvalidOperationException">当加载失败、资源为 null 或 YooAsset 操作失败时抛出</exception>
        /// <exception cref="OperationCanceledException">当操作被取消时抛出</exception>
        private async UniTask<AssetLease<T>> LoadAsyncInternal<T>(
            ResourcePackage package,
            string location,
            IProgress<float> progress,
            CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Asset location is required.", nameof(location));
            }

            AssetHandle handle;
            try
            {
                handle = package.LoadAssetAsync<T>(location);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Failed to start async load. location='{location}', type={typeof(T).Name}",
                    exception);
            }

            if (handle == null)
            {
                throw new InvalidOperationException(
                    $"YooAsset returned null handle. location='{location}', type={typeof(T).Name}");
            }

            try
            {
                await handle.Task
                    .AsUniTask()
                    .AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ReleaseHandleNoThrow(handle);
                throw;
            }
            catch (Exception exception)
            {
                ReleaseHandleNoThrow(handle);
                throw new InvalidOperationException(
                    $"Async load task failed. location='{location}', type={typeof(T).Name}",
                    exception);
            }

            if (handle.Status != EOperationStatus.Succeed)
            {
                var lastError = string.IsNullOrEmpty(handle.LastError) ? "unknown" : handle.LastError;
                ReleaseHandleNoThrow(handle);
                throw new InvalidOperationException(
                    $"YooAsset load failed. location='{location}', type={typeof(T).Name}, error='{lastError}'");
            }

            T asset;
            try
            {
                asset = handle.GetAssetObject<T>();
            }
            catch (OperationCanceledException)
            {
                ReleaseHandleNoThrow(handle);
                throw;
            }
            catch (Exception exception)
            {
                ReleaseHandleNoThrow(handle);
                throw new InvalidOperationException(
                    $"Failed to get asset object from handle. location='{location}', type={typeof(T).Name}",
                    exception);
            }

            if (asset == null)
            {
                ReleaseHandleNoThrow(handle);
                throw new InvalidOperationException(
                    $"Loaded asset is null. location='{location}', type={typeof(T).Name}");
            }

            var released = false;
            return new AssetLease<T>(asset, () =>
            {
                if (released)
                {
                    return;
                }

                released = true;
                ReleaseHandleNoThrow(handle);
            });
        }

        /// <summary>
        /// 异步加载并实例化 GameObject 的核心实现
        /// </summary>
        /// <param name="package">YooAsset 资源包</param>
        /// <param name="location">预制体资源地址</param>
        /// <param name="progress">可选的进度报告器</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>GameObject 实例的租约句柄</returns>
        private async UniTask<GameObjectLease> LoadAndInstantiateAsyncInternal(
            ResourcePackage package,
            string location,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Private — Helpers

        /// <summary>
        /// 安全释放 YooAsset handle，吞掉所有异常
        /// </summary>
        /// <param name="handle">要释放的 handle，可以为 null</param>
        private static void ReleaseHandleNoThrow(AssetHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            try
            {
                handle.Release();
            }
            catch
            {
                // 吞掉释放异常，防止掩盖原始错误
            }
        }

        #endregion
    }
}
