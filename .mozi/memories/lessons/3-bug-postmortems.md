# 3. Bug & Error Post-Mortems (技术踩坑与错误诊断)

*编译报错、运行时崩溃的具体代码坑*

- **Unity Mono 不支持值类型实例方法的 Delegate.CreateDelegate：** 计划用 `Delegate.CreateDelegate(typeof(Action<T>), null, method)` 创建值类型实例方法的开放实例委托，但 Unity Mono 抛 `ArgumentException: method arguments are incompatible`（值类型方法的隐式 `this` 是 `ref T`，与 `Action<T>` 的 by-value 首参数不兼容）。必须改用 `DynamicMethod` 发射 `Ldarga_S + Constrained + Callvirt` IL 实现零装箱约束调用。注意 DynamicMethod 不兼容 IL2CPP/AOT。
- **C# in 参数禁止 lambda 捕获：** `Send(in TCommand command)` 内的 `() => invoke(command)` 编译错误 CS1657（`in` 参数的引用期无法扩展到闭包）。必须在 lambda 前先复制到局部：`var cmdCopy = command; () => invoke(cmdCopy)`。该闭包有堆分配，但用于 ON-ONLY 诊断模式（用户接受的开销）。
- **Unity -testFilter 不支持逗号分隔多类名：** `-testFilter "ClassA,ClassB"` 返回 `testcasecount=0`。必须逐个类单独运行。
- **移除 Freeze 后遗留的 orphan 测试：** `RegistrationGuardTests` 引用已删除的 `bus.IsFrozen` API（编译时已不存在的成员），导致所有 EditMode 测试编译失败（CS1061）。代码库清理 Freeze 机制时应同时删除关联的 orphan 测试。

<!-- 容量上限：15-20 条。超出时合并或归档旧条目 -->
