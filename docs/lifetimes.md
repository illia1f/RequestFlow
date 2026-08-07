# Service lifetimes

What RequestFlow registers, with which lifetime, and what you can change.

## Defaults

| Service                                                                        | Lifetime                                                   | Configurable                   |
| ------------------------------------------------------------------------------ | ---------------------------------------------------------- | ------------------------------ |
| Handlers (`IRequestHandler<TRequest, TResponse>`, `IRequestHandler<TRequest>`) | Transient                                                  | Yes, `WithScopedHandlers`, per `AddRequestFlow` call |
| Stages (`IRequestStage<TRequest, TResponse>`, `IRequestStage<TRequest>`)       | Transient                                                  | Yes, `AsSingleton` or `AsScoped`, per `AddStage` call |
| `IRequestDispatcher`                                                           | Scoped                                                     | Yes, `WithTransientDispatcher` |
| Dispatch map (internal handler lookup)                                         | Singleton, built the first time the dispatcher is resolved | No                             |

## Configuring handler lifetime

Handlers are transient by default. Every call into the handler gets a fresh instance, so a handler can hold mutable state without leaking it into the next dispatch. The bottom of a stage chain resolves on each entry, so a retry stage that runs the chain twice reaches a second instance rather than the one that failed. Call `WithScopedHandlers` when handlers share per-request dependencies such as a `DbContext`. It chains with the registration methods:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .WithScopedHandlers());
```

Those two are the whole set. There is no singleton option: a singleton handler pins every dependency it injects for the life of the process, and that dependency is usually a `DbContext`. To register one anyway, add it yourself after the last `AddRequestFlow` call:

```csharp
services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<Program>());
services.AddSingleton<IRequestHandler<Ping, string>, PingHandler>();
```

Order matters there, and one rule covers handlers and stages alike: register your own after the last `AddRequestFlow` call. RequestFlow appends its descriptors whatever the collection already holds, and the container resolves the last descriptor registered for a service type. So yours has to come second.

- "Last call" is literal. A registration made between two calls usually does win, because a call registers only what it newly discovers and skips any handler or closed stage type an earlier call already registered. It loses in one case: the later call scans a new assembly and is the first to close an already declared stage over one of the new request types. Going after the last call saves you from telling those apart.
- A descriptor yours overrules stays in the collection and never resolves, but `ValidateOnBuild` still walks it and checks the constructor it names.
- Supplying an instance you built yourself calls for `services.Replace`, which drops the leftover descriptor along the way. [stages.md](stages.md#replacing-a-stage-registration) has the example.

The captive dependency rules below are yours to keep from that point on.

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
    .RegisterHandlersFromAssemblyContaining<Program>()
    .AddStage(typeof(LoggingStage<,>), s => s.AsSingleton())
    .AddStage(typeof(UnitOfWorkStage<,>), s => s.AsScoped()));
```

Stages get the singleton option handlers do not, because a stage is usually the cross-cutting kind of class that holds no dependency worth pinning. The rest of the container's rules still apply:

- A singleton stage is shared by every dispatch in the process, so it has to be thread safe, and anything it injects lives as long as it does.
- A scoped stage resolved from the root provider is the quiet case. With scope validation on, the resolution throws at dispatch. With it off, the root provider builds the stage and caches it there, so one instance serves every dispatch until the process exits. A chain arrives there through `WithTransientDispatcher` plus a dispatcher injected into a singleton. A unit-of-work stage shared across every request corrupts data rather than failing.
- A stage above that overlaps its `next` calls runs the levels below it side by side. So within one dispatch, a scoped or singleton stage under it is entered twice at once and has to be thread safe on that path too. Only transient stays clear of it, because every call resolves an instance of its own. [stages.md](stages.md) has the shape.
- A transient stage that owns an `IDisposable` is tracked by the scope that resolved it, which is the root scope for a root-resolved dispatcher.
- Repeated `next` calls multiply that. A level is resolved once per entry, so a retry stage that makes three attempts leaves three instances behind, and a hedging stage leaves one per branch. Inside a request scope they are disposed when the request ends. Under a root-resolved dispatcher they go on the root provider's disposal list instead. Nothing releases them until the process exits, and the list grows with every dispatch.

