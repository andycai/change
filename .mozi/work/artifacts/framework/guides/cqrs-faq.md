# CQRS 常见问题

**Q: Reset() 应该清理哪些状态？**
A: 仅清理 Handler 自己的瞬态可变字段（如临时集合、缓存）。readonly 注入依赖（`_state`/`_wallet`）**不要重置**——它们跨调用共享。

**Q: Struct 自处理如何访问外部服务（如 QuestSessionState）？**
A: 通过 readonly 字段在构造时注入：`new BumpMainQuestProgressCommand(state, delta)`。`Execute()` 无参（FRD #2 签名约束），不引入服务定位器，保持 0GC 与依赖显式。

**Q: 对象池何时创建和销毁 Handler？**
A: 框架不管理池生命周期，只在 `Handle` 后调 `Reset()`。Handler 实例由 DI/工厂创建并注册；池化复用由用户决定。如果不用池（每次新建），功能上仍正确但失去复用收益。

**Q: 异步 Command 执行期间反注册 Handler 会怎样？**
A: 不支持。确保 `await SendAsync/AskAsync` 完成后再反注册，否则 `Reset` 可能指向已释放实例。

**Q: 监控告警触发后如何排查？**
A: 启用 `ENABLE_CQRS_MONITORING`，订阅 `OnThresholdExceeded` 获取 `MessageType` 与 `GcBytes`。定位该消息的 handler，用 Profiler Memory 深查分配源（常见：闭包、LINQ、误复用 buffer）。

**Q: 为什么监控默认关闭？**
A: 监控 ON 时每次 dispatch 产生闭包分配（破坏 0GC）+ Stopwatch 开销。默认 OFF 保证生产热路径 0GC；ON 仅诊断用途。

**Q: 代码里还用 `GC.GetAllocatedBytesForCurrentThread()`，它不工作？**
A: 是的，该 API 在 Unity 2022.3 Mono EditMode 恒返回常量。改用 `GC.GetTotalMemory(true)` 测量存活堆增长。详见 `cqrs-performance-best-practices.md`。

**Q: Publish 的事件 Handler 可以池化吗？**
A: 事件 Handler 不池化（Publish 没有 `ResetIfPoolable` 调用）。需要复用实例的话，自行在外部管理引用。

**Q: 能否为同一 Command 同时注册同步和异步 Handler？**
A: 可以。它们位于不同注册表（`_commandHandlers` vs `_asyncCommandHandlers`），分别经 `Send` / `SendAsync` 分发。但同一 Command 类型只能选一种`执行`模式（Class vs Struct 互斥）。
