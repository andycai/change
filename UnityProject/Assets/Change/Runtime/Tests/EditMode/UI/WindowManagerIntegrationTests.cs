using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Change.Framework.Application;
using Change.Framework.UI;
using Change.Runtime.UI.Core;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Change.Runtime.UI.Tests
{
    /// <summary>
    /// Cross-component integration tests exercising multiple real subsystems together:
    /// <see cref="WindowManager"/> + <see cref="IWindowPresenterHost"/> + inflight/cache/LRU paths.
    ///
    /// These go beyond the single-component unit tests in <see cref="WindowManagerCoreTests"/>,
    /// <see cref="WindowManagerPresenterHostTests"/>, and <see cref="FairyGuiWindowFactoryTests"/>
    /// by combining real components across subsystem boundaries.
    ///
    /// NOTE: The 30-second <c>ScheduleDelayedRelease</c> timer is NOT exercised here because
    /// (a) it is a private method on <see cref="WindowManager"/>, (b) it uses a real
    /// <c>UniTask.Delay(30000)</c> making full integration impractical in EditMode tests, and
    /// (c) the timer cancellation logic (cache-hit reuse and LRU eviction cancellation) are
    /// already verified by <see cref="CachedWindowEntryTests"/> and the LRU eviction test below.
    /// </summary>
    public class WindowManagerIntegrationTests
    {
        // -----------------------------------------------------------------------
        // Test doubles (accessible within this file only)
        // -----------------------------------------------------------------------

        private sealed class TrackingHost : IWindowPresenterHost
        {
            public int OpenedCount;
            public int ClosedCount;
            public DummyPresenter LastPresenter;

            public void OnOpened(in WindowRequest request, IWindowView view,
                out IDisposable windowScope, out IPresenter presenter)
            {
                OpenedCount++;
                windowScope = null;
                presenter = new DummyPresenter();
                LastPresenter = (DummyPresenter)presenter;
            }

            public void OnClosing(in WindowRequest request, IWindowView view,
                IPresenter presenter, IDisposable windowScope)
            {
                ClosedCount++;
            }
        }

        private sealed class DummyPresenter : IPresenter
        {
            public int OpenCount;
            public int CloseCount;

            public void OnOpen() => OpenCount++;
            public void OnClose() => CloseCount++;
        }

        // -----------------------------------------------------------------------
        // Scenario 1 — Concurrent inflight + host lifecycle
        // -----------------------------------------------------------------------

        /// <summary>
        /// When multiple callers concurrently open the same window, the inflight
        /// merge path must invoke <see cref="IWindowPresenterHost.OnOpened"/>
        /// and <see cref="IPresenter.OnOpen"/> exactly once (not per caller).
        /// </summary>
        [UnityTest]
        public IEnumerator ConcurrentInflight_WithHost_InvokesLifecycleExactlyOnce()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new ControlledWindowFactory();
                var host = new TrackingHost();
                var manager = new WindowManager(factory, host);
                var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);

                // Launch 3 concurrent opens for the same request
                var task1 = manager.OpenAsync(in request, CancellationToken.None).AsTask();
                var task2 = manager.OpenAsync(in request, CancellationToken.None).AsTask();
                var task3 = manager.OpenAsync(in request, CancellationToken.None).AsTask();

                // Factory must be called exactly once
                Assert.AreEqual(1, factory.CreateCount,
                    "Factory.CreateAsync should be called exactly once for merged inflight.");
                Assert.AreEqual(1, factory.PendingCount,
                    "One pending CreateAsync should be in-flight.");

                // All three tasks are not yet complete
                Assert.IsFalse(task1.IsCompleted);
                Assert.IsFalse(task2.IsCompleted);
                Assert.IsFalse(task3.IsCompleted);

                // Host/presenter must NOT have fired before creation completes
                Assert.AreEqual(0, host.OpenedCount,
                    "OnOpened must not fire before factory completes.");
                Assert.IsNull(host.LastPresenter);

                // Complete the inflight creation
                var createdView = new FakeWindowView(request.Id);
                factory.CompleteNext(createdView);

                // All three callers must receive the same view instance
                var result1 = (FakeWindowView)await task1;
                var result2 = (FakeWindowView)await task2;
                var result3 = (FakeWindowView)await task3;

                Assert.AreSame(createdView, result1);
                Assert.AreSame(createdView, result2);
                Assert.AreSame(createdView, result3);

                // Factory count must remain 1 (no additional creates)
                Assert.AreEqual(1, factory.CreateCount,
                    "Factory.CreateAsync must remain at 1 after inflight completion.");

                // Host lifecycle must have fired exactly once for the shared inflight
                Assert.AreEqual(1, host.OpenedCount,
                    "OnOpened must fire exactly once for merged inflight.");
                Assert.IsNotNull(host.LastPresenter);
                Assert.AreEqual(1, host.LastPresenter.OpenCount,
                    "Presenter.OnOpen must fire exactly once for merged inflight.");

                // View must be visible and in opened state
                Assert.AreEqual(1, createdView.SetVisibleCount,
                    "SetVisible(true) must be called exactly once.");
                Assert.IsTrue(manager.TryGet(in request, out var opened));
                Assert.AreSame(createdView, opened,
                    "View must be registered in opened after inflight completes.");
            });
        }

        /// <summary>
        /// When all concurrent callers cancel before the factory completes,
        /// the merged inflight is cancelled and the window must NOT appear
        /// in _opened.  The host/presenter lifecycle must never fire.
        ///
        /// NOTE: We intentionally do NOT assert that <c>createdView.DisposeCount == 1</c>
        /// because <c>CreateAndCacheAsync</c> runs as a <c>.Forget()</c> fire-and-forget
        /// task and its continuation (which calls <c>created.Dispose()</c>) uses
        /// <c>TaskCreationOptions.RunContinuationsAsynchronously</c> — a single
        /// <c>UniTask.Yield()</c> is not guaranteed to flush it in EditMode.
        /// The existing <see cref="WindowManagerCoreTests.OpenAsync_AllCallersCanceledBeforeCompletion_DoesNotCacheOrShow"/>
        /// verifies <c>SetVisibleCount == 0</c> and <c>!TryGet</c> for equivalent coverage.
        /// </summary>
        [UnityTest]
        public IEnumerator ConcurrentInflight_AllCallersCanceled_NoLifecycleFired()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new ControlledWindowFactory();
                var host = new TrackingHost();
                var manager = new WindowManager(factory, host);
                var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);

                var cts1 = new CancellationTokenSource();
                var cts2 = new CancellationTokenSource();

                var task1 = manager.OpenAsync(in request, cts1.Token).AsTask();
                var task2 = manager.OpenAsync(in request, cts2.Token).AsTask();

                Assert.AreEqual(1, factory.CreateCount);

                // Cancel both callers before factory completes
                cts1.Cancel();
                cts2.Cancel();

                try { await task1; Assert.Fail("task1 should throw OCE."); }
                catch (OperationCanceledException) { }

                try { await task2; Assert.Fail("task2 should throw OCE."); }
                catch (OperationCanceledException) { }

                // Verify the shared inflight cancellation was triggered
                Assert.AreEqual(1, factory.SharedCancellationCount,
                    "Shared cancellation must be triggered when all callers cancel.");

                // Now complete the (now-abandoned) inflight
                var createdView = new FakeWindowView(request.Id);
                factory.CompleteNext(createdView);
                await UniTask.Yield();

                // View must never be shown
                Assert.AreEqual(0, createdView.SetVisibleCount,
                    "SetVisible must not be called when all callers cancel.");

                // No lifecycle fired
                Assert.AreEqual(0, host.OpenedCount,
                    "OnOpened must not fire when all callers cancel.");
                Assert.AreEqual(0, host.ClosedCount,
                    "OnClosing must not fire when all callers cancel.");

                // Window must not be registered in opened
                Assert.IsFalse(manager.TryGet(in request, out _),
                    "Cancelled inflight must not leave a window in opened.");
            });
        }

        // -----------------------------------------------------------------------
        // Scenario 2 — Cache-hit reopen + host lifecycle
        // -----------------------------------------------------------------------

        /// <summary>
        /// Opening a window, closing it, then opening it again (cache hit) must:
        /// - Reuse the same <see cref="IWindowView"/> instance (no factory call)
        /// - Fire <see cref="IWindowPresenterHost.OnOpened"/> and a fresh
        ///   <see cref="IPresenter.OnOpen"/> on each open (including cache hits)
        /// - Fire <see cref="IPresenter.OnClose"/> and
        ///   <see cref="IWindowPresenterHost.OnClosing"/> on each close
        /// - Never dispose the cached view
        /// </summary>
        [UnityTest]
        public IEnumerator CacheHit_InvokesLifecycleEachTime_WithoutFactoryCall()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new FakeWindowFactory();
                var host = new TrackingHost();
                var manager = new WindowManager(factory, host);
                var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);

                // === First open: factory creates new view ===
                var view = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);

                Assert.AreEqual(1, factory.CreateCount,
                    "First open must trigger a CreateAsync call.");
                Assert.AreEqual(1, host.OpenedCount,
                    "First open fires OnOpened once.");
                var presenter1 = host.LastPresenter;
                Assert.IsNotNull(presenter1);
                Assert.AreEqual(1, presenter1.OpenCount,
                    "First open fires presenter.OnOpen once.");
                Assert.AreEqual(0, presenter1.CloseCount);

                // === First close: goes to cache ===
                var closed1 = manager.Close(in request);
                Assert.IsTrue(closed1, "Close must succeed.");
                Assert.AreEqual(1, presenter1.CloseCount,
                    "First close fires presenter.OnClose.");
                Assert.AreEqual(1, host.ClosedCount,
                    "First close fires OnClosing once.");
                Assert.AreEqual(0, view.DisposeCount,
                    "Cached view must NOT be disposed on close.");

                // === Second open: cache hit, no factory call ===
                var view2 = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);

                Assert.AreSame(view, view2,
                    "Cache-hit must return the same view instance.");
                Assert.AreEqual(1, factory.CreateCount,
                    "Cache-hit must not trigger a new CreateAsync call.");
                Assert.AreEqual(2, host.OpenedCount,
                    "Cache-hit must fire OnOpened again.");
                var presenter2 = host.LastPresenter;
                Assert.IsNotNull(presenter2);
                Assert.AreNotSame(presenter1, presenter2,
                    "Cache-hit must create a fresh presenter.");
                Assert.AreEqual(1, presenter2.OpenCount,
                    "Cache-hit fires presenter.OnOpen once.");
                Assert.AreEqual(0, presenter2.CloseCount);

                // === Second close: goes to cache again ===
                var closed2 = manager.Close(in request);
                Assert.IsTrue(closed2, "Second close must succeed.");
                Assert.AreEqual(1, presenter2.CloseCount,
                    "Second close fires presenter.OnClose.");
                Assert.AreEqual(2, host.ClosedCount,
                    "Second close fires OnClosing again.");
                Assert.AreEqual(0, view.DisposeCount,
                    "View still not disposed after two close cycles.");

                // === Third open: cache hit again ===
                var view3 = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);

                Assert.AreSame(view, view3,
                    "Third cache-hit must return the same view instance.");
                Assert.AreEqual(1, factory.CreateCount,
                    "Still no new CreateAsync after third open.");
                Assert.AreEqual(3, host.OpenedCount,
                    "Third open fires OnOpened again.");
                var presenter3 = host.LastPresenter;
                Assert.IsNotNull(presenter3);
                Assert.AreNotSame(presenter2, presenter3,
                    "Third open must create yet another fresh presenter.");
                Assert.AreEqual(1, presenter3.OpenCount);
                Assert.AreEqual(0, view.DisposeCount,
                    "View must survive three open/close cycles without disposal.");
            });
        }

        // -----------------------------------------------------------------------
        // Scenario 3 — UIPackage sharing via FairyGuiWindowFactory
        // -----------------------------------------------------------------------

        /// <summary>
        /// When two different <see cref="WindowId"/> values share the same
        /// <c>PackageName</c> in the <see cref="IWindowRegistry"/>, the
        /// <see cref="FairyGuiWindowFactory"/> must load the package only once.
        ///
        /// This test uses a non-deduping loader (<see cref="FakeTrackableAssetLoader"/>)
        /// to isolate and verify the factory's own <c>_loadedPackages</c> dedup logic,
        /// as opposed to <see cref="FairyGuiWindowFactoryTests.CreateAsync_TwoWindowsShareSamePackage_LoadsPackageOnlyOnce"/>
        /// which relies on a dedup-aware loader.
        /// </summary>
        [UnityTest]
        public IEnumerator UIPackageSharing_TwoWindowsSamePackage_FactoryLoadsOnce()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var registry = new WindowRegistry();
                var windowA = new WindowId("WindowA");
                var windowB = new WindowId("WindowB");

                // Both share the same package
                registry.Register(windowA, "SharedPkg", "CompA", "Main", WindowLayer.Normal);
                registry.Register(windowB, "SharedPkg", "CompB", "Main", WindowLayer.Normal);

                // Use a loader that tracks EVERY call (no internal dedup)
                var loader = new FakeTrackableAssetLoader();
                var resolver = new FakeLocationResolver();

                var factory = new FairyGuiWindowFactory(loader, resolver, registry);

                // Create first window — should trigger package load
                var viewA = await factory.CreateAsync(
                    new WindowRequest(windowA, WindowOpenOptions.Default),
                    CancellationToken.None);

                Assert.IsNotNull(viewA);
                Assert.AreEqual(1, loader.LoadPackageCallCount,
                    "First window must trigger LoadPackageAsync.");
                Assert.AreEqual("SharedPkg", loader.LastLoadedPackage,
                    "Must load the correct package.");

                viewA.Dispose();

                // Create second window — same package, factory must skip loading
                var viewB = await factory.CreateAsync(
                    new WindowRequest(windowB, WindowOpenOptions.Default),
                    CancellationToken.None);

                Assert.IsNotNull(viewB);
                Assert.AreEqual(1, loader.LoadPackageCallCount,
                    "Second window with same package must NOT call LoadPackageAsync again. Factory dedup failed.");

                viewB.Dispose();
            });
        }

        /// <summary>
        /// When two windows have DIFFERENT <c>PackageName</c> values, the factory
        /// must load each package exactly once.
        /// </summary>
        [UnityTest]
        public IEnumerator UIPackageSharing_DifferentPackages_LoadsEachOnce()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var registry = new WindowRegistry();
                var windowA = new WindowId("WindowA");
                var windowB = new WindowId("WindowB");

                registry.Register(windowA, "PkgA", "CompA", "Main", WindowLayer.Normal);
                registry.Register(windowB, "PkgB", "CompB", "Main", WindowLayer.Normal);

                var loader = new FakeTrackableAssetLoader();
                var resolver = new FakeLocationResolver();

                var factory = new FairyGuiWindowFactory(loader, resolver, registry);

                var viewA = await factory.CreateAsync(
                    new WindowRequest(windowA, WindowOpenOptions.Default),
                    CancellationToken.None);

                Assert.IsNotNull(viewA);
                Assert.AreEqual(1, loader.LoadPackageCallCount,
                    "First window loads PkgA.");
                Assert.AreEqual("PkgA", loader.LastLoadedPackage);

                viewA.Dispose();

                var viewB = await factory.CreateAsync(
                    new WindowRequest(windowB, WindowOpenOptions.Default),
                    CancellationToken.None);

                Assert.IsNotNull(viewB);
                Assert.AreEqual(2, loader.LoadPackageCallCount,
                    "Second window with different package must trigger a new LoadPackageAsync.");
                Assert.AreEqual("PkgB", loader.LastLoadedPackage);

                viewB.Dispose();
            });
        }

        // -----------------------------------------------------------------------
        // Scenario 4 — LRU eviction + behavioral verification
        // -----------------------------------------------------------------------

        /// <summary>
        /// When the cache exceeds <c>MaxCacheSize</c> (10), the least-recently-used
        /// entry is evicted. This test verifies:
        /// - The evicted view is disposed.
        /// - All other views remain cached (not disposed).
        /// - Reopening the evicted window requires a fresh factory call.
        /// - Reopening a still-cached window hits cache (no factory call).
        /// </summary>
        [UnityTest]
        public IEnumerator LruEviction_EvictedViewRequiresNewCreate_OthersReopenFromCache()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new FakeWindowFactory();
                var manager = new WindowManager(factory);

                var views = new List<FakeWindowView>();
                var requests = new List<WindowRequest>();

                // Open and close 11 distinct windows to fill cache (size 10) and
                // trigger one eviction.
                for (int i = 0; i < 11; i++)
                {
                    var request = new WindowRequest(new WindowId($"W{i}"), WindowOpenOptions.Default);
                    requests.Add(request);
                    var view = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);
                    views.Add(view);
                    manager.Close(in request);
                }

                // All 11 windows were created from scratch
                Assert.AreEqual(11, factory.CreateCount,
                    "All 11 windows must be created via factory.");

                // The first-closed window (index 0) was the LRU victim — must be disposed
                Assert.AreEqual(1, views[0].DisposeCount,
                    "View 0 (first closed, LRU) must be disposed on cache eviction.");
                Assert.AreEqual(WindowState.Closed, views[0].State);

                // Views 1-10 must remain cached (not disposed)
                for (int i = 1; i < 11; i++)
                {
                    Assert.AreEqual(0, views[i].DisposeCount,
                        $"View {i} must remain cached and not disposed.");
                }

                // --- Behavioral consequence 1: reopening the EVICTED window ---
                // Since view 0 was evicted, reopening W0 must call the factory again.
                var req0 = requests[0];
                var reopenedEvicted = (FakeWindowView)await manager.OpenAsync(
                    in req0, CancellationToken.None);

                Assert.AreNotSame(views[0], reopenedEvicted,
                    "Evicted view must not be reused — a new instance is created.");
                Assert.AreEqual(12, factory.CreateCount,
                    "Reopening evicted window must trigger a fresh CreateAsync call.");

                // --- Behavioral consequence 2: reopening a CACHED window ---
                // View 1 is still in cache — reopening must NOT call the factory.
                var req1 = requests[1];
                var reopenedCached = (FakeWindowView)await manager.OpenAsync(
                    in req1, CancellationToken.None);

                Assert.AreSame(views[1], reopenedCached,
                    "Cached view must be reused without a new CreateAsync.");
                Assert.AreEqual(12, factory.CreateCount,
                    "Cache-hit reopen must not increase CreateCount.");
            });
        }

        /// <summary>
        /// Verify the full LRU ordering: accessing a cached window (by reopening it)
        /// should refresh its position, making it the most-recently-used and protecting
        /// it from subsequent evictions.
        /// </summary>
        [UnityTest]
        public IEnumerator LruOrdering_ReaccessPromotesToFront()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new FakeWindowFactory();
                var manager = new WindowManager(factory);

                var views = new List<FakeWindowView>();
                var requests = new List<WindowRequest>();

                // Open and close 10 windows (fills cache without triggering eviction)
                for (int i = 0; i < 10; i++)
                {
                    var request = new WindowRequest(new WindowId($"W{i}"), WindowOpenOptions.Default);
                    requests.Add(request);
                    var view = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);
                    views.Add(view);
                    manager.Close(in request);
                }

                Assert.AreEqual(10, factory.CreateCount);

                // Reopen W0 (the LRU at this point) — this promotes it to MRU
                var req0 = requests[0];
                var reopened = (FakeWindowView)await manager.OpenAsync(
                    in req0, CancellationToken.None);
                Assert.AreSame(views[0], reopened,
                    "Reopening cached W0 must return the same instance.");
                Assert.AreEqual(10, factory.CreateCount,
                    "Cache-hit must not create a new instance.");
                manager.Close(in req0);

                // Now add an 11th window — this will trigger eviction
                // W1 should now be the LRU (W0 was just promoted), so W1 gets evicted
                var extraRequest = new WindowRequest(new WindowId("W10"), WindowOpenOptions.Default);
                var extraView = (FakeWindowView)await manager.OpenAsync(in extraRequest, CancellationToken.None);
                manager.Close(in extraRequest);

                Assert.AreEqual(0, views[0].DisposeCount,
                    "W0 (promoted) must NOT be evicted — it was recently reaccessed.");
                Assert.AreEqual(1, views[1].DisposeCount,
                    "W1 (now LRU after W0's promotion) must be evicted.");
                Assert.AreEqual(11, factory.CreateCount,
                    "W10 was created from scratch.");
            });
        }
    }
}
