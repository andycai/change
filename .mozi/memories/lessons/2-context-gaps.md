# 2. Repeated Clarifications & Context Gaps (高频疑问与项目事实)

*反复搞错的项目结构或业务逻辑事实，避免盲目猜测*

- **文档输出语言：** 使用英文写文档 -> 始终使用中文写文档
- **GC.GetAllocatedBytesForCurrentThread 在 Unity 2022.3 Mono 失效：** 该 API 在 Unity 2022.3 Mono EditMode 恒返回常量（对 1MB 分配测得 delta=0）。0GC 验证必须改用 `GC.GetTotalMemory(true)` 测量存活堆变化。注意 GetTotalMemory 有 ~4KB GC 噪声，断言需加容差（如 `Assert.That(delta, Is.LessThanOrEqualTo(4096))`）。

<!-- 容量上限：15-20 条。超出时合并或归档旧条目 -->
