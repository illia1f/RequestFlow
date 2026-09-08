# Registration

Every `AddRequestFlow` option: what each one registers and when to reach for it.

## The options

`AddRequestFlow` takes a configure delegate, and every option chains on it:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .WithScopedHandlers());
```

| Option                                        | What it does                                                                  |
| --------------------------------------------- | ------------------------------------------------------------------------------ |
| `RegisterHandlersFromAssemblyContaining<T>()` | Scans the assembly containing `T`                                              |
| `RegisterHandlersFromCallingAssembly()`       | Scans the assembly containing the `AddRequestFlow` configuration delegate       |
| `RegisterHandlersFromAssembly(assembly)`      | Scans the given assembly                                                        |
| `RegisterGenericHandler(handlerType, ...)`    | Closes an open generic handler over the declared types                          |
| `AddStage(stageType, configure?)`             | Wraps applicable handlers in a stage; `configure` narrows its reach and sets its lifetime (see [stages.md](stages.md)) |
| `AddValueStage(stageType, configure?)`        | The same for ValueTask handlers, on a chain of its own (see [value-tasks.md](value-tasks.md#valuetask-stages)) |
| `AddStreamStage(stageType, configure?)`       | The same for stream handlers, on a chain of its own (see [streaming.md](streaming.md)) |
| `DisallowUnusedStages()`                      | Fails startup validation when a stage reaches no request (see [stages.md](stages.md)) |
| `AllowUnhandledRequest<TRequest>()`, `AllowUnhandledRequest(type)` | Permits one exact request type to have no handler |
| `AllowUnhandledRequestsFromAssembly(assembly)` | Permits requests declared in one assembly to have no handler |
| `AllowAllUnhandledRequests()`                  | Skips missing-handler validation for every module |
| `PublishAllEventsWith<TStrategy>(configure?)` | Selects the global event strategy and configures a custom strategy lifetime |
| `PublishEventsWith<TEvent, TStrategy>(configure?)` | Selects a strategy for an assignable event target |
| `PublishEventsInParallel()`, `PublishEventsSequentially()`, `PublishEventsFailFast()` | Select a built-in strategy globally; each has a `<TEvent>` overload for an assignable event target (see [events.md](events.md)) |
| `AllowUnhandledEvent<TEvent>()`, `AllowUnhandledEvent(type)` | Permits one exact known event type to have no handler |
| `AllowUnhandledEventsFromAssembly(assembly)` | Permits known events declared in one assembly to have no handler |
| `AllowAllUnhandledEvents()`                    | Permits known events in every module to have no handler |
| `DisallowUnusedEventHandlers()`               | Fails validation when an event subscription or typed strategy reaches no known event |
| `AddEventHandler<THandler>()`                 | Registers one event handler without scanning its assembly (see [events.md](events.md)) |
| `AddEvent<TEvent>()`, `AddEvent(eventType)`    | Registers a concrete, closed event type without adding a handler (see [events.md](events.md)) |
| `ExcludeEventHandler<THandler>()`             | Keeps one handler's event contracts out of this call's scan (see [events.md](events.md)) |
| `AddHandler<THandler>()`                      | Registers one Task, ValueTask, or stream handler without scanning its assembly (see [Manual handlers](#manual-handlers)) |
| `ExcludeHandler<THandler>()`                  | Keeps one handler's Task, ValueTask, and stream handler contracts out of this call's scan (see [Manual handlers](#manual-handlers)) |
| `WithScopedHandlers()`                        | Registers this call's handlers scoped instead of transient (see [lifetimes.md](lifetimes.md)) |
| `WithTransientDispatcher()`                   | Registers the Task, ValueTask, and stream dispatchers plus the event publisher as transient instead of scoped (see [lifetimes.md](lifetimes.md)) |

`RegisterHandlersFromCallingAssembly()` scans the assembly containing the `AddRequestFlow` configuration delegate. RequestFlow records the assembly before invoking the delegate, so inlining and tail calls cannot change the target. Outside `AddRequestFlow`, the method falls back to `Assembly.GetCallingAssembly()`. Use `RegisterHandlersFromAssemblyContaining<T>()` or `RegisterHandlersFromAssembly(assembly)` when the target must be explicit.

## What the scan picks up

The scan looks at every concrete class in the configured assemblies and registers those that implement `IRequestHandler<TRequest, TResponse>`, `IRequestHandler<TRequest>`, `IValueRequestHandler<TRequest, TResponse>`, `IValueRequestHandler<TRequest>`, `IStreamRequestHandler<TRequest, TItem>`, or `IEventHandler<TEvent>`. A class implementing several Task, ValueTask, or stream handler interfaces registers once per interface, so one class can handle several request types.

Event handlers differ at the container boundary. Each event handler class is registered once under its concrete type, while every closed `IEventHandler<TEvent>` contract it implements becomes a subscription in the frozen event plan. A class with two applicable contracts is invoked twice for one event, but both scoped resolutions return the same instance. RequestFlow does not register scanned handlers under `IEventHandler<TEvent>`, so `GetServices<IEventHandler<TEvent>>()` is not an event-publication extension point.

The registry records every request and concrete closed event type found by the scan. An exact closed event-handler contract or an `AddEvent` call also makes an event type known, even when its assembly was not scanned. Startup validation uses those lists to report requests and events no handler covers. Abstract event bases and event interfaces can be subscription targets, but do not get publishable plans of their own.

Abstract classes, interfaces, and open generic definitions are skipped. Open generic Task, ValueTask, and stream handlers need an explicit declaration, covered below. Open generic event handlers are not supported; use a closed `IEventHandler<IEvent>` for a catch-all handler.

## Multiple calls are additive

`AddRequestFlow` can be called any number of times, once per module for example. Each call adds to the same registration, and validation checks the combined result. An assembly registered by an earlier call is skipped, so its handlers keep the settings of the call that first registered it:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Orders.Module>()
    .WithScopedHandlers());

services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Reporting.Module>());
```

