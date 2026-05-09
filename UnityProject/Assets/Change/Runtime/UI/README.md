# Change.Runtime.UI

## Rules

1. Runtime UI does not include business decisions.
2. Views delegate to presenters/use-cases; business state changes go through CQRS.
3. Runtime reflection binding is forbidden (`System.Reflection`, `.GetType(`, `Type.GetType(`, `PropertyInfo`).
4. Window loading/unloading goes through `IUiAssetLoader`.

## Core flow

`WindowManager` -> `IWindowFactory` -> `IUiAssetLoader` -> view creation -> presenter orchestration.

## Presenter host (optional)

`WindowManager` accepts an `IWindowPresenterHost` (default `NullWindowPresenterHost`). After a view is created and shown, the host may attach an `IPresenter` and an optional per-window `IDisposable` scope (**strategy C** in the shell spec: simple windows use a transient presenter only; complex windows should create a child VContainer scope in the host and dispose it when the window closes). `WindowManager` calls `IPresenter.OnOpen` / `OnClose` in a fixed order with scope disposal; see `docs/superpowers/specs/2026-05-09-change-client-shell-composition-and-channel-design.md`.

Composition (`Root` / `GameRoot`, `INetworkGateway`, `IAppEventBus`) lives under `Change.Runtime.Composition` and `Change.Runtime.Net` / `App/Events` — wire these from VContainer in `Change.Runtime` / `GameScript`, not in `Change.Framework`.
