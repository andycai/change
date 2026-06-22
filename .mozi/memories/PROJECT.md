# Change（嫦娥）— Unity 游戏项目

## 项目概述

Change 是一个基于 HybridCLR 热更新架构的 Unity 手游项目，采用分层框架设计模式，业务无关的可复用基础设施位于 `Change/Framework` 下。

## 技术栈

| 组件 | 版本 | 用途 |
|------|------|------|
| Unity | 2022.3.60f1 (LTS) | 游戏引擎 |
| URP | 规划中（当前 Built-in RP） | 渲染管线 |
| HybridCLR | 8.9.0（嵌入式） | C# 热更新 |
| YooAsset | 2.3.18 | 资源管理与打包 |
| FairyGUI | 嵌入式 | UI 框架 |
| VContainer | 嵌入式（文件引用） | 依赖注入 / IoC 容器 |
| UniTask | 2.5.10 | Unity 异步/等待 |
| Obfuz | git | 代码混淆 |
| Obfuz4HybridCLR | git | Obfuz + HybridCLR 集成 |
| Nino | 4.0.0-preview.147 | 高性能序列化 |
| Luban | 规划中 | 配置/数据生成 |
| IngameDebugConsole | git | 游戏内调试控制台 |

## 项目结构

```
fun/
├── AGENTS.md                         # 代理初始化规则
├── .mozi/memories/                   # 项目记忆上下文
│   ├── GENERAL.md                    # 通用行为准则
│   ├── PROJECT.md                    # 本文件 - 项目技术文档
│   └── lessons/                      # 经验教训知识库
├── docs/
│   └── superpowers/
│       ├── specs/                    # 设计规格文档
│       └── plans/                    # 实施计划文档
└── UnityProject/                     # Unity 项目根目录
    ├── Assets/
    │   ├── Change/                   # 主游戏代码
    │   │   ├── Editor/               # 编辑器工具与扩展 (Change.Editor)
    │   │   ├── Framework/            # 业务无关框架层 (Change.Framework)
    │   │   │   ├── Application/      # 应用层抽象（IAppFacade, IPresenter, IUseCase）
    │   │   │   ├── Cqrs/             # CQRS 模块（命令/查询/事件）
    │   │   │   │   ├── Abstractions/ # 接口定义
    │   │   │   │   ├── Core/         # 核心实现
    │   │   │   │   ├── Exceptions/   # 异常类型
    │   │   │   │   ├── Monitoring/   # 性能监控
    │   │   │   │   └── Documentation/
    │   │   │   ├── Pooling/          # 对象池模块
    │   │   │   ├── Collections/      # 高性能集合模块
    │   │   │   │   ├── Abstractions/
    │   │   │   │   ├── Containers/   # FastDictionary, FastList, FastHashSet 等
    │   │   │   │   ├── Core/         # 核心枚举与守卫
    │   │   │   │   └── Diagnostics/  # 集合性能指标
    │   │   │   ├── Fsm/              # 有限状态机模块
    │   │   │   ├── Gas/              # Gameplay Ability System 框架抽象
    │   │   │   ├── Logging/          # 日志抽象（ILogger, ILogSink, LogRouter）
    │   │   │   ├── Network/          # 网络抽象层（IMessageCodec, IMockDispatcher, ITransport 等）
    │   │   │   │   ├── Abstractions/
    │   │   │   │   ├── Exceptions/
    │   │   │   │   └── Models/
    │   │   │   ├── UI/               # UI 框架抽象（WindowId, WindowLayer 等）
    │   │   │   │   └── Abstractions/
    │   │   │   ├── AssemblyInfo.cs   # InternalsVisibleTo 测试程序集
    │   │   │   └── Tests/EditMode/   # 框架 EditMode 测试
    │   │   │       ├── Application/
    │   │   │       ├── Collections/
    │   │   │       ├── Cqrs/
    │   │   │       ├── Fsm/
    │   │   │       ├── Gas/
    │   │   │       ├── Logging/
    │   │   │       ├── Network/
    │   │   │       └── Pooling/
    │   │   ├── Runtime/              # 游戏运行时代码 (Change.Runtime)
    │   │   │   ├── App/Events/       # 应用事件总线
    │   │   │   ├── Composition/      # DI 组合根（GameCompositionHost, EngineLifetimeScope）
    │   │   │   ├── ContentStreaming/ # 内容流式下载系统
    │   │   │   │   ├── Abstractions/ # 下载、时钟、网络状态接口
    │   │   │   │   ├── Catalog/      # 资源目录同步
    │   │   │   │   ├── Execution/    # 下载适配器（YooAsset 集成）
    │   │   │   │   ├── Maintenance/  # 缓存清理
    │   │   │   │   ├── Model/        # 数据模型
    │   │   │   │   ├── Policy/       # 下载策略引擎
    │   │   │   │   ├── Scheduling/   # 下载任务调度
    │   │   │   │   └── State/        # 下载状态管理
    │   │   │   ├── FrameBudget/      # 帧预算管理与帧调度
    │   │   │   ├── Gas/              # GAS 运行时实现
    │   │   │   │   ├── Core/         # 技能、属性、触发器核心
    │   │   │   │   ├── Cqrs/         # CQRS 消息定义
    │   │   │   │   ├── Data/         # 技能配置数据
    │   │   │   │   ├── Effects/      # 效果实现（伤害、治疗、Buff 等）
    │   │   │   │   └── Targeting/    # 目标选择器
    │   │   │   ├── Logging/          # 日志实现（UnityLogSink, FileLogSink）
    │   │   │   ├── Net/              # 网络网关抽象
    │   │   │   ├── Network/          # 网络实现层
    │   │   │   │   ├── Core/         # NetClient, TransportRouter
    │   │   │   │   ├── Mock/         # Mock 网络（MockDispatcher, LocalMockTransport）
    │   │   │   │   │   ├── Data/     # Mock 数据集与验证
    │   │   │   │   │   └── Samples/  # 示例 Mock 处理器
    │   │   │   │   └── Serialization/ # Protobuf 序列化适配
    │   │   │   ├── Timer/            # 定时器模块
    │   │   │   ├── UI/               # UI 运行时
    │   │   │   │   ├── Abstractions/ # UI 接口
    │   │   │   │   ├── Core/         # WindowManager 窗口管理核心
    │   │   │   │   ├── FairyGui/     # FairyGUI 窗口工厂与视图
    │   │   │   │   └── Loading/      # UI 资源加载（YooAsset 集成）
    │   │   │   ├── AssemblyInfo.cs
    │   │   │   └── Tests/            # 运行时测试
    │   │   │       ├── EditMode/     # Runtime EditMode 测试
    │   │   │       │   ├── Composition/
    │   │   │       │   ├── ContentStreaming/
    │   │   │       │   ├── FrameBudget/
    │   │   │       │   ├── GameFlow/
    │   │   │       │   ├── Gas/
    │   │   │       │   ├── Logging/
    │   │   │       │   ├── Network/
    │   │   │       │   └── UI/
    │   │   │       └── PlayMode/     # Runtime PlayMode 测试
    │   │   │           ├── ContentStreaming/
    │   │   │           ├── FrameBudget/
    │   │   │           ├── Quest/
    │   │   │           └── Timer/
    │   │   ├── YooSpaceShooter/      # YooAsset 示例配置
    │   │   └── QuestTest/            # Quest 测试预制体
    │   ├── GameScript/               # 游戏脚本（热更新程序集）
    │   │   ├── Composition/          # DI 安装器
    │   │   ├── GameFlow/             # 游戏流程状态机
    │   │   │   ├── Entry/            # 入口驱动
    │   │   │   ├── States/           # 状态定义（Boot, Login, Lobby, Match, BattleHost, Result）
    │   │   │   ├── BattleFlow/       # 战斗子流程状态机
    │   │   │   └── Orchestration/    # 流程编排
    │   │   ├── GasTemplate/          # GAS 模板系统
    │   │   │   ├── Entry/            # 模板运行入口
    │   │   │   └── Demo/             # 演示场景
    │   │   ├── UI/
    │   │   │   ├── Quest/            # 任务 UI 模块
    │   │   │   └── Inventory/        # 背包 UI 模块
    │   │   └── Tests/EditMode/       # 热更新程序集测试
    │   ├── Resources/                # Unity Resources 目录
    │   └── Samples/                  # 包示例（gitignore）
    ├── Packages/
    │   ├── manifest.json             # 包依赖清单
    │   ├── com.code-philosophy.hybridclr@8.9.0/  # HybridCLR（嵌入式）
    │   ├── com.fairygui.unity/                    # FairyGUI（嵌入式）
    │   └── jp.hadashikick.vcontainer/             # VContainer DI 框架（嵌入式）
    ├── Bundles/                      # AssetBundle 输出（gitignore）
    ├── yoo/                          # YooAsset 缓存（gitignore）
    └── ProjectSettings/
```

