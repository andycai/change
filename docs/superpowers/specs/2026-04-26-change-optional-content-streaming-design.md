# Change 可选资源边玩边下载设计（MVP）

- Date: 2026-04-26
- Project: `UnityProject/Assets/Change`
- Scope: 基于 UniTask + YooAsset 设计可选资源边玩边下载机制，要求与 FairyGUI 解耦，仅预留 UI 接入接口
- Status: Draft (approved in session, pending written spec review)

## 1. 结论与设计立场

本设计采用分层编排方案（CatalogSync + PolicyEngine + Scheduler + DownloadExecutor + StateStore/EventHub），在 `Change.Runtime` 新增独立下载域 `ContentStreaming`。

设计立场：

1. 首版仅覆盖可选资源下载，不阻塞主玩法路径。
2. 运行策略以“体验优先”为核心：高压玩法状态下自动降速或暂停。
3. UI 完全解耦：FairyGUI 只通过事件接口订阅状态，不依赖 YooAsset 类型。
4. 服务端驱动清单，客户端负责执行与本地治理。

## 2. 输入约束与目标

## 2.1 已确认输入约束

1. 下载对象：可选资源（如皮肤/语音/高精资源）。
2. 网络策略：Wi-Fi 自动下载；移动网络自动下载但限速，并受日流量预算约束。
3. 清单来源：服务端下发。
4. 玩法感知：高压状态自动降速/暂停，空闲后恢复。
5. UI 集成：事件流订阅模式，不耦合 FairyGUI。
6. 缓存治理：按版本/活动有效期自动清理，可叠加容量兜底。

## 2.2 目标

1. 建立可测试、可演进的下载域架构，避免单类膨胀。
2. 在边玩边下载场景下最小化对帧稳定性的影响。
3. 提供清晰稳定的服务层与事件层接口，支持 UI 可替换。
4. 支持失败恢复、可观测与灰度上线。

## 2.3 非目标（MVP 不做）

1. 不覆盖热更代码下载流程（仅资源可选包）。
2. 不构建事件溯源持久化全链路审计系统。
3. 不引入 UI 框架侧自动绑定能力。
4. 不在 MVP 做复杂多源 CDN 智能切换。

## 3. 方案比较与选型

## 3.1 备选方案

1. 单体 DownloadManager：
   - 优点：实现快、文件少。
   - 缺点：策略/执行/状态/通知容易耦合，演进成本高。
2. 分层编排（选中）：
   - 优点：职责清晰、易测、可替换，天然支持 UI 解耦。
   - 缺点：首版代码量高于单体方案。
3. FSM + 事件溯源重架构：
   - 优点：可观测与恢复能力最强。
   - 缺点：当前阶段过重，交付周期和维护成本高。

## 3.2 选型结论

选用分层编排方案，满足当前可交付性与未来扩展性平衡。

## 4. 分层架构与模块边界

模块建议落位：`Change.Runtime.ContentStreaming`。

## 4.1 CatalogSync

职责：

1. 拉取并校验服务端可选资源清单（支持版本号/ETag 增量语义）。
2. 产出声明式目标集合（可下载包、优先级、有效期、依赖）。

边界：

- 不做下载执行。
- 不做 UI 推送策略判断。

## 4.2 PolicyEngine

职责：

1. 汇总网络状态、流量预算、玩法高压状态，产出策略快照。
2. 输出自动下载开关、限速值、并发上限、暂停原因。

边界：

- 不调用 YooAsset。
- 不直接操作任务队列。

## 4.3 Scheduler

职责：

1. 将清单目标与本地缓存差异转化为任务队列。
2. 管理优先级、依赖关系、并发槽位、重试退避。
3. 在策略变化时执行动态重排。

边界：

- 不负责具体下载 IO。

## 4.4 DownloadExecutor

职责：

1. 适配 YooAsset 下载执行能力。
2. 监听进度、完成、失败并做错误类型映射。
3. 执行校验与句柄释放。

边界：

- 不包含业务策略判断。
- 不直接向 UI 发布事件。

## 4.5 StateStore + EventHub

职责：

1. 统一维护任务与全局策略状态快照。
2. 对外发布 UI 可订阅事件流。

边界：

- 仅发布 DTO 级事件，不泄漏底层下载句柄类型。

## 5. 数据模型与状态机

## 5.1 核心数据模型

1. `ContentPackDefinition`
   - `PackId`, `Version`, `SizeBytes`, `Priority`, `Dependencies`, `ExpireAt`, `NetworkRequirement`.
2. `DownloadTask`
   - `PackId`, `State`, `BytesDownloaded`, `BytesTotal`, `RateKbps`, `RetryCount`, `LastErrorCode`.
3. `DownloadPolicySnapshot`
   - `NetworkType`, `AllowAutoDownload`, `RateLimitKbps`, `MaxConcurrent`, `PauseReason`, `DailyBudgetRemaining`.
