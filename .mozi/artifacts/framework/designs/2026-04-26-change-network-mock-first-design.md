# Change 网络层 Mock-First 设计（WebSocket/TCP + Protobuf，含数据生成层）

- Date: 2026-04-26
- Project: `UnityProject/Assets/Change`
- Status: Approved baseline (brainstorming)
- Scope: 先落地前端本地 Mock Server，支持业务独立开发；保留未来接入真实 WebSocket/TCP 的兼容路径

## 1. 设计结论

本方案采用 **Mock-First + 可替换传输层**：

1. 一期只启用本地内存通道 `LocalMockTransport`，不建立真实 socket 连接。
2. 业务层只依赖统一入口 `INetClient.Send(cmdId, req)`，不感知 mock/real。
3. 从一期开始预留 `cmdId` 粒度路由（未来支持同连接内消息级 mock/real 切换）。
4. 一期即包含 mock 数据生成层（参考 `mind-mock` 思路），不仅支持固定桩，也支持规则化/可复现数据生成。

## 2. 目标与非目标

### 2.1 目标

1. 客户端在无服务端条件下可完成核心业务开发与联调自测。
2. 协议统一为 `cmdId + protobuf payload`，未来接入 WS/TCP 不改业务接口。
3. 通过启动配置显式选择 `datasetId`，保证可测、可复现、可追踪。
4. 构建 fail-fast 守卫：缺失 handler、重复注册、编解码失败均立即报错。
5. 一期交付 mock 数据层：固定桩 + 规则化生成（seed 可控）。

### 2.2 非目标（一期不做）

1. 不实现真实 WebSocket/TCP 建连、重连、心跳与断线重放。
2. 不实现服务端主动推送自动编排（后续二期扩展）。
3. 不实现复杂会话状态机与跨域事务一致性模拟。
4. 不引入反射扫描或自动注入。

## 3. 分层与程序集归属

### 3.1 `Change.Framework`

仅放抽象契约，不依赖 Unity/第三方库：

- `INetClient`
- `ITransport`
- `IMessageCodec`
- `IRoutePolicy`
- `IMockDispatcher`（抽象）
- `IMockHandler`
- `IMockDataProvider`
- `IMockValueFactory`

### 3.2 `Change.Runtime`

放运行时实现与组装：

- `NetClient`
- `LocalMockTransport`
- `ProtobufCodec`
- `MockDispatcher`
- `MockRegistry`
- `RouteSelector`
- `MockDatasetRegistry`
- `MockDataValidator`
- `DeterministicRandom`
- 后续扩展：`WebSocketTransport` / `TcpTransport`

### 3.3 `GameScript`

放业务调用与启动配置：

- 业务仅调用 `INetClient.Send<TReq, TRes>(cmdId, req)`。
- 启动时显式传入 `datasetId`（例如 `dev-default`）。
- 不直接依赖 transport 或 codec 实现。

## 4. 核心组件设计

## 4.1 统一消息信封

定义 `ProtocolEnvelope`：

- `int CmdId`
- `int RequestId`
- `byte[] Payload`

作用：解耦业务、编解码、传输；后续 mock/real 可共用同一数据通道。

## 4.2 路由与传输

### `IRoutePolicy`

- 输入：`cmdId`
- 输出：`Mock | RealWebSocket | RealTcp`

一期默认策略：所有 `cmdId` -> `Mock`。

### `ITransport`

- 统一发送帧并返回响应帧。
- 一期实现 `LocalMockTransport`，内部直接调用 `IMockDispatcher`。
- 后续实现 `WebSocketTransport` 与 `TcpTransport` 时复用同接口。

## 4.3 Mock 分发与注册

### `MockRegistry`

- 显式注册 `cmdId -> IMockHandler`。
- 重复注册直接抛 `DuplicateMockRegistrationException`。

### `IMockHandler`

- 单一职责：处理单个或一组明确 `cmdId`。
- 不在 handler 内散落大块硬编码数据。
- 响应组装通过 `IMockDataProvider` 与 `IMockValueFactory` 完成。

## 4.4 Mock 数据层（一期新增）

### `IMockDataProvider`