## 架构与设计规范

### 框架层（`Change/Framework`）

业务无关的可复用基础设施层（引擎无关，可用于任何 C# 环境）。核心原则：

1. **零外部依赖** — 框架代码不得依赖第三方库。
2. **业务无关** — 不与业务领域耦合；框架永不依赖业务程序集。
3. **热路径零 GC** — 运行时调度路径在预热后必须产生零托管内存分配。
4. **快速失败** — 配置错误（缺失注册、重复注册、Freeze 后修改）立即抛出异常。
5. **同步执行模型** — MVP 阶段仅支持同步；框架核心不使用 async/await。
6. **显式注册** — 禁止基于反射的自动扫描；处理器在启动阶段手动注册，随后调用 `Freeze()`。
7. **结构体消息** — Command/Query/Event 类型应为 `readonly struct`，避免装箱和分配。

### 框架模块一览

#### 核心模块
- **FSM** — 事件驱动的有限状态机，`IFsmState<TStateId, TEvent>`，单活跃状态，FIFO 事件队列，串行处理。
- **CQRS** — 命令/查询/职责分离，支持 struct 消息 + class 处理器，泛型强类型调度。支持同步与异步命令、池化 class 命令、自处理模式、性能监控。
- **高性能集合** — FastDictionary, FastList, FastHashSet, RingBuffer, FastPriorityQueue。
- **Pooling** — 对象池，零 GC 调度路径，支持严格安全检查与容量统计。
- **Logging** — 引擎无关的日志抽象（ILogger, ILogSink, LogRouter）。具体 Sink（UnityLogSink, FileLogSink）位于 `Change/Runtime/Logging`。

