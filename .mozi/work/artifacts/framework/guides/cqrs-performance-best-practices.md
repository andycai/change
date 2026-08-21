# CQRS 性能最佳实践

## 0GC 目标

热路径（`Send`/`Ask`/`Publish`）严格 0 字节 GC 分配。验证三法：

1. **单元测试**：`DualModeZeroGcTests` / `ZeroAllocationDispatchTests` 使用 `GC.GetTotalMemory(true)` 测量堆增长。热路径必须保持零增长（无新存活对象）。
2. **Profiler**：Unity Profiler → Memory 模块，观察 GC.Alloc 列。
3. **运行时监控**：定义 `ENABLE_CQRS_MONITORING` 符号，安装 `CqrsPerformanceMonitor`。

## 启用运行时监控

1. Project Settings → Player → Scripting Define Symbols 加 `ENABLE_CQRS_MONITORING`。
2. 启动时：`CqrsBus.SetActiveMonitor(new CqrsPerformanceMonitor())`。
3. 单次执行 GC > 100 字节（默认阈值）触发 `OnThresholdExceeded` 事件。
4. **代价**：ON 时每次 dispatch 产生闭包分配 + Stopwatch 读数——仅诊断用途，默认 OFF。

## Profiler 验证步骤

1. 打开 Profiler（Window → Analysis → Profiler），启 Memory。
2. 运行目标操作（如打开 Quest 面板、完成任务）。
3. 检查对应帧 GC.Alloc 为 0。

## 常见分配陷阱

- Struct 自处理 `Execute()` 内闭包/LINQ → 无法消除的每调用分配。
- Event 静态委托捕获变量 → `ClosureCaptureException`（注册时拦截）。
- Query Handler 复用 buffer 又返回 snapshot → snapshot 在 Reset 后失效（Quest `GetQuestPanelQueryHandler` 不复用 buffer）。

## 测试中的 GC 测量

当前 Unity 2022.3 Mono EditMode 中 `GC.GetAllocatedBytesForCurrentThread()` 失效（恒返回常量）。改用 `GC.GetTotalMemory(true)` 测量存活堆：

```
ForceFullGc();
var before = GC.GetTotalMemory(true);
// 循环执行 100000 次 dispatch
var after = GC.GetTotalMemory(true);
Assert.AreEqual(before, after);
```

`true` 参数强制做完整 GC，只有存活对象被计入——0GC 分发时前后值相等。
