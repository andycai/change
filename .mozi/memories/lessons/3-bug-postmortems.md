# 3. Bug & Error Post-Mortems (技术踩坑与错误诊断)

*编译报错、运行时崩溃的具体代码坑*

- **ag-psd 测试 mock 需包含 initializeCanvas：** `PsdParser` 模块顶层会调用 `initializeCanvas`，测试只 mock `readPsd` 会导致 Jest 加载时报 `(0, initializeCanvas) is not a function` -> mock `ag-psd` 时同时提供 `initializeCanvas: jest.fn()`

<!-- 容量上限：15-20 条。超出时合并或归档旧条目 -->
