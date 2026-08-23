# Registration

Every `AddRequestFlow` option: what each one registers and when to reach for it.

## The options

`AddRequestFlow` takes a configure delegate, and every option chains on it:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .WithScopedHandlers());
```

| Option                                        | What it does                                                                  |
| --------------------------------------------- | ------------------------------------------------------------------------------ |
| `RegisterHandlersFromAssemblyContaining<T>()` | Scans the assembly containing `T`                                              |
| `RegisterHandlersFromAssembly(assembly)`      | Scans the given assembly                                                        |
| `RegisterGenericHandler(handlerType, ...)`    | Closes an open generic handler over the declared types                          |
| `AddStage(stageType, configure?)`             | Wraps applicable handlers in a stage; `configure` narrows its reach and sets its lifetime (see [stages.md](stages.md)) |
| `AddStreamStage(stageType, configure?)`       | The same for stream handlers, on a chain of its own (see [streaming.md](streaming.md)) |
| `DisallowUnusedStages()`                      | Fails startup validation when a stage reaches no request (see [stages.md](stages.md)) |
| `AllowUnhandledRequests()`                    | Skips the missing-handler check at startup validation                           |
| `PublishAllEventsWith<TStrategy>(configure?)` | Selects the global event strategy and configures a custom strategy lifetime |
| `PublishEventsWith<TEvent, TStrategy>(configure?)` | Selects a strategy for an assignable event target |
| `PublishEventsInParallel()`                   | Selects the global `ParallelPublishStrategy` (see [events.md](events.md)) |
| `AllowUnhandledEvents()`                      | Permits a known event to have no applicable handler                            |
| `DisallowUnusedEventHandlers()`               | Fails validation when an event subscription or typed strategy reaches no known event |
| `WithScopedHandlers()`                        | Registers this call's handlers scoped instead of transient (see [lifetimes.md](lifetimes.md)) |
| `WithTransientDispatcher()`                   | Registers the dispatcher transient instead of scoped (see [lifetimes.md](lifetimes.md))    |

## What the scan picks up

The scan looks at every concrete class in the configured assemblies and registers those that implement `IRequestHandler<TRequest, TResponse>`, `IRequestHandler<TRequest>`, `IStreamRequestHandler<TRequest, TItem>`, or `IEventHandler<TEvent>`. A class implementing several request or stream handler interfaces registers once per interface, so one class can handle several request types.

Event handlers differ at the container boundary. Each event handler class is registered once under its concrete type, while every closed `IEventHandler<TEvent>` contract it implements becomes a subscription in the frozen event plan. A class with two applicable contracts is invoked twice for one event, but both scoped resolutions return the same instance. RequestFlow does not register scanned handlers under `IEventHandler<TEvent>`, so `GetServices<IEventHandler<TEvent>>()` is not an event-publication extension point.

The registry records every request and concrete closed event type found by the scan. An exact closed event-handler contract also makes its declared event type known, even when that event's assembly was not scanned. Startup validation uses those lists to report requests and events no handler covers. Abstract event bases and event interfaces can be subscription targets, but do not get publishable plans of their own.

Abstract classes, interfaces, and open generic definitions are skipped. Open generic request and stream handlers need an explicit declaration, covered below. Open generic event handlers are not supported; use a closed `IEventHandler<IEvent>` for a catch-all handler.

## Multiple calls are additive

`AddRequestFlow` can be called any number of times, once per module for example. Each call adds to the same registration, and validation checks the combined result. An assembly registered by an earlier call is skipped, so its handlers keep the settings of the call that first registered it:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Orders.Module>()
    .WithScopedHandlers());

services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Reporting.Module>());
```

## Generic handlers

One handler implementation can serve a family of generic requests. The scan ignores open generics, so each closing is declared with `RegisterGenericHandler`:

```csharp
public sealed record Audit<T>(string Payload) : IRequest<string>;

public sealed class AuditHandler<T> : IRequestHandler<Audit<T>, string>
{
    public Task<string> HandleAsync(Audit<T> request, CancellationToken cancellationToken)
        => Task.FromResult($"{typeof(T).Name}:{request.Payload}");
}

services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .RegisterGenericHandler(typeof(AuditHandler<>), typeof(Order), typeof(User)));
```

Each declared closing produces one concrete handler: `AuditHandler<Order>` handles `Audit<Order>`, `AuditHandler<User>` handles `Audit<User>`. A request whose closing was not declared has no handler:

```csharp
await dispatcher.SendAsync(new Audit<Order>("o1"));   // handled by AuditHandler<Order>
await dispatcher.SendAsync(new Audit<Refund>("r7"));  // HandlerNotFoundException
```

The undeclared closing surfaces at dispatch rather than at startup, because a generic request definition is not a scannable request type. Declare every closing you dispatch.

The handler type must be a concrete open generic definition with exactly one type parameter that implements a handler interface. Each closing type must be a closed type that satisfies the handler's `where` constraints. A declaration that breaks these rules fails startup validation with a problem naming the type; the full list is in [exceptions.md](exceptions.md). Declaring the same closing twice does nothing: the first declaration wins, the same rule as repeated assemblies.

## AllowUnhandledRequests

By default every scanned request type must have a handler, checked at startup validation. `AllowUnhandledRequests` skips that check. The intended case is a contracts assembly whose requests are handled in a different application:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Contracts.CreateOrder>()
    .AllowUnhandledRequests());
```

The setting is sticky: once any call opts in, the check is off for every registered assembly, not only that call's. The safety net also moves: a request that reaches `SendAsync` without a handler throws `HandlerNotFoundException` at dispatch instead of failing at startup. The duplicate-handler check stays on either way.

## Event options

Event publication uses `SequentialPublishStrategy` by default. Select a global fallback and optional per-event policies:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .PublishAllEventsWith<ParallelPublishStrategy>()
    .PublishEventsWith<IAuditEvent, ThrottledStrategy>(strategy => strategy.AsScoped())
    .PublishEventsWith<OrderPlaced, FailFastPublishStrategy>()
    .AllowUnhandledEvents()
    .DisallowUnusedEventHandlers());
```

Strategy declarations accumulate across additive `AddRequestFlow` calls. Identical declarations deduplicate. Conflicting global or same-target declarations produce `RF0119` at freeze.

`PublishAllEventsWith<TStrategy>` supplies the terminal fallback. A per-event declaration beats it when its target is assignable from the concrete event. Exact types beat base classes, base classes beat interfaces, interfaces beat `IEvent`, and `IEvent` beats the global fallback. Unrelated interfaces at the winning tier produce `RF0120`; declare the exact event type to resolve the tie.

Custom strategies are singleton by default. The configure delegate accepts `AsSingleton`, `AsScoped`, or `AsTransient`. Built-in strategies are never registered in DI and reject lifetime configuration. RequestFlow uses `TryAdd` for a custom strategy descriptor, so an application descriptor already present suppresses the generated one. A normal application descriptor added afterwards resolves last. In either order, its lifetime wins over the lifetime named in the options.

`PublishEventsInParallel` starts handlers serially in frozen plan order on the publishing thread, then overlaps only incomplete asynchronous work. It does not use `Task.Run`, and it waits for every handler. See [events.md](events.md#parallel-publication-and-scopes) before enabling it for scoped handlers.

`AllowUnhandledEvents` suppresses `RF0114` and permits a known event to freeze with an empty plan. A built-in strategy then does no work. A custom strategy still resolves and receives a delivery with zero entries. The option does not permit an event type that the scan never saw; an unknown runtime type still throws `EventNotRegisteredException`.

`DisallowUnusedEventHandlers` enables `RF0115` for a handler contract and `RF0122` for a per-event strategy declaration that reaches no known event. It is off by default and independent of `AllowUnhandledEvents`. Global strategy declarations never produce `RF0122`.

## ValidateRequestFlow

Registration problems normally surface the first time a dispatcher or event publisher is resolved. `ValidateRequestFlow` runs the same validation right after the provider is built, so a misconfigured application fails at startup instead of on its first request or event:

```csharp
var app = builder.Build();
app.Services.ValidateRequestFlow();
```

It returns the provider, so it chains in non-hosted code too:

```csharp
IServiceProvider provider = services.BuildServiceProvider().ValidateRequestFlow();
```

All problems are reported in one `RequestFlowValidationException`, not one at a time (see [exceptions.md](exceptions.md)). The container's own `ValidateOnBuild` cannot catch these problems; [lifetimes.md](lifetimes.md) explains why and covers validation timing in detail.

## Checks of your own

`AddRequestFlow` returns a builder, and `AddValidationRule` puts a check of yours in the same startup pass:

```csharp
services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<Program>())
    .AddValidationRule<RequestNameRule>();
```

The rule sees every registered request, handler, stage, event, and event subscription, and reports into the same exception as the built-in checks. [validation-rules.md](validation-rules.md) covers writing, registering, and testing one.