- Handler lifetime applies to handlers added by that call.
- The first dispatcher registration sets its lifetime for the application. Set it in the composition root before registering modules.
- [Missing-handler exemptions](#missing-handler-exemptions) accumulate across calls, regardless of order.
- `DisallowUnusedStages()` and `DisallowUnusedEventHandlers()` apply to every module once enabled.
- Conflicting event strategies fail startup validation.

## Generic handlers

One handler implementation can serve a family of generic Task, ValueTask, or stream requests. The scan ignores open generics, so each closing is declared with `RegisterGenericHandler`:

```csharp
public sealed record Audit<T>(string Payload) : IRequest<string>;

public sealed class AuditHandler<T> : IRequestHandler<Audit<T>, string>
{
    public Task<string> HandleAsync(Audit<T> request, CancellationToken cancellationToken)
        => Task.FromResult($"{typeof(T).Name}:{request.Payload}");
}

services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .RegisterGenericHandler(typeof(AuditHandler<>), typeof(Order), typeof(User)));
```

Each declared closing produces one concrete handler: `AuditHandler<Order>` handles `Audit<Order>`, `AuditHandler<User>` handles `Audit<User>`. A request whose closing was not declared has no handler:

```csharp
await dispatcher.SendAsync(new Audit<Order>("o1"));   // handled by AuditHandler<Order>
await dispatcher.SendAsync(new Audit<Refund>("r7"));  // HandlerNotFoundException
```

Open implementations of `IValueRequestHandler<TRequest, TResponse>` and the void
`IValueRequestHandler<TRequest>` use the same closing rules.

The undeclared closing surfaces at dispatch rather than at startup, because a generic request definition is not a scannable request type. Declare every closing you dispatch.

The handler type must be a concrete open generic definition with exactly one type parameter that implements a handler interface. Each closing type must be a closed type that satisfies the handler's `where` constraints. A declaration that breaks these rules fails startup validation with a problem naming the type; the full list is in [exceptions.md](exceptions.md). Declaring the same closing twice does nothing: the first declaration wins, the same rule as repeated assemblies.

## Manual handlers

`AddHandler<THandler>()` registers one handler without scanning its assembly. Every Task, ValueTask, and stream handler contract the class implements becomes a registration, validated and frozen exactly like a scanned one. `ExcludeHandler<THandler>()` keeps those contracts out of the same call's scan; the type's event handler contracts are unaffected.

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .ExcludeHandler<PlaceOrderHandler>()          // drop the scanned handler
    .AddHandler<AuditedOrderHandler>());          // register the replacement
```

- A handler both scanned and added manually registers once, and the first registration decides the lifetime. `WithScopedHandlers()` covers the same call's manual adds wherever it appears in the delegate.
- Excluding a request's only handler produces `RF0102` at startup unless the request is exempt from missing-handler validation.
- A second handler type for the same request is still `RF0101`, whichever source registered it.
- A closed generic such as `AddHandler<AuditHandler<Order>>()` registers one closing without `RegisterGenericHandler`.
- An exclusion filters the scan only. A closing declared with `RegisterGenericHandler` registers either way, and a closed generic never comes from a scan, so `ExcludeHandler` on one is a no-op; drop the closing type from the declaration instead.
- An exclusion applies only to the call that declares it, but an assembly is never re-scanned, so the handler stays out until `AddHandler` names it.

## Missing-handler exemptions

Every discovered request and known event must have a handler by default. Exempt messages that this application deliberately leaves unhandled:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Contracts.CreateOrder>()
    .AllowUnhandledRequestsFromAssembly(typeof(Contracts.CreateOrder).Assembly));
```

