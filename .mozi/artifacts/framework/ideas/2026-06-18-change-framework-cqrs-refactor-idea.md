# Cqrs 模块重构

## 简化 Command 和 Query 层

为了追求 0GC，现在它们的实现跟 Event 是一样的，都需要注册 Handler 来处理请求。这样增加了复杂度和用户的使用负担，需要改为：

- 一个 Command 类和一个 Query 类来分别处理命令和查询请求，使用对象池来实现 0GC
- 消灭 Lambda 闭包： 传递参数时使用强类型传递（如带有 TArg 的重载），杜绝匿名函数捕获局部变量
- 严格的 Clear 逻辑： 别忘了清空命令内部的各种引用类型字段

## Command 需要支持异步执行 ExecuteAsync

Command 类需要支持异步执行，以便在处理命令时不阻塞主线程。ExecuteAsync 方法需要返回一个 UniTask 对象，以便调用者可以等待命令处理完成。

## Event 现在的实现是每个 Event 都有一个对应的 Handler 类

这样的设计导致每个 Event 都需要一个对应的 Handler 类，增加了代码的复杂性和维护成本。需要增加支持订阅接口，以便用户可以订阅感兴趣的 Event 并注册相应的 Handler 委托而不是类。

## 简化

- IDomainEvent、IDomainEventHandler 是否有存在必要，已经有 IEvent 和 IEventHandler 了
- ICqrsRuntime、 ICqrsRuntimeProvider、CqrsRuntime 和 CqrsContextRuntimeProvider 是否有存在必要，是否可以简化，现在实现是 CqrsBootstrap 里提供注册接口，但是send 的接口又是从 Build出来的 cqrsruntime 对象执行，注册和执行的接口没有一起隔离；是否直接去掉 CqrsRuntime 和 CqrsBootstrap ，直接使用 CqrsBus 对象提供的注册和执行接口即可，不同的 domain 使用不同的 CqrsBus 对象。
