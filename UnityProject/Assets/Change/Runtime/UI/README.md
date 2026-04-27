# Change.Runtime.UI

## Rules

1. Runtime UI does not include business decisions.
2. Views delegate to presenters/use-cases; business state changes go through CQRS.
3. Runtime reflection binding is forbidden (`System.Reflection`, `.GetType(`, `Type.GetType(`, `PropertyInfo`).
4. Window loading/unloading goes through `IUiAssetLoader`.

## Core flow

`WindowManager` -> `IWindowFactory` -> `IUiAssetLoader` -> view creation -> presenter orchestration.
