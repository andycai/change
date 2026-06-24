using System;
using System.Collections;
using System.Text;
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

        [UnityTest]
        public IEnumerator LoadPackageAsync_SuccessfulLoad_CreatesUIPackageEntry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var packageBinary = CreateMinimalPackageBinary("testpkg", "TestPkg");
                var textAsset = CreateTextAsset(packageBinary);
                var package = FakeYooPackage.ForRawAsset(textAsset);
                var loader = new YooUiAssetLoader(package);

                await loader.LoadPackageAsync("testpkg", CancellationToken.None);

                var pkg = FairyGUI.UIPackage.GetById("testpkg");
                Assert.NotNull(pkg, "Package should be loaded and accessible by id.");
                Assert.AreEqual("TestPkg", pkg.name);

                loader.UnloadPackage("testpkg");
            });
        }

        [UnityTest]
        public IEnumerator LoadPackageAsync_DuplicateLoad_DoesNotThrow()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var packageBinary = CreateMinimalPackageBinary("dupkg", "DupPkg");
                var textAsset = CreateTextAsset(packageBinary);
                var package = FakeYooPackage.ForRawAsset(textAsset);
                var loader = new YooUiAssetLoader(package);

                await loader.LoadPackageAsync("dupkg", CancellationToken.None);

                Exception caught = null;
                try
                {
                    await loader.LoadPackageAsync("dupkg", CancellationToken.None);
                }
                catch (Exception exception)
                {
                    caught = exception;
                }

                Assert.Null(caught, "Duplicate load should not throw.");

                var pkg = FairyGUI.UIPackage.GetById("dupkg");
                Assert.NotNull(pkg);

                loader.UnloadPackage("dupkg");
            });
        }

        [UnityTest]
        public IEnumerator LoadPackageAsync_NullPackageName_ThrowsArgumentException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var package = new FakeYooPackage();
                var loader = new YooUiAssetLoader(package);

                try
                {
                    await loader.LoadPackageAsync(null, CancellationToken.None);
                    Assert.Fail("Expected ArgumentException when package name is null.");
                }
                catch (ArgumentException exception)
                {
                    Assert.That(exception.Message, Does.Contain("packageName"));
                }
            });
        }

        [UnityTest]
        public IEnumerator LoadPackageAsync_EmptyPackageName_ThrowsArgumentException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var package = new FakeYooPackage();
                var loader = new YooUiAssetLoader(package);

                try
                {
                    await loader.LoadPackageAsync("  ", CancellationToken.None);
                    Assert.Fail("Expected ArgumentException when package name is whitespace.");
                }
                catch (ArgumentException exception)
                {
                    Assert.That(exception.Message, Does.Contain("packageName"));
                }
            });
        }

        [UnityTest]
        public IEnumerator LoadPackageAsync_PackageCreateHandleThrow_ThrowsInvalidOperationException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var rootCause = new Exception("create-handle-fault");
                var package = new ThrowingYooPackage(rootCause);
                var loader = new YooUiAssetLoader(package);

                try
                {
                    await loader.LoadPackageAsync("faultpkg", CancellationToken.None);
                    Assert.Fail("Expected InvalidOperationException when package throws during create handle.");
                }
                catch (InvalidOperationException exception)
                {
                    Assert.That(exception.Message, Does.Contain("faultpkg"));
                    Assert.AreSame(rootCause, exception.InnerException);
                }
            });
        }

        [UnityTest]
        public IEnumerator LoadPackageAsync_FailedHandle_ThrowsInvalidOperationException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var package = FakeYooPackage.RawFailed();
                var loader = new YooUiAssetLoader(package);

                try
                {
                    await loader.LoadPackageAsync("failpkg", CancellationToken.None);
                    Assert.Fail("Expected InvalidOperationException when YooAsset handle reports failure.");
                }
                catch (InvalidOperationException exception)
                {
                    Assert.That(exception.Message, Does.Contain("failpkg"));
                    Assert.That(exception.Message, Does.Contain("YooAsset reported failure"));
                }
            });
        }

        [UnityTest]
        public IEnumerator LoadPackageAsync_Cancellation_DoesNotWrapOperationCanceledException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var package = new PendingYooPackage();
                var loader = new YooUiAssetLoader(package);
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();

                try
                {
                    await loader.LoadPackageAsync("cancelpkg", cancellation.Token);
                    Assert.Fail("Expected cancellation.");
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        [UnityTest]
        public IEnumerator UnloadPackage_RemovesPackageFromFairyGUI()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var packageBinary = CreateMinimalPackageBinary("rmpkg", "RmPkg");
                var textAsset = CreateTextAsset(packageBinary);
                var package = FakeYooPackage.ForRawAsset(textAsset);
                var loader = new YooUiAssetLoader(package);

                await loader.LoadPackageAsync("rmpkg", CancellationToken.None);
                Assert.NotNull(FairyGUI.UIPackage.GetById("rmpkg"));

                loader.UnloadPackage("rmpkg");
                Assert.Null(FairyGUI.UIPackage.GetById("rmpkg"));
            });
        }

        [UnityTest]
        public IEnumerator UnloadPackage_NullOrEmptyName_DoesNotThrow()
        {
            var package = new FakeYooPackage();
            var loader = new YooUiAssetLoader(package);

            Assert.DoesNotThrow(() => loader.UnloadPackage(null));
            Assert.DoesNotThrow(() => loader.UnloadPackage(string.Empty));
            Assert.DoesNotThrow(() => loader.UnloadPackage("  "));
            return null;
        }

        [UnityTest]
        public IEnumerator LoadPackageAsync_SuccessfulLoad_ReleasesFuiHandle()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var packageBinary = CreateMinimalPackageBinary("relpkg", "RelPkg");
                var textAsset = CreateTextAsset(packageBinary);
                var rawHandle = new FakeYooRawAssetHandle(true, textAsset, string.Empty, null);
                var package = new FakeYooPackageWithRawHandle(rawHandle);
                var loader = new YooUiAssetLoader(package);

                await loader.LoadPackageAsync("relpkg", CancellationToken.None);

                Assert.AreEqual(1, rawHandle.ReleaseCount, "FUI descriptor handle should be released after successful load.");
                loader.UnloadPackage("relpkg");
            });
        }

        [UnityTest]
        public IEnumerator LoadPackageAsync_ConcurrentCalls_DoNotThrowDuplicatePackage()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var packageBinary = CreateMinimalPackageBinary("concpkg", "ConcPkg");
                var textAsset = CreateTextAsset(packageBinary);
                var gateHandle = new GateYooRawAssetHandle(textAsset);
                var package = new GateYooPackage(gateHandle, textAsset);
                var loader = new YooUiAssetLoader(package);

                var task1 = loader.LoadPackageAsync("concpkg", CancellationToken.None);
                var task2 = loader.LoadPackageAsync("concpkg", CancellationToken.None);

                gateHandle.Open();

                await UniTask.WhenAll(task1, task2);

                var pkg = FairyGUI.UIPackage.GetById("concpkg");
                Assert.NotNull(pkg, "Package should be loaded exactly once.");
                Assert.AreEqual("ConcPkg", pkg.name);

                loader.UnloadPackage("concpkg");
            });
        }

        [UnityTest]
        public IEnumerator LoadPackageAsync_AfterFailure_PendingLoadsCleanedUp()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var packageBinary = CreateMinimalPackageBinary("retrypkg", "RetryPkg");
                var textAsset = CreateTextAsset(packageBinary);
                var package = new RetryYooPackage(textAsset);
                var loader = new YooUiAssetLoader(package);

                try
                {
                    await loader.LoadPackageAsync("retrypkg", CancellationToken.None);
                    Assert.Fail("First load should fail.");
                }
                catch (InvalidOperationException)
                {
                }

                Assert.AreEqual(1, package.RawAssetCallCount);

                await loader.LoadPackageAsync("retrypkg", CancellationToken.None);

                Assert.AreEqual(2, package.RawAssetCallCount, "Second call should retry after TCS cleanup.");
                Assert.NotNull(FairyGUI.UIPackage.GetById("retrypkg"));
                loader.UnloadPackage("retrypkg");
            });
        }

        private static byte[] CreateMinimalPackageBinary(string packageId, string packageName)
        {
            byte[] idBytes = Encoding.UTF8.GetBytes(packageId);
            byte[] nameBytes = Encoding.UTF8.GetBytes(packageName);

            int idStrSize = 2 + idBytes.Length;
            int nameStrSize = 2 + nameBytes.Length;
            int headerSize = 4 + 4 + 1 + idStrSize + nameStrSize + 20;

            int indexTablePos = headerSize;
            const int segCount = 6;
            int offsetTableSize = 2 + segCount * 4;

            int block0Size = 2;
            int block1Size = 2;
            int block2Size = 2;
            int block3Size = 2;
            int block4Size = 4;
            int block5Size = 4;

            int[] offsets = new int[segCount];
            offsets[0] = offsetTableSize;
            offsets[1] = offsets[0] + block0Size;
            offsets[2] = offsets[1] + block1Size;
            offsets[3] = offsets[2] + block2Size;
            offsets[4] = offsets[3] + block3Size;
            offsets[5] = offsets[4] + block4Size;

            int totalBlockDataSize = block0Size + block1Size + block2Size + block3Size + block4Size + block5Size;
            int totalSize = headerSize + offsetTableSize + totalBlockDataSize;

            byte[] data = new byte[totalSize];
            int pos = 0;

            data[pos++] = 0x46;
            data[pos++] = 0x47;
            data[pos++] = 0x55;
            data[pos++] = 0x49;

            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x03;

            data[pos++] = 0x00;

            data[pos++] = (byte)(idBytes.Length >> 8);
            data[pos++] = (byte)(idBytes.Length & 0xFF);
            Array.Copy(idBytes, 0, data, pos, idBytes.Length);
            pos += idBytes.Length;

            data[pos++] = (byte)(nameBytes.Length >> 8);
            data[pos++] = (byte)(nameBytes.Length & 0xFF);
            Array.Copy(nameBytes, 0, data, pos, nameBytes.Length);
            pos += nameBytes.Length;

            pos += 20;

            data[pos++] = segCount;
            data[pos++] = 0x00;

            for (int i = 0; i < segCount; i++)
            {
                int offset = offsets[i];
                data[pos++] = (byte)(offset >> 24);
                data[pos++] = (byte)((offset >> 16) & 0xFF);
                data[pos++] = (byte)((offset >> 8) & 0xFF);
                data[pos++] = (byte)(offset & 0xFF);
            }

            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;
            data[pos++] = 0x00;

            return data;
        }

        private static UnityEngine.TextAsset CreateTextAsset(byte[] bytes)
        {
            return new UnityEngine.TextAsset(Encoding.UTF8.GetString(bytes));
        }
    }

    internal sealed class FakeYooPackage : IYooAssetPackage
    {
        private readonly bool _success;
        private readonly Exception _releaseException;
        private readonly UnityEngine.Object _rawAsset;
        private readonly bool _rawSuccess;

        public FakeYooPackage(bool success = true, Exception releaseException = null)
            : this(success, releaseException, null, true)
        {
        }

        public FakeYooPackage(bool success, Exception releaseException, UnityEngine.Object rawAsset, bool rawSuccess = true)
        {
            _success = success;
            _releaseException = releaseException;
            _rawAsset = rawAsset;
            _rawSuccess = rawSuccess;
        }

        public static FakeYooPackage ForRawAsset(UnityEngine.Object rawAsset)
        {
            return new FakeYooPackage(true, null, rawAsset, true);
        }

        public static FakeYooPackage RawFailed()
        {
            return new FakeYooPackage(true, null, null, false);
        }

        public IYooAssetLoadHandle LoadGameObjectAsync(string location)
        {
            var prefab = _success ? new GameObject($"prefab:{location}") : null;
            var error = _success ? string.Empty : "asset-not-found";
            return new FakeYooLoadHandle(_success, prefab, error, _releaseException);
        }

        public IYooRawAssetHandle LoadRawAssetAsync(string location)
        {
            var error = _rawSuccess ? string.Empty : "asset-not-found";
            return new FakeYooRawAssetHandle(_rawSuccess, _rawAsset, error, _releaseException);
        }

        public UnityEngine.Object LoadRawAssetSync(string location, Type assetType)
        {
            if (!_rawSuccess)
                return null;
            return _rawAsset;
        }
    }

    internal sealed class FakeYooPackageWithRawHandle : IYooAssetPackage
    {
        private readonly IYooRawAssetHandle _rawHandle;

        public FakeYooPackageWithRawHandle(IYooRawAssetHandle rawHandle)
        {
            _rawHandle = rawHandle ?? throw new ArgumentNullException(nameof(rawHandle));
        }

        public IYooAssetLoadHandle LoadGameObjectAsync(string location)
        {
            throw new NotImplementedException();
        }

        public IYooRawAssetHandle LoadRawAssetAsync(string location)
        {
            return _rawHandle;
        }

        public UnityEngine.Object LoadRawAssetSync(string location, Type assetType)
        {
            return _rawHandle.AssetObject;
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

    internal sealed class FakeYooRawAssetHandle : IYooRawAssetHandle
    {
        private readonly bool _succeeded;
        private readonly UnityEngine.Object _asset;
        private readonly string _lastError;
        private readonly Exception _releaseException;

        public FakeYooRawAssetHandle(bool succeeded, UnityEngine.Object asset, string lastError, Exception releaseException)
        {
            _succeeded = succeeded;
            _asset = asset;
            _lastError = lastError;
            _releaseException = releaseException;
        }

        public System.Threading.Tasks.Task Task => System.Threading.Tasks.Task.CompletedTask;
        public bool Succeeded => _succeeded;
        public string LastError => _lastError;
        public UnityEngine.Object AssetObject => _asset;

        public int ReleaseCount { get; private set; }

        public void Release()
        {
            ReleaseCount++;
            if (_releaseException != null)
            {
                throw _releaseException;
            }

            if (_asset != null && _asset is GameObject go)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }

    internal sealed class GateYooRawAssetHandle : IYooRawAssetHandle
    {
        private readonly System.Threading.Tasks.TaskCompletionSource<bool> _tcs = new();
        private readonly UnityEngine.Object _asset;

        public GateYooRawAssetHandle(UnityEngine.Object asset)
        {
            _asset = asset;
        }

        public System.Threading.Tasks.Task Task => _tcs.Task;
        public bool Succeeded => true;
        public string LastError => string.Empty;
        public UnityEngine.Object AssetObject => _asset;

        public int ReleaseCount { get; private set; }

        public void Release()
        {
            ReleaseCount++;
        }

        public void Open()
        {
            _tcs.TrySetResult(true);
        }
    }

    internal sealed class GateYooPackage : IYooAssetPackage
    {
        private readonly GateYooRawAssetHandle _rawHandle;
        private readonly UnityEngine.Object _syncAsset;

        public GateYooPackage(GateYooRawAssetHandle rawHandle, UnityEngine.Object syncAsset)
        {
            _rawHandle = rawHandle ?? throw new ArgumentNullException(nameof(rawHandle));
            _syncAsset = syncAsset;
        }

        public IYooAssetLoadHandle LoadGameObjectAsync(string location)
        {
            throw new NotImplementedException();
        }

        public IYooRawAssetHandle LoadRawAssetAsync(string location)
        {
            return _rawHandle;
        }

        public UnityEngine.Object LoadRawAssetSync(string location, Type assetType)
        {
            return _syncAsset;
        }
    }

    internal sealed class RetryYooPackage : IYooAssetPackage
    {
        private readonly UnityEngine.Object _successAsset;
        private int _rawAssetCallCount;

        public RetryYooPackage(UnityEngine.Object successAsset)
        {
            _successAsset = successAsset;
        }

        public int RawAssetCallCount => _rawAssetCallCount;

        public IYooAssetLoadHandle LoadGameObjectAsync(string location)
        {
            throw new NotImplementedException();
        }

        public IYooRawAssetHandle LoadRawAssetAsync(string location)
        {
            _rawAssetCallCount++;
            if (_rawAssetCallCount <= 1)
            {
                return new FakeYooRawAssetHandle(false, null, "first-attempt-failed", null);
            }

            return new FakeYooRawAssetHandle(true, _successAsset, string.Empty, null);
        }

        public UnityEngine.Object LoadRawAssetSync(string location, Type assetType)
        {
            return _successAsset;
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

        public IYooRawAssetHandle LoadRawAssetAsync(string location)
        {
            throw _exception;
        }

        public UnityEngine.Object LoadRawAssetSync(string location, Type assetType)
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

        public IYooRawAssetHandle LoadRawAssetAsync(string location)
        {
            return new TaskFaultYooRawAssetHandle(_exception);
        }

        public UnityEngine.Object LoadRawAssetSync(string location, Type assetType)
        {
            throw _exception;
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

    internal sealed class TaskFaultYooRawAssetHandle : IYooRawAssetHandle
    {
        private readonly Exception _exception;

        public TaskFaultYooRawAssetHandle(Exception exception)
        {
            _exception = exception;
        }

        public System.Threading.Tasks.Task Task => System.Threading.Tasks.Task.FromException(_exception);
        public bool Succeeded => false;
        public string LastError => "task-fault";
        public UnityEngine.Object AssetObject => null;
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

        public IYooRawAssetHandle LoadRawAssetAsync(string location)
        {
            return new PendingYooRawAssetHandle();
        }

        public UnityEngine.Object LoadRawAssetSync(string location, Type assetType)
        {
            return null;
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

    internal sealed class PendingYooRawAssetHandle : IYooRawAssetHandle
    {
        private readonly System.Threading.Tasks.TaskCompletionSource<bool> _completion = new();

        public System.Threading.Tasks.Task Task => _completion.Task;
        public bool Succeeded => false;
        public string LastError => string.Empty;
        public UnityEngine.Object AssetObject => null;

        public void Release()
        {
        }
    }
}
