# Change.Runtime.ContentStreaming

## Purpose

Runtime module for optional-content background downloading with policy-based scheduling.

## Boundaries

- Runtime owns: catalog sync, policy decision, queueing, adapter execution, and state/event publication.
- UI owns: rendering and user interaction only (subscribe to DTO events).
- Game logic owns: enqueue/remove commands via `IContentDownloadService`.

## Key Interfaces

- `IContentDownloadService`: command/query facade for gameplay layer.
- `IContentDownloadEvents`: DTO event stream for FairyGUI or other presentation layers.
- `IAssetDownloadAdapter`: YooAsset seam; swap implementation without touching policy or scheduler.

## MVP Policy Defaults

- Wi-Fi auto download: enabled.
- Cellular auto download: enabled with rate limit.
- High pressure: pause auto dequeue.
- Daily budget exhausted: pause auto dequeue.

## Testing

- EditMode tests cover contracts, policy, scheduler, state/event hub, catalog/cleaner, and orchestrator flows.
- PlayMode smoke test compiles and executes orchestrator lifecycle in Unity's test runtime.
