using System;
using System.Collections.Generic;
using System.Threading;
using Change.Framework.UI;
using Cysharp.Threading.Tasks;
using FairyGUI;
using UnityEngine;
using YooAsset;

namespace Change.Runtime.UI
{
    internal interface IYooAssetPackage
    {
        IYooAssetLoadHandle LoadGameObjectAsync(string location);
        IYooRawAssetHandle LoadRawAssetAsync(string location);
        UnityEngine.Object LoadRawAssetSync(string location, System.Type assetType);
    }

    internal interface IYooAssetLoadHandle
    {
        System.Threading.Tasks.Task Task { get; }
        bool Succeeded { get; }
        string LastError { get; }
        GameObject AssetObject { get; }
        void Release();
    }

    internal interface IYooRawAssetHandle
    {
        System.Threading.Tasks.Task Task { get; }
        bool Succeeded { get; }
        string LastError { get; }
        UnityEngine.Object AssetObject { get; }
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

        IYooRawAssetHandle IYooAssetPackage.LoadRawAssetAsync(string location)
        {
            return new YooRawAssetHandleAdapter(_package.LoadAssetAsync<UnityEngine.Object>(location));
        }

        UnityEngine.Object IYooAssetPackage.LoadRawAssetSync(string location, System.Type assetType)
        {
            var handle = _package.LoadAssetSync(location, assetType);
            try
            {
                if (handle.Status != EOperationStatus.Succeed)
                    return null;
                return handle.AssetObject;
            }
            finally
            {
                handle.Release();
            }
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

    internal sealed class YooRawAssetHandleAdapter : IYooRawAssetHandle
    {
        private readonly AssetHandle _handle;

        public YooRawAssetHandleAdapter(AssetHandle handle)
        {
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
        }

        public System.Threading.Tasks.Task Task => _handle.Task;
        public bool Succeeded => _handle.Status == EOperationStatus.Succeed;
        public string LastError => _handle.LastError;
        public UnityEngine.Object AssetObject => _handle.AssetObject;
        public void Release() => _handle.Release();
    }

    public sealed class YooUiAssetLoader : IUiAssetLoader
    {
        private readonly IYooAssetPackage _package;
        private readonly Dictionary<string, UniTaskCompletionSource> _pendingLoads = new();

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

        public async UniTask LoadPackageAsync(string packageName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("Package name is required.", nameof(packageName));
            }

            if (UIPackage.GetById(packageName) != null)
            {
                return;
            }

            UniTaskCompletionSource existingTcs = null;
            lock (_pendingLoads)
            {
                if (UIPackage.GetById(packageName) != null)
                    return;
                if (_pendingLoads.TryGetValue(packageName, out var pending))
                {
                    existingTcs = pending;
                }
                else
                {
                    _pendingLoads[packageName] = new UniTaskCompletionSource();
                }
            }

            if (existingTcs != null)
            {
                await existingTcs.Task;
                return;
            }

            try
            {
                IYooRawAssetHandle handle;
                try
                {
                    var location = packageName + "_fui";
                    handle = _package.LoadRawAssetAsync(location);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Failed to load FairyGUI package descriptor. package={packageName}, reason=package threw while creating load handle",
                        exception);
                }

                if (handle == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to load FairyGUI package descriptor. package={packageName}, reason=package returned null load handle");
                }

                try
                {
                    await handle.Task.AsUniTask()
                        .AttachExternalCancellation(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    ReleaseRawNoThrow(handle);
                    throw;
                }
                catch (Exception exception)
                {
                    var releaseException = TryReleaseRawForFailure(handle);
                    throw new InvalidOperationException(
                        $"Failed to load FairyGUI package descriptor. package={packageName}, reason=async load task failed",
                        releaseException != null ? new AggregateException(exception, releaseException) : exception);
                }

                if (!handle.Succeeded)
                {
                    var lastError = string.IsNullOrEmpty(handle.LastError) ? "unknown" : handle.LastError;
                    var releaseException = TryReleaseRawForFailure(handle);
                    throw new InvalidOperationException(
                        $"Failed to load FairyGUI package descriptor. package={packageName}, reason=YooAsset reported failure: {lastError}",
                        releaseException);
                }

                TextAsset textAsset;
                try
                {
                    textAsset = handle.AssetObject as TextAsset;
                }
                catch (OperationCanceledException)
                {
                    ReleaseRawNoThrow(handle);
                    throw;
                }
                catch (Exception exception)
                {
                    var releaseException = TryReleaseRawForFailure(handle);
                    throw new InvalidOperationException(
                        $"Failed to resolve loaded package descriptor. package={packageName}",
                        releaseException != null ? new AggregateException(exception, releaseException) : exception);
                }

                if (textAsset == null)
                {
                    var releaseException = TryReleaseRawForFailure(handle);
                    throw new InvalidOperationException(
                        $"Failed to load FairyGUI package descriptor. package={packageName}, reason=loaded asset is null or not a TextAsset",
                        releaseException);
                }

                UIPackage.LoadResource loadFunc = (name, extension, type, out DestroyMethod destroyMethod) =>
                {
                    destroyMethod = DestroyMethod.None;
                    var location = name + extension;
                    return _package.LoadRawAssetSync(location, type);
                };

                try
                {
                    UIPackage.AddPackage(textAsset.bytes, packageName, loadFunc);
                }
                finally
                {
                    ReleaseRawNoThrow(handle);
                }

                lock (_pendingLoads)
                {
                    if (_pendingLoads.TryGetValue(packageName, out var tcs))
                    {
                        _pendingLoads.Remove(packageName);
                        tcs.TrySetResult();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                lock (_pendingLoads)
                {
                    if (_pendingLoads.TryGetValue(packageName, out var tcs))
                    {
                        _pendingLoads.Remove(packageName);
                        tcs.TrySetCanceled();
                    }
                }
                throw;
            }
            catch (Exception ex)
            {
                lock (_pendingLoads)
                {
                    if (_pendingLoads.TryGetValue(packageName, out var tcs))
                    {
                        _pendingLoads.Remove(packageName);
                        tcs.TrySetException(ex);
                    }
                }
                throw;
            }
        }

        public void UnloadPackage(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return;
            }

            UIPackage.RemovePackage(packageName);
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

        private static Exception TryReleaseRawForFailure(IYooRawAssetHandle handle)
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

        private static void ReleaseRawNoThrow(IYooRawAssetHandle handle)
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
