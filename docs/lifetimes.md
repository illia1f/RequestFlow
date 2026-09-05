# Service lifetimes

What RequestFlow registers, with which lifetime, and what you can change.

## Defaults

| Service                                                                        | Lifetime                                                   | Configurable                   |
| ------------------------------------------------------------------------------ | ---------------------------------------------------------- | ------------------------------ |
| Handlers (Task, ValueTask, and stream contracts; event handlers under their concrete class) | Transient                                                  | Yes, `WithScopedHandlers`, per `AddRequestFlow` call |
| Stages (`IRequestStage`, `IValueRequestStage`, and `IStreamRequestStage` contracts) | Transient                                                  | Yes, `AsSingleton` or `AsScoped`, per `AddStage`, `AddValueStage`, or `AddStreamStage` call |
| Custom event publish strategies | Singleton | Yes, `AsScoped` or `AsTransient`, per strategy declaration |
| `IRequestDispatcher`, `IValueRequestDispatcher`, `IStreamDispatcher`, `IEventPublisher` | Scoped                                                     | Yes, `WithTransientDispatcher`, which moves all four |
| Typed CQRS dispatchers (`ICommandDispatcher`, `IQueryDispatcher`, `IValueCommandDispatcher`, `IValueQueryDispatcher`, `IStreamQueryDispatcher`) | Transient, added by `AddCqrs`; each forwards to a dispatcher above, so a singleton injecting one still fails with `Cannot consume scoped service`, naming the core dispatcher inside | Not through `AddCqrs`; a descriptor of your own registered before it wins |
| Validation rules (`IRequestFlowValidationRule`)                                | Singleton, added by `AddValidationRule` and by `AddCqrs`    | Not through those calls; register your own descriptor for another lifetime |
| Frozen request and event maps (internal handler lookup)                        | Singleton, built together on first dispatcher or publisher resolution | No             |

The lifetimes in the first three rows are readable at the freeze. A rule of your own reads the lifetime of every handler, stage, and event strategy off the model, so a house rule such as "no singleton stages here" fails startup instead of waiting for a code review ([validation-rules.md](validation-rules.md#the-model)).

The `FrozenPlans` singleton resolves rules while it freezes the request and event maps, so every rule comes from the root provider. A provider that validates scopes throws there. A scoped rule throws ``Cannot resolve scoped service 'System.Collections.Generic.IEnumerable`1[RequestFlow.IRequestFlowValidationRule]' from root provider``, which names the enumerable rather than the rule, so look for the descriptor you registered scoped. A rule whose constructor takes a scoped dependency throws `Cannot consume scoped service` instead, naming the dependency and `RequestFlow.IRequestFlowValidationRule`, and `ValidateOnBuild` reports that one at `BuildServiceProvider`. [validation-rules.md](validation-rules.md) covers writing and registering one.

## Configuring handler lifetime

Handlers are transient by default. Every call into the handler gets a fresh instance, so a handler can hold mutable state without leaking it into the next dispatch. The bottom of a stage chain resolves on each entry, so a retry stage that runs the chain twice reaches a second instance rather than the one that failed. Call `WithScopedHandlers` when handlers share per-request dependencies such as a `DbContext`. It chains with the registration methods:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .WithScopedHandlers());
```

Those two are the whole set. There is no singleton option: a singleton handler pins every dependency it injects for the life of the process, and that dependency is usually a `DbContext`. To register one anyway, add it yourself after the last `AddRequestFlow` call:

```csharp
services.AddRequestFlow(o => o.RegisterHandlersFromCallingAssembly());
services.AddSingleton<IRequestHandler<Ping, string>, PingHandler>();
```

Order matters there, and one rule covers handlers and stages alike: register your own after the last `AddRequestFlow` call. RequestFlow appends its descriptors whatever the collection already holds, and the container resolves the last descriptor registered for a service type. So yours has to come second.

- "Last call" is literal. A registration made between two calls usually does win, because a call registers only what it newly discovers and skips any handler or closed stage type an earlier call already registered. It loses in one case: the later call scans a new assembly and is the first to close an already declared stage over one of the new request types. Going after the last call saves you from telling those apart.
- A descriptor yours overrules stays in the collection and never resolves, but `ValidateOnBuild` still walks it and checks the constructor it names.
- Supplying an instance you built yourself calls for `services.Replace`, which drops the leftover descriptor along the way. [stages.md](stages.md#replacing-a-stage-registration) has the example.

Past that point, the captive dependency rules below are yours to keep.

### Event handlers use concrete service keys

Task, ValueTask, and stream handlers register under their handler interfaces. Event handler classes register under their concrete types instead, once per class. Each applicable event subscription resolves that concrete type immediately before invoking it. This avoids resolving every event handler before any handler runs and lets one broken resolution become one entry in `EventPublishException.Failures`.

`WithScopedHandlers` applies to event handlers found by that registration call. If one scoped class implements two applicable `IEventHandler<TEvent>` contracts, both subscription entries resolve the same instance and invoke it twice.

The concrete key also means an interface registration for `IEventHandler<TEvent>` does not replace, decorate, or join RequestFlow publication. Replace or decorate the concrete handler descriptor when you intentionally customize a scanned event handler.

### Event publish strategies default to singleton

A custom `IEventPublishStrategy` is singleton unless its declaration calls `AsScoped` or `AsTransient`. Built-in strategies have no descriptor. RequestFlow registers custom strategies with `TryAdd`, so an application descriptor already present suppresses the generated one. A normal descriptor added afterwards resolves last. In either order, the application lifetime wins.

Singleton strategy fields are shared across concurrent publishes. Keep per-publish collections and counters in method locals. Fields are suitable for deliberately shared coordination such as a semaphore or meter. `RF0121` rejects one strategy type declared with different lifetimes, and `RF0123` rejects a strategy class that holds an event handler or stage role under another lifetime.

A class in both roles, Task, ValueTask, or stream handler and event handler, gets two descriptors under two different keys: the handler interface for one role, the concrete class for the other. Under `WithScopedHandlers` a scope holds one instance per key, so the constructor runs twice and neither role sees the other's state. Two keys cannot disagree about a lifetime, so nothing is reported. Split the class when either role has state or its constructor does work.

Stages use that same key, so a class registered with `AddStage` that also handles events has two descriptors under it, and the one registered last sets the lifetime for both roles. Within a single `AddRequestFlow` call that is the stage. A stage declared by an earlier call than the one that scans the class loses to the event handler instead. The freeze reports `RF0118` when the two lifetimes disagree; splitting the class in two is the fix.

## Lifetime is per registration call

`AddRequestFlow` can be called multiple times; calls are additive. Each call decides the lifetime of the handlers that call discovers:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Orders.Module>()
    .WithScopedHandlers());

// Handlers stay transient by default.
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Reporting.Module>());
```

An assembly already registered by an earlier call is skipped, so its handlers keep the lifetime of the call that first registered it. The same rule applies to `RegisterGenericHandler`: registering a generic handler for the same closing type again does nothing, and the first registration's lifetime wins.

`WithScopedHandlers` is the opposite of the dispatcher's rule below, where the first call fixes the lifetime for everyone. Handlers belong to the call that found them. The dispatcher is one service shared by all of them.

## Stage lifetime is per stage

Handlers are homogeneous, so one lifetime per registration call fits them. Stages are not: a logging stage wants singleton and a unit-of-work stage wants scoped in the same chain. One global setting would drag both to whichever is stricter, so the lifetime sits on the declaration:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .AddStage(typeof(LoggingStage<,>), s => s.AsSingleton())
    .AddStage(typeof(UnitOfWorkStage<,>), s => s.AsScoped()));