#### 网络模块
- **Network（Framework）** — 网络抽象接口：`IMessageCodec`（消息编解码）、`IMockDispatcher`（Mock 调度）、`ITransport`（传输层）、`INetClient`（网络客户端）、`IRoutePolicy`（路由策略）。
- **Network（Runtime）** — 实现层：`NetClient`、`TransportRouter`、Mock 系统（`MockDispatcher`, `LocalMockTransport`, `MockRegistry`）、Protobuf 序列化适配。
- **Net（Runtime）** — 网络网关抽象：`INetworkGateway`, `NetworkCommandEnvelope`。

#### Gameplay Ability System（GAS）
- **Gas（Framework）** — 引擎无关的 GAS 抽象：`IAbilitySystem`, `IGameplayAbility`, `IAttribute`, `IAttributeSet`, `IModifier`, `ITrigger`, `IGameplayEffect`, `ITargetResolver`, `IGameplayTagSet`。
- **Gas（Runtime）** — GAS 运行时实现：`AbilitySystem`, `GameplayAbility`, `Attribute`, `AttributeSet`, `Modifier`, `Trigger`, `TriggerEngine`。通过 CQRS 消息（`CastAbilityCmd`, `ApplyModifierCmd`, `DamageAppliedEvt` 等）驱动，支持多种效果（伤害、治疗、属性修改、Tag、冷却、消耗等）和目标选择（自身、敌方、友方、AoE）。

#### UI 模块
- **UI（Framework）** — 窗口管理抽象：`WindowId`, `WindowLayer`, `WindowState`, `WindowOpenOptions`。
- **UI（Runtime）** — `WindowManager` 窗口管理器，`FairyGuiWindowFactory`/`FairyGuiWindowView` FairyGUI 适配，`YooUiAssetLoader` YooAsset UI 资源加载，Presenter 模式支持。

#### 内容流式下载
- **ContentStreaming（Runtime）** — 完整的资源下载管理系统：`ContentDownloadOrchestrator`（下载编排器）、`DownloadTaskScheduler`（任务调度）、`ContentDownloadPolicyEngine`（策略引擎，基于网络类型与数据预算）、`ContentDownloadStateStore`/`ContentDownloadEventHub`（状态与事件管理）、`CatalogSyncService`（目录同步）、`CacheExpiryCleaner`（缓存清理）、`YooAssetDownloadAdapter`（YooAsset 适配器）。