- 提供按 `cmdId` 组织的数据模板或固定样本。
- 数据来源为 C# 静态定义（一期决策）。

### `IMockDataset`

- 表示一个可切换数据集（如 `dev-default`）。
- 由 `MockDatasetRegistry` 按 `datasetId` 显式注册与选择。

### `IMockValueFactory`

参考 `mind-mock` 思路，提供通用值生成能力（可复现）：

- `Bool/Int/Float/String/Guid/Time` 等基础生成
- 业务友好生成：`Name/IdLike/EnumPick/RangePick`（按项目需要最小集合）

### `DeterministicRandom`

- 输入 `seed`，保证同请求条件下可复现。
- 支持按 `datasetId + cmdId + requestId` 组合 seed，便于问题复盘。

### `MockTemplate<TReq, TRes>`（可选接口）

- 允许“固定桩 + 动态字段”组合。
- 示例：固定主结构，部分字段由 `IMockValueFactory` 生成。

## 5. 请求流转

1. 业务调用 `INetClient.Send(cmdId, req)`。
2. `IMessageCodec` 将请求 protobuf 编码为 `Payload`。
3. 构造 `ProtocolEnvelope(cmdId, requestId, payload)`。
4. `IRoutePolicy` 解析路由（一期默认 `Mock`）。
5. `LocalMockTransport` 转交 `IMockDispatcher`。
6. `IMockHandler` 通过 `IMockDataProvider`/`IMockValueFactory` 生成响应。
7. 响应 envelope 返回后解码为 `TRes`，交还业务层。

## 6. 错误模型与守卫

1. `MockHandlerNotFound(cmdId)`：未注册处理器，立即失败。
2. `DuplicateMockRegistration(cmdId)`：启动注册阶段即失败。
3. `DatasetNotFound(datasetId)`：启动配置非法，立即失败。
4. `MockDataInvalid(cmdId, reason)`：模板字段缺失或结构非法。
5. `CodecEncodeFailed/CodecDecodeFailed(cmdId, requestId)`：协议错误保留上下文。
6. `UnsupportedRoute(cmdId, route)`：路由命中未启用通道时失败。

## 7. 配置与启动

1. `GameScript` 启动参数显式传入 `datasetId`。
2. `Runtime` 根据 `datasetId` 装配 `IMockDataProvider`。
3. 执行 `MockDataValidator`：
   - `cmdId` 覆盖校验
   - 重复键校验
   - 模板结构校验
4. 校验通过后开放业务请求入口。

## 8. 测试与验收标准

### 8.1 功能测试

1. 至少 2~3 个核心 `cmdId` 跑通“请求 -> 响应 -> UI 刷新”闭环。
2. 不依赖服务端进程，纯客户端可运行。

### 8.2 架构守卫测试

1. 业务代码不直接依赖 `LocalMockTransport`。
2. 切换 `datasetId` 不修改业务调用代码。
3. `cmdId` 未注册时必定报错，不允许静默默认值。

### 8.3 数据层测试

1. 固定桩返回稳定一致。
2. 同 seed 下动态字段可复现。
3. 不同 seed 下动态字段可变化。
4. 数据模板缺字段时启动失败。

## 9. 一期交付清单

1. 抽象层：`INetClient`、`ITransport`、`IMessageCodec`、`IRoutePolicy`。
2. 运行时：`LocalMockTransport`、`MockDispatcher`、`MockRegistry`、`ProtobufCodec`。
3. 数据层：`IMockDataProvider`、`IMockDataset`、`IMockValueFactory`、`DeterministicRandom`、`MockDataValidator`。
4. 启动配置：`datasetId` 显式注入与校验。
5. 用例：2~3 个关键 `cmdId` 的 mock 样例与测试。

## 10. 二期扩展路径（与一期兼容）

1. 新增 `WebSocketTransport` 与 `TcpTransport` 实现。
2. 启用 `IRoutePolicy` 的 `cmdId` 级混合路由（mock/real 并行）。
3. 补充推送、重连、心跳与网络抖动模拟。
4. 从固定桩逐步升级到状态化场景脚本，无需改业务入口。