```

Stages get the singleton option that handlers do not: a stage is usually a cross-cutting class holding no dependency worth pinning. The rest of the container's rules still apply:

- A singleton stage is shared by every dispatch in the process, so it has to be thread safe, and anything it injects lives as long as it does.
- A scoped stage resolved from the root provider is the case that can pass unnoticed. With scope validation on, the resolution throws when the level runs: at dispatch on the Task or ValueTask path, at the first enumeration on a stream. With it off, the root provider builds the stage and caches it there, so one instance serves every dispatch until the process exits. A chain arrives there through `WithTransientDispatcher` plus a dispatcher injected into a singleton. A unit-of-work stage shared across every request corrupts data rather than failing.
- A stage above that overlaps its `next` calls runs the levels below it side by side. So within one dispatch, a scoped or singleton stage under it is entered twice at once and has to be thread safe on that path too. Only transient stays clear of it, because every call resolves an instance of its own. [stages.md](stages.md) has the shape.
- A transient stage that owns an `IDisposable` is tracked by the scope that resolved it, which is the root scope for a root-resolved dispatcher.
- Repeated `next` calls multiply that. A level is resolved once per entry, so a retry stage that makes three attempts leaves three instances behind, and a hedging stage leaves one per branch. Inside a request scope they are disposed when the request ends. Under a root-resolved dispatcher they go on the root provider's disposal list instead. Nothing releases them until the process exits, and the list grows with every dispatch.

Catching the singleton case at startup takes both container flags. `ValidateOnBuild` walks every descriptor and builds its constructor graph. Every closed stage type is a registered service, so stages are in that walk. The lifetime comparison behind "Cannot consume scoped service" comes from `ValidateScopes`. Turn on the pair, which is what ASP.NET Core turns on in Development. Under a bare `BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true })`, a singleton stage holding a scoped `DbContext` starts up clean.

`AddStage` registers each closed stage type the way the scan registers handlers, so the ordering rule above applies unchanged. The declaration's descriptor comes last, its lifetime is the one that applies, and any descriptor it overrules is left behind unresolved. To supply your own, call `services.Replace` afterwards: it drops one descriptor for the type and appends yours, where a plain `AddSingleton` leaves the old one for `ValidateOnBuild` to check. One is all `Replace` drops. If the type also has a descriptor of your own from before `AddRequestFlow`, use `services.RemoveAll<TStage>()` and then register. [stages.md](stages.md) has the example.

## Why the dispatcher is scoped

The dispatcher resolves handlers from the scope it was resolved in. In ASP.NET Core that means scoped handler dependencies (a `DbContext`, a unit of work) live per request without extra setup.

The consequence: a singleton service cannot inject `IRequestDispatcher` directly. With scope validation on (the ASP.NET Core development default) the host fails at startup with "Cannot consume scoped service". From a hosted service or other singleton, create a scope explicitly:

```csharp
public sealed class OutboxWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        await dispatcher.SendAsync(new FlushOutbox(), stoppingToken);
    }
}
```

The same pattern serves `IValueRequestDispatcher`. It also serves `IStreamDispatcher`, with one more rule: finish the enumeration inside the scope. A stream resolves its handler and stages from the dispatching scope on the first enumeration, not on the `Stream` call, so a sequence carried out of the `using` block throws `ObjectDisposedException` when it is finally enumerated. Keep the `await foreach` inside the block that created the scope.

The pattern also serves `IEventPublisher`. Keep publication inside the scope because event handlers resolve from that scope. Parallel publication shares the scope across every handler, so two handlers can use one scoped dependency concurrently. Sequential publication avoids overlap within one call, but two concurrent publishes from the same scope can still enter the same scoped handler together. Use separate scopes for concurrent units of work when scoped dependencies such as `DbContext` are not thread safe.

## Switching the dispatcher to transient

`WithTransientDispatcher` registers the Task request dispatcher, ValueTask request dispatcher,
stream dispatcher, and event publisher as transient instead of scoped:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .WithTransientDispatcher());
```