#### 帧预算管理
- **FrameBudget（Runtime）** — `FrameWorkScheduler`（帧任务调度器，按优先级执行与延期）、`FrameBudgetDriver`（帧预算驱动，按阶段分配帧时间）、`FrameBudgetPolicy`（预算策略）、`FramePerfRecorder`（帧性能记录）、`FramePhase`（帧阶段定义）、`FrameTaskPriority`（任务优先级）。

#### 应用与组合
- **Application（Framework）** — 应用层模式抽象：`IAppFacade`, `IPresenter`, `IUseCase`。
- **Composition（Runtime）** — VContainer DI 组合根：`GameCompositionHost`（游戏组合宿主，管理 AOT 与热更新双通道注册）、`EngineLifetimeScope`（引擎生命周期作用域）、`IHotfixGameInstaller`（热更新安装器接口）。
- **App/Events（Runtime）** — 应用级事件总线：`IAppEventBus`, `InProcessAppEventBus`。

### 程序集定义

共 8 个程序集（6 个核心 + 2 个 GameScript）：

| 程序集 | 平台 | 用途 |
|--------|------|------|
| `Change.Framework` | Any | 引擎无关框架层 |
| `Change.Framework.EditModeTests` | Editor | 框架 EditMode 测试 |
| `Change.Runtime` | Any | 运行时代码（依赖 Framework） |
| `Change.Runtime.EditModeTests` | Editor | 运行时 EditMode 测试 |
| `Change.Runtime.PlayModeTests` | Standalone | 运行时 PlayMode 测试 |
| `Change.Editor` | Editor | 编辑器工具与扩展 |
| `GameScript` | Any | 热更新游戏脚本 |
| `GameScript.EditModeTests` | Editor | 热更新程序集 EditMode 测试 |

### 热更新架构（HybridCLR）

- AOT 程序集：框架和引擎级代码，编译进 App 包体
- 热更新程序集（`GameScript`）：业务逻辑，运行时通过 HybridCLR 加载
- Obfuz 对 AOT 和热更新 DLL 均提供代码混淆
- 菜单路径：`HybridCLR/` 热更新操作，`HybridCLR/ObfuzExtension/` 混淆操作
- VContainer 的 `GameCompositionHost` 支持 AOT 通道与热更新通道的双通道注册

### 依赖注入（VContainer）

- 嵌入式文件引用 `jp.hadashikick.vcontainer`
- AOT 侧：`EngineLifetimeScope` 注册框架层服务
- 热更新侧：`IHotfixGameInstaller` 接口，由 `GameScript/Composition/GameHotfixInstaller` 实现
- 双通道注册表共享同一容器，实现 AOT ↔ 热更新服务的无缝互联

### 资源管理（YooAsset）

- 资源收集与打包通过 YooAsset v2.3.18
- Bundle 输出目录：`UnityProject/Bundles/`（gitignore）
- 缓存目录：`UnityProject/yoo/`（gitignore）
- `YooSpaceShooter` 目录包含 YooAsset 资源收集器配置示例
- YooAsset 与 UI 加载集成：`YooUiAssetLoader` 通过 YooAsset 加载 FairyGUI 资源

### UI 系统（FairyGUI + WindowManager）

- UI 框架基于嵌入式 FairyGUI 包
- `WindowManager` 提供窗口生命周期管理（打开、关闭、层叠、排队）
- `FairyGuiWindowFactory` 将 FairyGUI 组件适配为 `IWindowView`
- 支持 Presenter 模式：每个窗口可绑定 Presenter 处理业务逻辑
- 编辑器工具位于 FairyGUI 菜单

## 编码规范

- **语言**：C#，目标 .NET Standard 2.1，兼容 Unity 2022.3
- **程序集定义**：8 个程序集（见上表）；`InternalsVisibleTo` 声明在 `AssemblyInfo.cs` 中，用于测试访问
- **命名空间约定**：每个程序集单一根命名空间 — `Change.Framework`, `Change.Runtime`, `Change.Editor`, `GameScript`
- **泛型约束**：对消息类型使用 `where T : struct` 强制值类型语义
- **`in` 关键字**：对 struct 参数使用 `in` 传递，避免拷贝开销
- **热路径禁止反射**：仅使用泛型强类型调度；运行时不使用基于 `object` 或反射的调度
- **异常策略**：框架使用 `InvalidOperationException` 表示编程错误；异常传播给调用者，不吞没

