using System;
using System.Collections.Generic;
using System.Threading;
using Change.Framework.UI;
using Change.Runtime.UI.Core;
using Cysharp.Threading.Tasks;
using FairyGUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace Change.Runtime.UI.Tests
{
    public class FairyGuiWindowFactoryTests
    {
        [UnityTest]
        public IEnumerator CreateAsync_MetadataExists_PackageNotYetLoaded_CallsLoadPackageAsync()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var registry = new WindowRegistry();
                var windowId = new WindowId("Inventory");
                registry.Register(windowId, "InventoryPkg", "InventoryComp", "Main", WindowLayer.Normal);

                var loader = new FakeTrackableAssetLoader();
                var resolver = new FakeLocationResolver();

                var factory = new FairyGuiWindowFactory(loader, resolver, registry);

                var request = new WindowRequest(windowId, WindowOpenOptions.Default);
                var view = await factory.CreateAsync(request, CancellationToken.None);

                Assert.NotNull(view, "View should be created.");
                Assert.AreEqual(1, loader.LoadPackageCallCount, "LoadPackageAsync should be called once.");
                Assert.AreEqual("InventoryPkg", loader.LastLoadedPackage, "Should load the correct package.");

                view.Dispose();
            });
        }

        [UnityTest]
        public IEnumerator CreateAsync_MetadataExists_PackageAlreadyLoaded_DoesNotCallLoadPackageAsyncAgain()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var registry = new WindowRegistry();
                var windowId = new WindowId("Inventory");
                registry.Register(windowId, "InventoryPkg", "InventoryComp", "Main", WindowLayer.Normal);

                var loader = new FakeTrackableAssetLoader();
                var resolver = new FakeLocationResolver();

                var factory = new FairyGuiWindowFactory(loader, resolver, registry);

                var request = new WindowRequest(windowId, WindowOpenOptions.Default);

                // First call: should load the package
                var view1 = await factory.CreateAsync(request, CancellationToken.None);
                Assert.AreEqual(1, loader.LoadPackageCallCount, "First CreateAsync should call LoadPackageAsync.");

                view1.Dispose();

                // Second call: package already loaded, should not load again
                var view2 = await factory.CreateAsync(request, CancellationToken.None);
                Assert.AreEqual(1, loader.LoadPackageCallCount, "Second CreateAsync should NOT call LoadPackageAsync again.");
                Assert.NotNull(view2, "Second view should be created.");

                view2.Dispose();
            });
        }

        [UnityTest]
        public IEnumerator CreateAsync_RegistryIsNull_DoesNotCallLoadPackageAsync()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var loader = new FakeTrackableAssetLoader();
                var resolver = new FakeLocationResolver();

                var factory = new FairyGuiWindowFactory(loader, resolver, registry: null);

                var request = new WindowRequest(new WindowId("AnyWindow"), WindowOpenOptions.Default);
                var view = await factory.CreateAsync(request, CancellationToken.None);

                Assert.NotNull(view, "View should be created even without registry.");
                Assert.AreEqual(0, loader.LoadPackageCallCount, "LoadPackageAsync should not be called when registry is null.");

                view.Dispose();
            });
        }

        [UnityTest]
        public IEnumerator CreateAsync_IdNotRegistered_DoesNotCallLoadPackageAsync()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var registry = new WindowRegistry();
                var loader = new FakeTrackableAssetLoader();
                var resolver = new FakeLocationResolver();

                var factory = new FairyGuiWindowFactory(loader, resolver, registry);

                var request = new WindowRequest(new WindowId("Unregistered"), WindowOpenOptions.Default);
                var view = await factory.CreateAsync(request, CancellationToken.None);

                Assert.NotNull(view, "View should be created even when window is not registered.");
                Assert.AreEqual(0, loader.LoadPackageCallCount, "LoadPackageAsync should not be called for unregistered window id.");

                view.Dispose();
            });
        }

        [UnityTest]
        public IEnumerator CreateAsync_MetadataExists_DifferentPackages_LoadsEachOnce()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var registry = new WindowRegistry();
                var invId = new WindowId("Inventory");
                var questId = new WindowId("Quest");

                registry.Register(invId, "InventoryPkg", "InventoryComp", "Main", WindowLayer.Normal);
                registry.Register(questId, "QuestPkg", "QuestComp", "Main", WindowLayer.Normal);

                var loader = new FakeTrackableAssetLoader();
                var resolver = new FakeLocationResolver();

                var factory = new FairyGuiWindowFactory(loader, resolver, registry);

                var view1 = await factory.CreateAsync(new WindowRequest(invId, WindowOpenOptions.Default), CancellationToken.None);
                Assert.AreEqual(1, loader.LoadPackageCallCount);
                Assert.AreEqual("InventoryPkg", loader.LastLoadedPackage);

                view1.Dispose();

                var view2 = await factory.CreateAsync(new WindowRequest(questId, WindowOpenOptions.Default), CancellationToken.None);
                Assert.AreEqual(2, loader.LoadPackageCallCount);
                Assert.AreEqual("QuestPkg", loader.LastLoadedPackage);

                view2.Dispose();
            });
        }

        [UnityTest]
        public IEnumerator CreateAsync_LoadPackageAsyncThrows_ExceptionPropagates()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var registry = new WindowRegistry();
                var windowId = new WindowId("Broken");
                registry.Register(windowId, "BrokenPkg", "BrokenComp", "Main", WindowLayer.Normal);

                var rootCause = new Exception("package-load-failed");
                var loader = new FakeTrackableAssetLoader(throwFromLoadPackage: rootCause);
                var resolver = new FakeLocationResolver();

                var factory = new FairyGuiWindowFactory(loader, resolver, registry);

                var request = new WindowRequest(windowId, WindowOpenOptions.Default);

                try
                {
                    await factory.CreateAsync(request, CancellationToken.None);
                    Assert.Fail("Expected exception when LoadPackageAsync throws.");
                }
                catch (Exception ex)
                {
                    Assert.AreSame(rootCause, ex, "The thrown exception should propagate from LoadPackageAsync.");
                }
            });
        }

        [UnityTest]
        public IEnumerator CreateAsync_TwoWindowsShareSamePackage_LoadsPackageOnlyOnce()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var registry = new WindowRegistry();
                var windowA = new WindowId("WindowA");
                var windowB = new WindowId("WindowB");

                // Both windows share the same UIPackage "SharedPkg".
                registry.Register(windowA, "SharedPkg", "CompA", "Main", WindowLayer.Normal);
                registry.Register(windowB, "SharedPkg", "CompB", "Main", WindowLayer.Normal);

                // Use a dedup-aware loader that mimics YooUiAssetLoader's
                // UIPackage.GetById check: once a package is loaded, subsequent
                // LoadPackageAsync calls are no-ops.
                var loader = new FakeDedupAssetLoader();
                var resolver = new FakeLocationResolver();

                var factory = new FairyGuiWindowFactory(loader, resolver, registry);

                var viewA = await factory.CreateAsync(
                    new WindowRequest(windowA, WindowOpenOptions.Default), CancellationToken.None);
                Assert.NotNull(viewA);
                Assert.AreEqual(1, loader.LoadPackageCallCount,
                    "First window should trigger LoadPackageAsync.");
                Assert.AreEqual("SharedPkg", loader.LastLoadedPackage);

                viewA.Dispose();

                var viewB = await factory.CreateAsync(
                    new WindowRequest(windowB, WindowOpenOptions.Default), CancellationToken.None);
                Assert.NotNull(viewB);
                Assert.AreEqual(1, loader.LoadPackageCallCount,
                    "Second window sharing the same package must NOT call LoadPackageAsync again.");

                viewB.Dispose();
            });
        }
    }

    /// <summary>
    /// Fake IUiAssetLoader that tracks LoadPackageAsync calls and returns a simple GameObject for LoadPrefabAsync.
    /// </summary>
    internal sealed class FakeTrackableAssetLoader : IUiAssetLoader
    {
        private readonly Exception _throwFromLoadPackage;
        private readonly List<string> _loadedPackages = new();

        public FakeTrackableAssetLoader(Exception throwFromLoadPackage = null)
        {
            _throwFromLoadPackage = throwFromLoadPackage;
        }

        public int LoadPackageCallCount { get; private set; }
        public string LastLoadedPackage { get; private set; }
        public IReadOnlyList<string> LoadedPackages => _loadedPackages;

        public UniTask LoadPackageAsync(string packageName, CancellationToken cancellationToken)
        {
            LoadPackageCallCount++;
            LastLoadedPackage = packageName;
            _loadedPackages.Add(packageName);

            if (_throwFromLoadPackage != null)
            {
                return UniTask.FromException(_throwFromLoadPackage);
            }

            return UniTask.CompletedTask;
        }

        public void UnloadPackage(string packageName)
        {
        }

        public UniTask<UiAssetLease> LoadPrefabAsync(WindowId windowId, string location, CancellationToken cancellationToken)
        {
            var go = new GameObject($"prefab:{windowId}");
            go.AddComponent<FakeWindowRootSource>();
            var lease = new UiAssetLease(go, () => { if (go != null) UnityEngine.Object.DestroyImmediate(go); });
            return UniTask.FromResult(lease);
        }
    }

    /// <summary>
    /// Simple MonoBehaviour that provides a GComponent root for the factory.
    /// </summary>
    internal sealed class FakeWindowRootSource : MonoBehaviour, IFairyGuiWindowRootSource
    {
        private GComponent _root;

        public GComponent GetWindowRoot()
        {
            if (_root == null)
            {
                _root = new GComponent();
            }
            return _root;
        }

        private void OnDestroy()
        {
            if (_root != null)
            {
                _root.Dispose();
                _root = null;
            }
        }
    }

    /// <summary>
    /// Simple IWindowLocationResolver for tests.
    /// </summary>
    internal sealed class FakeLocationResolver : IWindowLocationResolver
    {
        public string ResolvePrefabLocation(WindowId id)
        {
            return $"fake/location/{id.Value}.prefab";
        }
    }

    /// <summary>
    /// Dedup-aware fake loader that tracks loaded packages and skips LoadPackageAsync
    /// when the package has already been loaded — mimicking YooUiAssetLoader's
    /// UIPackage.GetById early-return behaviour.
    /// </summary>
    internal sealed class FakeDedupAssetLoader : IUiAssetLoader
    {
        private readonly HashSet<string> _loadedPackages = new();

        public int LoadPackageCallCount { get; private set; }
        public string LastLoadedPackage { get; private set; }

        public UniTask LoadPackageAsync(string packageName, CancellationToken cancellationToken)
        {
            if (_loadedPackages.Contains(packageName))
            {
                return UniTask.CompletedTask;
            }

            LoadPackageCallCount++;
            LastLoadedPackage = packageName;
            _loadedPackages.Add(packageName);
            return UniTask.CompletedTask;
        }

        public void UnloadPackage(string packageName)
        {
            _loadedPackages.Remove(packageName);
        }

        public UniTask<UiAssetLease> LoadPrefabAsync(WindowId windowId, string location, CancellationToken cancellationToken)
        {
            var go = new GameObject($"prefab:{windowId}");
            go.AddComponent<FakeWindowRootSource>();
            var lease = new UiAssetLease(go, () => { if (go != null) UnityEngine.Object.DestroyImmediate(go); });
            return UniTask.FromResult(lease);
        }
    }
}