Dispatch behavior does not change: a transient dispatcher still resolves handlers from the provider it was created from. Two things do change:

- Each injection point gets its own dispatcher or publisher instance instead of sharing one per scope. These objects are cheap to create.
- A singleton can now inject one of these interfaces directly, because scope validation allows transient services at the root. It then resolves handlers from the root provider, which moves a scoped handler's problem past startup. With scope validation off, the root provider builds the handler, caches it, and hands the same instance to every dispatch or publication for the life of the process. With scope validation on, the operation throws "Cannot resolve scoped service" instead. Prefer the `IServiceScopeFactory` pattern above. It keeps the failure at startup and gives each unit of work its own scope.

Root-resolved dispatch has one more cost, and nothing reports it. The container tracks every transient `IDisposable` it creates in the scope that resolved it, and the root scope only ends at application shutdown. So a transient handler that is or owns an `IDisposable` is kept alive by the root provider on every send, and memory grows for the life of the process. Inside a request scope, or an explicit `IServiceScopeFactory` scope, the same handler is disposed at scope end. This is standard Microsoft DI behavior, not something RequestFlow can override, and one more reason to prefer a scope per unit of work.

The first `AddRequestFlow` call fixes every dispatcher and the publisher lifetime, and later calls cannot change it. This matches how the first registration wins for assemblies. There is no singleton option: a singleton dispatch surface would resolve every handler from the root provider, so scoped handlers could never work with it.

## Captive dependencies

The container does not stop a longer-lived handler from holding a shorter-lived dependency. The dependency silently lives as long as the handler does (a "captive dependency"). Safe pairings:

- Transient or scoped handlers can depend on services of any lifetime. That is why those are the two lifetimes RequestFlow registers.
- A hand-registered singleton handler should depend on singletons only. One holding a scoped `DbContext` keeps that `DbContext` alive for the entire process.

`ValidateOnBuild` and `ValidateScopes` together report the second case at startup; neither alone does.

## Validation and build timing

Each `AddRequestFlow` call adds handler registrations to the container immediately. The `FrozenPlans` singleton freezes and validates the request and event maps together once per provider, on the first dispatcher or publisher resolution. Missing or duplicate request handlers and event validation failures across all calls surface at that point as one `RequestFlowValidationException`.

`ValidateOnBuild` cannot catch these problems: it checks constructor dependencies without executing the `FrozenPlans` singleton factory that performs the freeze. To fail at startup instead of at first dispatch or publication, call `ValidateRequestFlow` once after building the provider. It works in any application, hosted or not, and returns the provider for chaining:

```csharp
var app = builder.Build();
app.Services.ValidateRequestFlow();
```

```csharp
IServiceProvider provider = services.BuildServiceProvider().ValidateRequestFlow();
```

## Registration changes after the provider is built

`AddRequestFlow` records what it finds in one registry per service collection. A provider freezes its `FrozenPlans` from that registry on the first dispatcher or publisher resolution, which can be long after `BuildServiceProvider` returned. A registration made in between reaches the frozen request or event map but not the provider, because a provider reads the service collection only when it is built.

Nothing reports the split. The map gets a plan for the new request, and the dispatch fails further in, where that plan asks the container for the handler. The error is the container's own "No service for type", not a RequestFlow one. Removing or replacing a descriptor in the same window splits the same way: the provider keeps what it was built with.

Register, replace, or remove every stage and handler descriptor before calling `BuildServiceProvider`. This includes concrete event handler descriptors and `services.Replace(...)` ([stages.md](stages.md#replacing-a-stage-registration)). A provider built after the change is fine; it holds both the registry entry and the descriptor.