4. `ContentCacheRecord`
   - `PackId`, `InstalledVersion`, `LastAccessAt`, `ExpireAt`, `SourceCampaign`.

## 5.2 单任务状态机

主路径：

`Pending -> Queued -> Downloading -> Verifying -> Completed`

分支路径：

1. `Downloading -> Paused -> Queued`
2. `Downloading/Verifying -> FailedTransient`（可重试）
3. `Downloading/Verifying -> FailedTerminal`（不可重试）
4. 中间态可进入 `Canceled`

## 5.3 事件契约

1. `TaskAdded`
2. `TaskStateChanged`
3. `TaskProgressChanged`
4. `TaskRemoved`
5. `GlobalPolicyChanged`
6. `CatalogUpdated`

约束：

1. 同任务状态事件使用单调序号，避免乱序展示。
2. 进度事件节流（建议 200ms 级）降低 UI 和 GC 压力。
3. 慢订阅者不反压下载主流程（缓冲或丢旧策略）。

## 6. 端到端流程

1. `CatalogSync` 拉取并更新可选资源清单。
2. `PolicyEngine` 根据网络/预算/高压信号生成策略快照。
3. `Scheduler` 结合清单与本地缓存生成和重排任务。
4. `DownloadExecutor` 调用 YooAsset 执行下载与校验。
5. `StateStore` 更新状态；`EventHub` 发布事件供 UI 订阅。
6. 缓存治理按 TTL（版本/活动有效期）在空闲窗口执行清理。

## 7. 失败恢复、重试与降级

## 7.1 错误分层

1. `Transient`：网络抖动、超时、短时 5xx 等，进入重试流程。
2. `Terminal`：签名非法、配置不兼容、磁盘不可写、持续校验失败等，直接终态失败。

## 7.2 重试与恢复

1. 指数退避重试（如 2s/5s/10s/30s，默认上限 4 次，支持配置）。
2. 网络切换到 Wi-Fi 可触发提前重试。
3. 冷启动从 `StateStore` 恢复未完成任务，并先与最新清单对齐。

## 7.3 降级策略

1. 高压玩法状态：降并发、降速，必要时暂停。
2. 预算耗尽：停止自动推进，保留队列状态供后续恢复。
3. 下载域失败不阻塞核心玩法路径。

## 8. 接口契约（UI 解耦）

## 8.1 业务服务接口（GameScript 侧）

`IContentDownloadService`：

1. `SyncCatalogAsync()`
2. `EnqueuePack(packId)`
3. `PauseAll(reason)`
4. `ResumeByPolicy()`
5. `RemovePack(packId, removeCache)`
6. `GetPackState(packId)`

## 8.2 UI 事件接口（FairyGUI 侧）

`IContentDownloadEvents`：

1. `TaskStateChanged`
2. `TaskProgressChanged`
3. `GlobalPolicyChanged`
4. `CatalogUpdated`

约束：

1. 仅暴露 DTO（`packId/status/progress/rate/errorCode`）。
2. 默认主线程派发，便于 FairyGUI 消费。
3. UI 只订阅与渲染，不执行下载策略判断。

## 8.3 运行时依赖抽象

1. `INetworkStateProvider`
2. `IPlayPressureSignal`
3. `IDataBudgetProvider`
4. `IClock`

## 9. 测试与验收标准

## 9.1 功能验收

1. 清单同步、增量更新与失效规则生效。
2. Wi-Fi 自动下载、移动网络限速与预算约束生效。
3. 高压状态触发降速/暂停，退出高压后恢复。
4. UI 通过事件流可完整展示下载态。

## 9.2 稳定性验收

1. 临时错误重试可恢复，终态错误可落库并可观测。
2. 冷启动恢复不中断、不重复下载已完成内容。
3. TTL 清理不影响主玩法可进入性。

## 9.3 性能验收

1. 高压状态下下载模块不显著拉高帧尖峰（关注 P95）。
2. 进度节流后 UI 无明显抖动与持续 GC 走高。
3. 调度重排与状态更新开销可控。

## 10. 分阶段上线建议

1. Phase 1：Wi-Fi 自动下载 + 基础事件接口，先灰度。
2. Phase 2：开启移动网络限速自动下载 + 日预算治理。
3. Phase 3：开启高压感知策略 + TTL 自动清理全量规则。
4. 每阶段配套开关配置、观测阈值与回滚路径。

## 11. 风险与缓解

1. 风险：高压状态识别不准确导致体验波动。
   - 缓解：高压信号抽象化 + 观测调参 + 灰度开关。
2. 风险：服务端清单异常导致下载目标错误。
   - 缓解：签名/版本校验 + 快速禁用开关 + 本地兜底策略。
3. 风险：事件频率过高影响 UI 平滑度。
   - 缓解：节流、批量合并、订阅端限频渲染。
