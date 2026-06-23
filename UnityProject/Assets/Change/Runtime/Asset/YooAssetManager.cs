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
        public UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            return LoadAndInstantiateAsyncInternal(_defaultPackage, location, progress, cancellationToken);
        }

        /// <inheritdoc />
        public UniTask<GameObjectLease> LoadAndInstantiateAsync(
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

            return LoadAndInstantiateAsyncInternal(package, location, progress, cancellationToken);
        }

        #endregion

        #region Private — Core Implementation

        /// <summary>
        /// 异步加载资源的核心实现
        /// </summary>
        /// <typeparam name="T">要加载的资源类型，必须是 UnityEngine.Object 的子类</typeparam>
        /// <param name="package">YooAsset 资源包</param>
        /// <param name="location">资源地址</param>
        /// <param name="progress">可选的进度报告器，范围 0.0 到 1.0</param>
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

            if (progress != null)
            {
                ReportProgressAsync(handle, progress, cancellationToken).Forget();
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
        /// <remarks>
        /// <para>首先通过 <see cref="LoadAsyncInternal{T}"/> 加载预制体资源，然后调用 <see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/> 实例化。</para>
        /// <para>实例化失败时自动释放已加载的资源租约。</para>
        /// <para>返回的 <see cref="GameObjectLease"/> 释放时会销毁实例并释放底层资源。</para>
        /// </remarks>
        /// <param name="package">YooAsset 资源包</param>
        /// <param name="location">预制体资源地址</param>
        /// <param name="progress">可选的进度报告器</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>GameObject 实例的租约句柄</returns>
        /// <exception cref="ArgumentException">当 location 为 null 或空白时抛出</exception>
        /// <exception cref="InvalidOperationException">当加载或实例化失败时抛出</exception>
        /// <exception cref="OperationCanceledException">当操作被取消时抛出</exception>
        private async UniTask<GameObjectLease> LoadAndInstantiateAsyncInternal(
            ResourcePackage package,
            string location,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            var lease = await LoadAsyncInternal<GameObject>(package, location, progress, cancellationToken);

            GameObject instance;
            try
            {
                instance = UnityEngine.Object.Instantiate(lease.Asset);
            }
            catch (OperationCanceledException)
            {
                lease.Dispose();
                throw;
            }
            catch (Exception exception)
            {
                lease.Dispose();
                throw new InvalidOperationException(
                    $"Failed to instantiate GameObject. location='{location}'",
                    exception);
            }

            if (instance == null)
            {
                lease.Dispose();
                throw new InvalidOperationException(
                    $"Instantiate returned null. location='{location}'");
            }

            var released = false;
            return new GameObjectLease(instance, () =>
            {
                if (released)
                {
                    return;
                }

                released = true;

                if (instance != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(instance);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(instance);
                    }
                }

                lease.Dispose();
            });
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

        /// <summary>
        /// 后台轮询 handle 进度并报告给调用方
        /// </summary>
        /// <remarks>
        /// <para>本方法通过 <c>.Forget()</c> 启动为 fire-and-forget 的 UniTaskVoid。</para>
        /// <para>循环检测 <paramref name="handle"/>.IsDone 和 <paramref name="cancellationToken"/>.IsCancellationRequested。</para>
        /// <para>每次循环报告当前进度并通过 <see cref="UniTask.Yield()"/> 让出主线程。</para>
        /// <para>完成时（未被取消）报告 1.0f 以填充最后的进度增量。</para>
        /// </remarks>
        /// <param name="handle">YooAsset 的 AssetHandle，用于读取进度</param>
        /// <param name="progress">进度报告器</param>
        /// <param name="cancellationToken">取消令牌</param>
        private static async UniTaskVoid ReportProgressAsync(
            AssetHandle handle,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!handle.IsDone && !cancellationToken.IsCancellationRequested)
                {
                    progress.Report(handle.Progress);
                    await UniTask.Yield();
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    progress.Report(1.0f);
                }
            }
            catch
            {
                // 进度报告失败不应影响加载流程，静默忽略
            }
        }

        #endregion
    }
}