Catching the first at startup takes both container flags. `ValidateOnBuild` walks every descriptor and builds its constructor graph. Every closed stage type is a registered service, so stages are in that walk. The lifetime comparison behind "Cannot consume scoped service" comes from `ValidateScopes`. Turn on the pair, which is what ASP.NET Core turns on in Development. Under a bare `BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true })`, a singleton stage holding a scoped `DbContext` starts up clean.

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

## Switching the dispatcher to transient

`WithTransientDispatcher` registers the dispatcher transient instead of scoped:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .WithTransientDispatcher());
```

Dispatch behavior does not change: a transient dispatcher still resolves handlers from the provider it was created from. Two things do change:

- Each injection point gets its own dispatcher instance instead of sharing one per scope. Creating a dispatcher is cheap, so this costs nothing in practice.
- A singleton can now inject `IRequestDispatcher` directly, because scope validation allows transient services at the root. That dispatcher resolves handlers from the root provider, which moves a scoped handler's problem past startup. With scope validation off it also moves out of sight: the root provider builds the handler, caches it, and hands the same instance to every dispatch for the life of the process. With scope validation on, the dispatch throws "Cannot resolve scoped service" instead. Prefer the `IServiceScopeFactory` pattern above. It keeps the failure at startup and gives each unit of work its own scope.

Root-resolved dispatch has a quieter cost. The container tracks every transient `IDisposable` it creates in the scope that resolved it, and the root scope only ends at application shutdown. So a transient handler that is or owns an `IDisposable` is kept alive by the root provider on every send, and memory grows for the life of the process. Inside a request scope, or an explicit `IServiceScopeFactory` scope, the same handler is disposed at scope end. This is standard Microsoft DI behavior, not something RequestFlow can override, and one more reason to prefer a scope per unit of work.

The first `AddRequestFlow` call fixes the dispatcher lifetime, and later calls cannot change it. This matches how the first registration wins for assemblies. There is no singleton option: a singleton dispatcher would resolve every handler from the root provider, so scoped handlers could never work with it.

## Captive dependencies

The container does not stop a longer-lived handler from holding a shorter-lived dependency. The dependency silently lives as long as the handler does (a "captive dependency"). Safe pairings:

- Transient or scoped handlers can depend on services of any lifetime. That is why those are the two lifetimes RequestFlow registers.
- A hand-registered singleton handler should depend on singletons only. One holding a scoped `DbContext` keeps that `DbContext` alive for the entire process.

`ValidateOnBuild` and `ValidateScopes` together report the second case at startup; neither alone does.

## Validation and build timing

Each `AddRequestFlow` call adds handler registrations to the container immediately. The dispatch map is the internal lookup the dispatcher uses to find handlers. It is built and validated once per provider, the first time that provider resolves a dispatcher. Missing or duplicate handlers across all calls surface at that point, as a single `RequestFlowValidationException` listing every problem.

`ValidateOnBuild` cannot catch these problems: it checks constructor dependencies without executing factory registrations, and the dispatch map is built by one. To fail at startup instead of at first dispatch, call `ValidateRequestFlow` once after building the provider. It works in any application, hosted or not, and returns the provider for chaining:

```csharp
var app = builder.Build();
app.Services.ValidateRequestFlow();
```

```csharp
IServiceProvider provider = services.BuildServiceProvider().ValidateRequestFlow();
```

## Registration changes after the provider is built

`AddRequestFlow` records what it finds in one registry per service collection. A provider builds its dispatch map from that registry the first time it resolves a dispatcher, which can be long after `BuildServiceProvider` returned. A registration made in between reaches the map but not the provider, because a provider reads the collection once, when it is built.

Nothing reports the split. The map gets a plan for the new request, and the dispatch fails further in, where that plan asks the container for the handler. The error is the container's own "No service for type", not a RequestFlow one. Removing or replacing a descriptor in the same window splits the same way: the provider keeps what it was built with.

Register, replace, or remove every stage and handler descriptor before calling `BuildServiceProvider`. This includes `services.Replace(...)` ([stages.md](stages.md#replacing-a-stage-registration)). A provider built after the change is fine; it holds both the registry entry and the descriptor.
