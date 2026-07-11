# Service lifetimes

What RequestFlow registers, with which lifetime, and what you can change.

## Defaults

| Service                                                                        | Lifetime                                                   | Configurable                   |
| ------------------------------------------------------------------------------ | ---------------------------------------------------------- | ------------------------------ |
| Handlers (`IRequestHandler<TRequest, TResponse>`, `IRequestHandler<TRequest>`) | Transient                                                  | Yes, per `AddRequestFlow` call |
| `IRequestDispatcher`                                                           | Scoped                                                     | Yes, `WithTransientDispatcher` |
| Dispatch map (internal handler lookup)                                         | Singleton, built the first time the dispatcher is resolved | No                             |

## Configuring handler lifetime

Call `WithHandlerLifetime` inside the configure delegate; it chains with the registration methods:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .WithHandlerLifetime(ServiceLifetime.Scoped));
```

`ServiceLifetime.Transient` is the default: each dispatch gets a fresh handler instance, so a handler can hold mutable state without leaking it into the next dispatch. Use `Scoped` when handlers share per-request dependencies such as a `DbContext`. Use `Singleton` only for stateless handlers whose dependencies are all singletons.

## Lifetime is per registration call

`AddRequestFlow` can be called multiple times; calls are additive. Each call's `HandlerLifetime` applies to the handlers that call discovers:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Orders.Module>()
    .WithHandlerLifetime(ServiceLifetime.Scoped));

// Handlers stay transient by default.
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Reporting.Module>());
```

An assembly already registered by an earlier call is skipped, so its handlers keep the lifetime of the call that first registered it. The same rule applies to `RegisterGenericHandler`: registering a generic handler for the same closing type again does nothing, and the first registration's lifetime wins.

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
- A singleton can now inject `IRequestDispatcher` directly, because scope validation allows transient services at the root. That dispatcher resolves handlers from the root provider, so a scoped handler now fails at dispatch time instead of at startup. Prefer the `IServiceScopeFactory` pattern above: it keeps the failure at startup and gives each unit of work its own scope.

There is a quieter cost to root-resolved dispatch: the container tracks every transient `IDisposable` it creates in the scope that resolved it, and the root scope only ends at application shutdown. A transient handler that is (or owns) an `IDisposable`, dispatched through a root-resolved dispatcher, is therefore kept alive by the root provider on every send; memory grows for the life of the process. Inside a request scope or an explicit `IServiceScopeFactory` scope the same handler is disposed at scope end. This is standard Microsoft DI behavior, not something RequestFlow can override, and one more reason to prefer the scope-per-unit-of-work pattern.

The first `AddRequestFlow` call fixes the dispatcher lifetime; later calls cannot change it. This matches how the first registration wins for assemblies. There is no singleton option: a singleton dispatcher would resolve every handler from the root provider, so scoped handlers could never work with it.

## Captive dependencies

The container does not stop a longer-lived handler from holding a shorter-lived dependency. The dependency silently lives as long as the handler does (a "captive dependency"). Safe pairings:

- Transient or scoped handlers can depend on services of any lifetime.
- Singleton handlers should depend on singletons only. A singleton handler holding a scoped `DbContext` keeps that one `DbContext` alive for the entire process.

## Validation and build timing

Each `AddRequestFlow` call adds handler registrations to the container immediately. The dispatch map, the internal lookup the dispatcher uses to find handlers, is built and validated once per provider, the first time that provider resolves a dispatcher. Missing or duplicate handlers across all calls surface at that point as a single `RequestFlowValidationException` listing every problem.

`ValidateOnBuild` cannot catch these problems: it checks constructor dependencies without executing factory registrations, and the dispatch map is built by one. To fail at startup instead of at first dispatch, call `ValidateRequestFlow` once after building the provider. It works in any application, hosted or not, and returns the provider for chaining:

```csharp
var app = builder.Build();
app.Services.ValidateRequestFlow();
```

```csharp
IServiceProvider provider = services.BuildServiceProvider().ValidateRequestFlow();
```
