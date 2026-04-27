using System;
using System.Collections;
using System.Threading;
using Change.Framework.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Change.Runtime.UI.Tests
{
    public class YooUiAssetLoaderTests
    {
        [UnityTest]
        public IEnumerator LoadPrefabAsync_SuccessfulPrefabLoad_ReturnsLeaseWithInstance()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var package = new FakeYooPackage(success: true);
                var loader = new YooUiAssetLoader(package);

                using var lease = await loader.LoadPrefabAsync(new WindowId("Inventory"), "ui/inventory.prefab", CancellationToken.None);

                Assert.NotNull(lease.Instance);
            });
        }

        [UnityTest]
        public IEnumerator LoadPrefabAsync_FailedHandle_ThrowsInvalidOperationException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var package = new FakeYooPackage(success: false);
                var loader = new YooUiAssetLoader(package);

                try
                {
                    await loader.LoadPrefabAsync(new WindowId("Inventory"), "ui/missing.prefab", CancellationToken.None);
                    Assert.Fail("Expected InvalidOperationException when YooAsset handle reports failure.");
                }
                catch (InvalidOperationException)
                {
                }
            });
        }

        [UnityTest]
        public IEnumerator LoadPrefabAsync_PackageThrow_ThrowsInvalidOperationExceptionWithInnerException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var rootCause = new Exception("package-fault");
                var package = new ThrowingYooPackage(rootCause);
                var loader = new YooUiAssetLoader(package);
                var windowId = new WindowId("Inventory");
                const string location = "ui/inventory.prefab";

                try
                {
                    await loader.LoadPrefabAsync(windowId, location, CancellationToken.None);
                    Assert.Fail("Expected InvalidOperationException when package throws.");
                }
                catch (InvalidOperationException exception)
                {
                    Assert.That(exception.Message, Does.Contain(windowId.ToString()));
                    Assert.That(exception.Message, Does.Contain(location));
                    Assert.AreSame(rootCause, exception.InnerException);
                }
            });
        }

        [UnityTest]
        public IEnumerator LoadPrefabAsync_LoadTaskThrow_ThrowsInvalidOperationExceptionWithInnerException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var rootCause = new Exception("load-fault");
                var package = new TaskFaultYooPackage(rootCause);
                var loader = new YooUiAssetLoader(package);
                var windowId = new WindowId("Inventory");
                const string location = "ui/inventory.prefab";

                try
                {
                    await loader.LoadPrefabAsync(windowId, location, CancellationToken.None);
                    Assert.Fail("Expected InvalidOperationException when async load task throws.");
                }
                catch (InvalidOperationException exception)
                {
                    Assert.That(exception.Message, Does.Contain(windowId.ToString()));
                    Assert.That(exception.Message, Does.Contain(location));
                    Assert.AreSame(rootCause, exception.InnerException);
                }
            });
        }

        [UnityTest]
        public IEnumerator LoadPrefabAsync_Cancellation_DoesNotWrapOperationCanceledException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var package = new PendingYooPackage();
                var loader = new YooUiAssetLoader(package);
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();

                try
                {
                    await loader.LoadPrefabAsync(new WindowId("Inventory"), "ui/inventory.prefab", cancellation.Token);
                    Assert.Fail("Expected cancellation.");
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        [UnityTest]
        public IEnumerator LoadPrefabAsync_FailedHandleWithReleaseThrow_StillThrowsInvalidOperationException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var package = new FakeYooPackage(success: false, releaseException: new Exception("release-fault"));
                var loader = new YooUiAssetLoader(package);

                try
                {
                    await loader.LoadPrefabAsync(new WindowId("Inventory"), "ui/missing.prefab", CancellationToken.None);
                    Assert.Fail("Expected InvalidOperationException when load fails.");
                }
                catch (InvalidOperationException exception)
                {
                    Assert.That(exception.Message, Does.Contain("Inventory"));
                    Assert.That(exception.Message, Does.Contain("ui/missing.prefab"));
                    Assert.NotNull(exception.InnerException);
                    Assert.That(exception.InnerException.ToString(), Does.Contain("release-fault"));
                }
            });
        }

        [UnityTest]
        public IEnumerator LoadPrefabAsync_DisposeLeaseWithReleaseThrow_DoesNotThrow()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var package = new FakeYooPackage(success: true, releaseException: new Exception("release-fault"));
                var loader = new YooUiAssetLoader(package);
                var lease = await loader.LoadPrefabAsync(new WindowId("Inventory"), "ui/inventory.prefab", CancellationToken.None);

                Assert.DoesNotThrow(() => lease.Dispose());
            });
        }
    }

    internal sealed class FakeYooPackage : IYooAssetPackage
    {
        private readonly bool _success;
        private readonly Exception _releaseException;

        public FakeYooPackage(bool success, Exception releaseException = null)
        {
            _success = success;
            _releaseException = releaseException;
        }

        public IYooAssetLoadHandle LoadGameObjectAsync(string location)
        {
            var prefab = _success ? new GameObject($"prefab:{location}") : null;
            var error = _success ? string.Empty : "asset-not-found";
            return new FakeYooLoadHandle(_success, prefab, error, _releaseException);
        }
    }

    internal sealed class FakeYooLoadHandle : IYooAssetLoadHandle
    {
        private readonly bool _succeeded;
        private readonly GameObject _prefab;
        private readonly string _lastError;
        private readonly Exception _releaseException;

        public FakeYooLoadHandle(bool succeeded, GameObject prefab, string lastError, Exception releaseException)
        {
            _succeeded = succeeded;
            _prefab = prefab;
            _lastError = lastError;
            _releaseException = releaseException;
        }

        public System.Threading.Tasks.Task Task => System.Threading.Tasks.Task.CompletedTask;
        public bool Succeeded => _succeeded;
        public string LastError => _lastError;
        public GameObject AssetObject => _prefab;

        public void Release()
        {
            if (_releaseException != null)
            {
                throw _releaseException;
            }

            if (_prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_prefab);
            }
        }
    }

    internal sealed class ThrowingYooPackage : IYooAssetPackage
    {
        private readonly Exception _exception;

        public ThrowingYooPackage(Exception exception)
        {
            _exception = exception;
        }

        public IYooAssetLoadHandle LoadGameObjectAsync(string location)
        {
            throw _exception;
        }
    }

    internal sealed class TaskFaultYooPackage : IYooAssetPackage
    {
        private readonly Exception _exception;

        public TaskFaultYooPackage(Exception exception)
        {
            _exception = exception;
        }

        public IYooAssetLoadHandle LoadGameObjectAsync(string location)
        {
            return new TaskFaultYooLoadHandle(_exception);
        }
    }

    internal sealed class TaskFaultYooLoadHandle : IYooAssetLoadHandle
    {
        private readonly Exception _exception;

        public TaskFaultYooLoadHandle(Exception exception)
        {
            _exception = exception;
        }

        public System.Threading.Tasks.Task Task => System.Threading.Tasks.Task.FromException(_exception);
        public bool Succeeded => false;
        public string LastError => "task-fault";
        public GameObject AssetObject => null;
        public void Release()
        {
        }
    }

    internal sealed class PendingYooPackage : IYooAssetPackage
    {
        public IYooAssetLoadHandle LoadGameObjectAsync(string location)
        {
            return new PendingYooLoadHandle();
        }
    }

    internal sealed class PendingYooLoadHandle : IYooAssetLoadHandle
    {
        private readonly System.Threading.Tasks.TaskCompletionSource<bool> _completion = new();

        public System.Threading.Tasks.Task Task => _completion.Task;
        public bool Succeeded => false;
        public string LastError => string.Empty;
        public GameObject AssetObject => null;

        public void Release()
        {
        }
    }
}