- `AllowUnhandledRequest<TRequest>()` and `AllowUnhandledRequest(type)` exempt one exact Task, ValueTask, or stream request type.
- `AllowUnhandledEvent<TEvent>()` and `AllowUnhandledEvent(type)` exempt one exact event type. Derived request and event types remain checked.
- `AllowUnhandledRequestsFromAssembly(assembly)` and `AllowUnhandledEventsFromAssembly(assembly)` exempt messages declared in that assembly. Other assemblies remain checked, even within the same registration call.
- Exemptions do not register messages or scan assemblies. An event still needs registration through `AddEvent`, a scan, or an exact closed handler contract before it can be published.
- Type exemptions require concrete, closed message types. Invalid types throw `ArgumentException`; null types or assemblies throw `ArgumentNullException`.
- Repeated exemptions register once. Only missing-handler checks change; all other enabled checks still run.
- An exempt request without a handler throws `HandlerNotFoundException` at dispatch or inspection. An exempt known event gets an empty publication plan. An unknown event throws `EventNotRegisteredException`.

`AllowAllUnhandledRequests()` and `AllowAllUnhandledEvents()` disable the respective missing-handler check for every module in the service collection. Use them when the application accepts missing handlers everywhere.

Replace calls to the former `AllowUnhandledRequests()` and `AllowUnhandledEvents()` with these `AllowAll...` names to keep their global behavior, or choose type or assembly exemptions to narrow it.

## Event options

Event publication uses `SequentialPublishStrategy` by default. Select a global fallback and optional per-event policies:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .PublishAllEventsWith<ParallelPublishStrategy>()
    .PublishEventsWith<IAuditEvent, ThrottledStrategy>(strategy => strategy.AsScoped())
    .PublishEventsWith<OrderPlaced, FailFastPublishStrategy>()
    .AllowAllUnhandledEvents()
    .DisallowUnusedEventHandlers());
```

Strategy declarations accumulate across additive `AddRequestFlow` calls. Identical declarations deduplicate. Conflicting global or same-target declarations produce `RF0119` at freeze.

`PublishAllEventsWith<TStrategy>` supplies the terminal fallback. A per-event declaration beats it when its target is assignable from the concrete event. Exact types beat base classes, base classes beat interfaces, interfaces beat `IEvent`, and `IEvent` beats the global fallback. Unrelated interfaces at the winning tier produce `RF0120`; declare the exact event type to resolve the tie.

Custom strategies are singleton by default. The configure delegate accepts `AsSingleton`, `AsScoped`, or `AsTransient`. Built-in strategies are never registered in DI and reject lifetime configuration. RequestFlow uses `TryAdd` for a custom strategy descriptor, so an application descriptor already present suppresses the generated one. A normal application descriptor added afterwards resolves last. In either order, its lifetime wins over the lifetime named in the options.

`PublishEventsInParallel` starts handlers serially in frozen plan order on the publishing thread, then overlaps only incomplete asynchronous work. It does not use `Task.Run`, and it waits for every handler. See [events.md](events.md#parallel-publication-and-scopes) before enabling it for scoped handlers.

For an exempt event with no handlers, built-in strategies do no work. A custom strategy still resolves and receives an empty delivery. See [Missing-handler exemptions](#missing-handler-exemptions).

`DisallowUnusedEventHandlers` enables `RF0115` for handler contracts and `RF0122` for per-event strategies that reach no known event, even when missing handlers are allowed. It is off by default. Global strategy declarations never produce `RF0122`.

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

`provider.InspectRequestFlow<TRequest>()` runs the same validation and returns the request's declared handler, ordered stages, and stage exclusion reasons. See [Pipeline inspection](pipeline-inspection.md).

## Checks of your own

`AddRequestFlow` returns a builder, and `AddValidationRule` puts a check of yours in the same startup pass:

```csharp
services.AddRequestFlow(o => o.RegisterHandlersFromCallingAssembly())
    .AddValidationRule<RequestNameRule>();
```

The rule sees every registered request, handler, stage, event, and event subscription, and reports into the same exception as the built-in checks. [validation-rules.md](validation-rules.md) covers writing, registering, and testing one.
