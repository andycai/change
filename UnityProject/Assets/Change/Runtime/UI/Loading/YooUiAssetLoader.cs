using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Change.Framework.UI;
using UnityEngine;
using YooAsset;

namespace Change.Runtime.UI
{
    internal interface IYooAssetPackage
    {
        IYooAssetLoadHandle LoadGameObjectAsync(string location);
    }

    internal interface IYooAssetLoadHandle
    {
        System.Threading.Tasks.Task Task { get; }
        bool Succeeded { get; }
        string LastError { get; }
        GameObject AssetObject { get; }
        void Release();
    }

    internal sealed class YooAssetPackageAdapter : IYooAssetPackage
    {
        private readonly ResourcePackage _package;

        internal YooAssetPackageAdapter(ResourcePackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        IYooAssetLoadHandle IYooAssetPackage.LoadGameObjectAsync(string location)
        {
            return new YooAssetLoadHandleAdapter(_package.LoadAssetAsync<GameObject>(location));
        }
    }

    internal sealed class YooAssetLoadHandleAdapter : IYooAssetLoadHandle
    {
        private readonly AssetHandle _handle;

        public YooAssetLoadHandleAdapter(AssetHandle handle)
        {
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
        }

        public System.Threading.Tasks.Task Task => _handle.Task;
        public bool Succeeded => _handle.Status == EOperationStatus.Succeed;
        public string LastError => _handle.LastError;
        public GameObject AssetObject => _handle.GetAssetObject<GameObject>();
        public void Release() => _handle.Release();
    }

    public sealed class YooUiAssetLoader : IUiAssetLoader
    {
        private readonly IYooAssetPackage _package;

        internal YooUiAssetLoader(IYooAssetPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public static IUiAssetLoader FromResourcePackage(ResourcePackage package)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            return new YooUiAssetLoader(new YooAssetPackageAdapter(package));
        }

        public async UniTask<UiAssetLease> LoadPrefabAsync(WindowId windowId, string location, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Asset location is required.", nameof(location));
            }

            IYooAssetLoadHandle handle;
            try
            {
                handle = _package.LoadGameObjectAsync(location);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateLoadFailureException(windowId, location, "package threw while creating load handle", exception);
            }

            if (handle == null)
            {
                throw CreateLoadFailureException(windowId, location, "package returned null load handle");
            }

            try
            {
                await handle.Task.AsUniTask()
                    .AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ReleaseNoThrow(handle);
                throw;
            }
            catch (Exception exception)
            {
                var releaseException = TryReleaseForFailure(handle);
                throw CreateLoadFailureException(windowId, location, "async load task failed", exception, releaseException);
            }

            if (!handle.Succeeded)
            {
                var lastError = string.IsNullOrEmpty(handle.LastError) ? "unknown" : handle.LastError;
                var releaseException = TryReleaseForFailure(handle);
                throw CreateLoadFailureException(windowId, location, $"YooAsset reported failure: {lastError}", null, releaseException);
            }

            GameObject prefab;
            try
            {
                prefab = handle.AssetObject;
            }
            catch (OperationCanceledException)
            {
                ReleaseNoThrow(handle);
                throw;
            }
            catch (Exception exception)
            {
                var releaseException = TryReleaseForFailure(handle);
                throw CreateLoadFailureException(windowId, location, "failed to resolve loaded prefab", exception, releaseException);
            }
            if (prefab == null)
            {
                var releaseException = TryReleaseForFailure(handle);
                throw CreateLoadFailureException(windowId, location, "loaded prefab is null", null, releaseException);
            }

            GameObject instance;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab);
            }
            catch (OperationCanceledException)
            {
                ReleaseNoThrow(handle);
                throw;
            }
            catch (Exception exception)
            {
                var releaseException = TryReleaseForFailure(handle);
                throw CreateLoadFailureException(windowId, location, "failed to instantiate loaded prefab", exception, releaseException);
            }

            var released = false;
            return new UiAssetLease(instance, () =>
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

                ReleaseNoThrow(handle);
            });
        }

        private static InvalidOperationException CreateLoadFailureException(WindowId windowId, string location, string reason, Exception innerException = null)
        {
            return new InvalidOperationException($"Failed to load window asset. id={windowId}, location={location}, reason={reason}", innerException);
        }

        private static InvalidOperationException CreateLoadFailureException(WindowId windowId, string location, string reason, Exception primaryException, Exception releaseException)
        {
            Exception innerException;
            if (primaryException != null && releaseException != null)
            {
                innerException = new AggregateException(primaryException, releaseException);
            }
            else
            {
                innerException = primaryException ?? releaseException;
            }

            return CreateLoadFailureException(windowId, location, reason, innerException);
        }

        private static Exception TryReleaseForFailure(IYooAssetLoadHandle handle)
        {
            if (handle == null)
            {
                return null;
            }

            try
            {
                handle.Release();
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static void ReleaseNoThrow(IYooAssetLoadHandle handle)
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
            }
        }
    }
}
