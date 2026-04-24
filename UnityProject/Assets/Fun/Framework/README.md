# Fun Framework Usage Guide

## Framework overview

`Fun/Framework` hosts business-agnostic runtime infrastructure shared by AOT and hot-update code.

Current runtime modules in this directory:
- `Runtime/Cqrs`: synchronous CQRS bus with explicit registration and fail-fast dispatch.
- `Runtime/Collections`: pure-managed high-performance containers for hot-path gameplay loops.
- `Runtime/Pooling`: static generic object pooling for pure C# reusable objects.

Design goals:
- no reflection-based runtime auto-scan
- explicit registration during bootstrap, then immutable runtime routing
- struct-based messages on hot paths

## Included APIs

Create one `CqrsBus` instance and use it as both `ICqrsRegistry` and `ICqrsBus`.

Registration phase (`ICqrsRegistry`):
- `RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)`
- `RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)`
- `Subscribe<TEvent>(IEventHandler<TEvent> handler)`
- `Freeze()`

Dispatch phase (`ICqrsBus`):
- `Send<TCommand>(in TCommand command)`
- `Query<TQuery, TResult>(in TQuery query)`
- `Publish<TEvent>(in TEvent @event)`

Lifecycle rule:
1. register handlers/subscribers
2. call `Freeze()` during bootstrap before runtime dispatch (recommended once; repeated calls are idempotent)
3. call `Send`/`Query`/`Publish` after freeze

## Core constraints

- message types are value types (`readonly struct`) implementing `ICommand`, `IQuery<TResult>`, or `IEvent`
- handlers implement `ICommandHandler<T>`, `IQueryHandler<TQuery, TResult>`, `IEventHandler<T>` (typically class instances by convention and performance tradeoff, but not an API-level restriction)
- duplicate command/query registration is forbidden
- registration APIs are unavailable after `Freeze()`
- dispatch is synchronous; no async/await in CQRS core
- `CqrsBus` supports optional logger injection via `new CqrsBus(ICqrsLogger logger)`

## Bootstrap example

```csharp
using Fun.Framework.Cqrs;

public static class GameplayBootstrap
{
    public static ICqrsBus BuildBus()
    {
        var bus = new CqrsBus();

        // Registration phase
        bus.RegisterCommand<StartBattleCommand>(new StartBattleCommandHandler());
        bus.RegisterQuery<GetPlayerLevelQuery, int>(new GetPlayerLevelQueryHandler());
        bus.Subscribe<PlayerLevelUpEvent>(new AnalyticsLevelUpHandler());

        // Freeze registry before dispatch
        bus.Freeze();

        return bus;
    }
}
```

## Message struct examples

```csharp
using Fun.Framework.Cqrs;

public readonly struct StartBattleCommand : ICommand
{
    public StartBattleCommand(int stageId)
    {
        StageId = stageId;
    }

    public int StageId { get; }
}

public readonly struct GetPlayerLevelQuery : IQuery<int>
{
    public GetPlayerLevelQuery(int playerId)
    {
        PlayerId = playerId;
    }

    public int PlayerId { get; }
}

public readonly struct PlayerLevelUpEvent : IEvent
{
    public PlayerLevelUpEvent(int playerId, int newLevel)
    {
        PlayerId = playerId;
        NewLevel = newLevel;
    }

    public int PlayerId { get; }
    public int NewLevel { get; }
}
```

Dispatch usage:

```csharp
var bus = GameplayBootstrap.BuildBus();

bus.Send(new StartBattleCommand(stageId: 1001));
var level = bus.Query<GetPlayerLevelQuery, int>(new GetPlayerLevelQuery(playerId: 42));
bus.Publish(new PlayerLevelUpEvent(playerId: 42, newLevel: level + 1));
```

## Failure semantics

Framework CQRS fails fast with explicit exception types:
- `HandlerNotRegisteredException`: `Send`/`Query` called without a matching registered handler
- `DuplicateRegistrationException`: duplicate `RegisterCommand` or duplicate `RegisterQuery` for the same message key
- `RegistryFrozenException`: any `RegisterCommand`/`RegisterQuery`/`Subscribe` call after `Freeze()`
- `InvalidOperationException`: `Send`/`Query`/`Publish` called before `Freeze()`
- `ArgumentNullException`: null handler passed to `RegisterCommand`/`RegisterQuery`/`Subscribe`

`Publish` with no subscribers is a no-op (no exception).

## Collections (`Fun.Framework.Collections`)

### Available containers

- `FastList<T>`
- `FastDictionary<TKey, TValue>`
- `FastHashSet<T>`
- `RingBuffer<T>`
- `FastPriorityQueue<T>`
- `ObjectPool<T>`

### 0GC contract

1. Pre-size via constructor capacity (or `EnsureCapacity` where available) before entering hot paths.
2. Prefer `NoResize` APIs in gameplay loops (`AddNoResize`, `TryAddNoResize`, `EnqueueNoResize`).
3. Treat capacity overflow as a configuration/programming error and fix sizing up-front.
4. Reuse memory with `Clear(ClearMode.Logical)` and object pooling.

### Usage samples

FastList hot loop:

```csharp
var list = new FastList<int>(1024);
for (var i = 0; i < 1024; i++)
{
    list.AddNoResize(i);
}
list.Clear(ClearMode.Logical);
```

FastDictionary lookup loop:

```csharp
var map = new FastDictionary<int, int>(2048);
for (var i = 0; i < 1024; i++)
{
    map.TryAddNoResize(i, i);
}

for (var i = 0; i < 1024; i++)
{
    map.TryGetValue(i, out _);
}
```

ObjectPool reuse:

```csharp
var pool = new ObjectPool<MyReusable>(() => new MyReusable(), 256);
pool.Prewarm(128);

var item = pool.Rent();
pool.Return(item);
```

## Pooling (`Fun.Framework.Pooling`)

`Fun.Framework.Pooling` is the production static generic pool implementation for pure C# objects.
It coexists with the legacy `Fun.Framework.Collections.ObjectPool<T>` implementation.

Core contract:

```csharp
public interface IPoolable
{
    void Reset();
}
```

Usage:

```csharp
using Fun.Framework.Pooling;

public sealed class DamageEvent : IPoolable
{
    public int SourceId;
    public int Value;

    public void Reset()
    {
        SourceId = 0;
        Value = 0;
    }
}

Pool<DamageEvent>.SetMaxSize(256);
Pool<DamageEvent>.Prewarm(64);

var evt = Pool<DamageEvent>.Get();
// use evt...
Pool<DamageEvent>.Release(evt);
```

Notes:
- `Pool<T>` requires `where T : class, IPoolable, new()`.
- Pool miss auto-creates via `new T()`.
- In Editor/Development builds, invalid release patterns fail fast with exceptions.
- In release/non-dev builds, invalid releases are ignored to avoid corrupting pool state.
- Existing `Fun.Framework.Collections.ObjectPool<T>` remains supported for compatibility.