## 构建与工具说明

- Unity 可执行文件路径（本机）：`/Applications/Unity/Unity.app/Contents/MacOS/Unity`
- HybridCLR 菜单：`HybridCLR/CompileDll`, `GenerateAOTReference`, `GenerateLinkXml` 等
- Obfuz 菜单：`HybridCLR/ObfuzExtension/GenerateAll`, `CompileAndObfuscateDll`, `GeneratePolymorphicCodes`
- YooAsset：使用 YooAsset 编辑器窗口进行资源收集和 Bundle 构建
- FairyGUI：使用 FairyGUI 编辑器进行 UI 编辑，发布到 Unity 项目
- Luban：配置生成（待搭建）
- Unity Test Framework（`com.unity.test-framework@1.1.33`）CLI 运行：避免同时使用 `-quit` 和 `-runTests`，否则命令行测试参数可能不会执行；不带 `-quit` 运行以生成 XML 结果。
- 单元测试结果路径规则：始终将 `-testResults` 写入 `UnityProject/TestResults/` 下（例如 `UnityProject/TestResults/editmode-results.xml`），不要写入 `/tmp` 或其他目录。
- 单元测试结果命名规则：使用 `<suite>-<yyyyMMdd-HHmmss>.xml`（例如 `UnityProject/TestResults/editmode-cqrs-20260425-233000.xml`, `UnityProject/TestResults/playmode-runtime-20260425-233500.xml`）。

## 版本控制

- `.gitignore` 作用域为 `UnityProject/` 子目录
- 排除项：`Library/`, `Temp/`, `Build/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`, `Samples/`, `Bundles/`, `yoo/`
- 嵌入式包（HybridCLR, FairyGUI, VContainer）在 `Packages/` 中进行版本控制

## Unity 命令行测试规范（强制）

Unity 编辑器路径："/Applications/Unity/Unity.app/Contents/MacOS/Unity"

1. 所有自动化测试默认使用 `-batchmode -nographics`，禁止依赖手工点选 Test Runner 窗口。
2. 统一使用绝对路径参数，测试结果统一输出到 `TestResults/` 目录。
3. 先跑编译回归，再跑 EditMode，最后跑 PlayMode，避免无效长时间等待。
4. PlayMode 必须设置超时保护，防止域重载或回调异常导致 CI 卡死。
5. **测试命令禁止携带 `-quit`**，否则可能出现进程正常退出但测试未执行、XML 未产出的假通过。
6. 结果必须包含 JSON + XML + log 三类文件，且保留最近一次执行日志用于追溯。

推荐命令（本项目）：

> 说明：仅编译回归命令使用 `-quit`；EditMode/PlayMode 测试命令不得带 `-quit`。

```bash
# 1) 编译回归
"/Applications/Unity/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "<项目绝对路径>" \
  -logFile "<项目绝对路径>/TestResults/game-module-compile.log" \
  -quit

# 2) EditMode 批处理
"/Applications/Unity/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "<项目绝对路径>" \
  -executeMethod Game.Module.Editor.ModuleTestCommand.RunFromCommandLine \
  -hudTestPlatform EditMode \
  -hudTestFilter Game.Tests.EditMode.ModuleConfigTests \
  -hudTestTimeoutSec 120 \
  -hudTestResult "<项目绝对路径>/TestResults/game-module-editmode.json" \
  -hudTestXml "<项目绝对路径>/TestResults/game-module-editmode.xml" \
  -logFile "<项目绝对路径>/TestResults/game-module-editmode-run.log"

# 3) PlayMode 批处理
"/Applications/Unity/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "<项目绝对路径>" \
  -executeMethod Game.Module.Editor.ModuleTestCommand.RunFromCommandLine \
  -hudTestPlatform PlayMode \
  -hudTestTimeoutSec 240 \
  -hudTestResult "<项目绝对路径>/TestResults/game-module-playmode.json" \
  -hudTestXml "<项目绝对路径>/TestResults/game-module-playmode.xml" \
  -logFile "<项目绝对路径>/TestResults/game-module-playmode-run.log"
```
