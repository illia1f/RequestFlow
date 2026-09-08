# Events

RequestFlow events are in-process publish and subscribe over `IEvent`, `IEventHandler<TEvent>`, and `IEventPublisher`. The default `SequentialPublishStrategy` runs every applicable handler and reports the collected failures together. A built-in or custom strategy changes that policy globally or for one event family.

## Define, register, and publish

An event implements `IEvent` while a handler implements `IEventHandler<TEvent>`, with `HandleAsync` returning a `Task`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

public abstract record OrderEvent : IEvent;

public sealed record OrderPlaced(Guid OrderId) : OrderEvent;

public sealed class SendReceipt : IEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced placed, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

The usual assembly scan picks up both the event and the handler, and the runtime registers `IEventPublisher`:

```csharp
services.AddRequestFlow(options =>
    options.RegisterHandlersFromCallingAssembly());

IServiceProvider provider = services.BuildServiceProvider().ValidateRequestFlow();

using IServiceScope scope = provider.CreateScope();
var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

await publisher.PublishAsync(new OrderPlaced(Guid.NewGuid()), cancellationToken);
```

The publisher is scoped like the dispatchers. Publish inside a scope and finish the call there, because handlers resolve from that same scope. [lifetimes.md](lifetimes.md) has the hosted-service form.

The scan skips abstract classes, interfaces, and open generic event handler definitions. It registers each event handler class under its concrete type rather than as an `IEventHandler<TEvent>` service, so `GetServices<IEventHandler<TEvent>>()` does not find RequestFlow's scanned handlers. Open generic event handlers are not supported; the closed catch-all is `IEventHandler<IEvent>`.

The assembly is not the only unit. `AddEventHandler<THandler>()` registers one handler without scanning its assembly, and `ExcludeEventHandler<THandler>()` keeps one scanned handler out:

```csharp
services.AddRequestFlow(options =>
{
    options.RegisterHandlersFromCallingAssembly();
    options.ExcludeEventHandler<NoisyAuditHandler>();
    if (darkLaunchEnabled)
        options.AddEventHandler<DarkLaunchHandler>();
});
```

- A manual add registers every `IEventHandler<TEvent>` contract the class implements, validated and frozen exactly like a scanned handler. Its exact event types count as known.
- Duplicates collapse. A handler both scanned and added manually, or added twice, delivers once, and the first registration decides the lifetime. `WithScopedHandlers()` covers the same call's manual adds wherever it appears in the delegate.
- A type that is not a concrete event handler class is a startup problem (`RF0015` to `RF0017`).
- Exclusion filters the excluding call's own scan. Request and stream handler contracts on the same class still register, an `AddEventHandler` in the same call still wins, and excluding a type the scan does not find does nothing. A later call naming the same assembly does not re-scan it, so the excluded handler stays out until an `AddEventHandler` names it.
- A closed generic such as `AddEventHandler<AuditHandler<OrderPlaced>>()` works like any concrete class.

Bare `services.AddTransient<IEventHandler<X>, H>()` stays inert either way: resolution is concrete-keyed, so an interface-keyed container registration never joins delivery.

### Register an event without an exact handler

Use `AddEvent<TEvent>()` or `AddEvent(Type)` when a base or catch-all handler covers an event that scanning cannot discover:

```csharp
services.AddRequestFlow(options => options
    .AddEvent<EntitySaved<int>>()
    .AddEventHandler<AuditAllEvents>());

public sealed record EntitySaved<T>(T Entity) : IEvent;

public sealed class AuditAllEvents : IEventHandler<IEvent>
{
    public Task HandleAsync(IEvent @event, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

- Scanning sees `EntitySaved<>`, not constructed types such as `EntitySaved<int>`. Register each closed event type that the application publishes.
- `AddEvent` registers the event type only. Existing handler contracts and publish strategies apply during startup validation.
- Repeated declarations register once across `AddRequestFlow` calls. Invalid event types produce `RF0021`; events without a handler produce `RF0114` unless exempted from missing-handler validation.

## Select a publish strategy

The built-in strategies are public and stateless:

| Strategy | Behavior |
| --- | --- |
| `SequentialPublishStrategy` | Runs every entry in frozen order and collects failures. This is the default |
| `ParallelPublishStrategy` | Starts every entry in frozen order, waits for all, then collects failures |
| `FailFastPublishStrategy` | Runs sequentially and stops after the first failure |

Fail-fast treats a canceled handler task as that entry's failure. It throws `EventPublishException` and sets `SkippedHandlerCount` to the number of later entries that never started.

Select a global fallback or an assignable event target:

```csharp
services.AddRequestFlow(options => options
    .RegisterHandlersFromCallingAssembly()
    .PublishAllEventsWith<ParallelPublishStrategy>()
    .PublishEventsWith<OrderEvent, SequentialPublishStrategy>()
    .PublishEventsWith<OrderPlaced, FailFastPublishStrategy>());
```

Each built-in strategy has shorthand. `PublishEventsInParallel()`, `PublishEventsSequentially()`, and `PublishEventsFailFast()` select one globally; each also has a `<TEvent>` overload for one assignable event target.

Per-event declarations use this precedence:

1. Exact event type.
2. Nearest base class.
3. Event interface. An interface that extends another is more specific.
4. `IEvent`.
5. The global declaration, or `SequentialPublishStrategy` when none exists.

Two unrelated interface declarations at the winning tier produce `RF0120` at startup. Declare the strategy on the exact event type to resolve the tie. An `IEvent` declaration is explicit and beats the global fallback.

Custom strategies resolve from DI for each publish. They are singleton by default:

```csharp
options.PublishEventsWith<OrderEvent, ThrottledStrategy>(strategy =>
    strategy.AsTransient());
```

`AsSingleton`, `AsScoped`, and `AsTransient` configure a custom strategy. Built-ins are selected directly and reject lifetime configuration. If the application registered its own descriptor for a custom strategy, that descriptor wins over RequestFlow's default registration.

- Keep per-publication state, such as a failure list, in method locals. A singleton is shared across publications; a scoped instance is shared by every publication in that scope, including concurrent ones.
- Use `AsScoped()` when a strategy needs scoped dependencies. It does not isolate publications in the same scope.
- Use `AsTransient()` when fields must belong to one publication. DI must create a new instance on each resolution.
- Keep shared state, such as a semaphore or meter, in fields only when its lifetime matches the intended sharing.

The strategy owns the publish outcome:

- `StartAsync(i)` called twice starts entry `i` twice.
- A strategy that ignores the publishing token does not report publisher cancellation.
- A strategy that collects failures in its own order controls the order in `EventPublishException`.
- `delivery.Services` exposes the publishing provider. A test that builds a delivery with `EventDelivery.Over` can omit the provider until the strategy reads that property.

### Port a MediatR notification publisher

MediatR's handler callback throws when a handler fails. `EventDelivery.StartAsync` returns an `EventHandlerFailure` instead. Collect those results and finish with `ThrowIfAny`:

```csharp
public sealed class MigratedPublisher : IEventPublishStrategy
{
    public async Task PublishAsync(
        EventDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            throw delivery.Canceled(null, delivery.Count, cancellationToken);

        List<EventHandlerFailure>? failures = null;
        for (int i = 0; i < delivery.Count; i++)
        {
            if (i > 0 && cancellationToken.IsCancellationRequested)
            {
                throw delivery.Canceled(
                    failures,
                    delivery.Count - i,
                    cancellationToken);
            }

            EventHandlerFailure? failure = await delivery.StartAsync(
                i, cancellationToken);
            if (failure is not null)
            {
                failures ??= [];
                failures.Add(failure);
            }
        }

        delivery.ThrowIfAny(failures);
    }
}

services.AddRequestFlow(options => options
    .RegisterHandlersFromCallingAssembly()
    .PublishAllEventsWith<MigratedPublisher>());
```

Do not discard the result of `StartAsync`. Doing so drops the handler failure and lets the publish succeed. Use `RunAsync` when the strategy should stop and propagate the handler exception directly.

### Propagate the first handler exception

`RunAsync` rethrows the handler's own exception. This loop matches the usual stop-first publisher shape without wrapping the failure in `EventPublishException`:

```csharp
public async Task PublishAsync(
    EventDelivery delivery,
    CancellationToken cancellationToken)
{
    for (int i = 0; i < delivery.Count; i++)
        await delivery.RunAsync(i, cancellationToken);
}
```

### Detach entry work from the publishing token

`CancellationToken.None` means "use the publishing token" for `StartAsync` and `RunAsync`. Pass a real strategy-owned or application-lifetime token to detach work:

```csharp
public sealed class DetachedStrategy(CancellationToken lifetimeToken)
    : IEventPublishStrategy
{
    public async Task PublishAsync(
        EventDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            throw delivery.Canceled(null, delivery.Count, cancellationToken);

        for (int i = 0; i < delivery.Count; i++)
            _ = ObserveAsync(delivery, i, lifetimeToken);

        await Task.CompletedTask;
    }

    private static async Task ObserveAsync(
        EventDelivery delivery,
        int index,
        CancellationToken cancellationToken)
    {
        EventHandlerFailure? failure = await delivery.StartAsync(
            index, cancellationToken);
        if (failure is not null)
            Console.Error.WriteLine(failure.Exception);
    }
}
```

Detached work may outlive the publishing scope. Do not use this pattern when handlers depend on scoped services that can be disposed after `PublishAsync` returns.

## Which handlers receive an event

RequestFlow works out the full handler list when the provider freezes. A handler applies when its declared event type is assignable from the event's runtime type. This means a derived event can be picked up by handlers for its base classes, its interfaces, or `IEvent` as well as its own exact type.

The frozen entry order handed to a strategy has four tiers:

| Tier | Delivery order |
| --- | --- |
| Exact runtime type | First |
| Base classes | Nearest base class first |
| Interfaces | Larger inherited-interface set first |
| `IEvent` | Last |

Unrelated interfaces and the branches of a diamond can have the same set size and tie. RequestFlow breaks those ties on a stable internal key, so the order repeats from one run and one target framework to the next. Order within a tier is not a public compatibility contract and can change in a future release. When two handlers in one tier need a fixed sequence, put them behind a single handler that owns the order.

A custom strategy may reorder, skip, or start an entry more than once. A single class is free to implement multiple applicable event-handler contracts:

```csharp
public sealed class AuditOrder :
    IEventHandler<OrderEvent>,
    IEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderEvent value, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task HandleAsync(OrderPlaced value, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

If `OrderPlaced` is published, this class is invoked twice, once per contract and at the position of each contract. Each such pairing of handler class and declared event type counts as one subscription; with scoped handlers, both subscriptions resolve to the same scoped instance.

## Failures

With the built-in sequential strategy, a handler is resolved immediately before it is invoked. The publisher awaits it, records any failure, and moves on. A handler failure never halts the walk: not a throw before the task is returned, not a DI resolution problem, not a canceled or null handler task. A null task is recorded as an `EventHandlerNullTaskException`.

When cancellation has not stopped the walk, one or more failures produce an `EventPublishException`, even if only a single handler was at fault. Under the built-ins, the `Failures` list keeps frozen entry order. A custom strategy controls collection order. Each `EventHandlerFailure` carries the `HandlerType` (the class that failed), the `DeclaredEventType` (the handler contract used for that invocation), and the original `Exception`. Those same exceptions appear in `EventPublishException.InnerExceptions`, again in order. RequestFlow adds no per-handler wrapper around an exception. A handler task that held several exceptions keeps that task's `AggregateException` on the entry.

Only two errors come out of `PublishAsync` before it returns a task: an `ArgumentNullException` for a null event, and an `EventNotRegisteredException` for a runtime event type with no frozen plan. Everything else is reported through the returned task: synchronous handler throws, service resolution failures, null handler or strategy tasks, publication cancellation, aggregated handler failures, and strategy construction or execution failures.

## Known, unhandled, and unknown events

- A known event is a concrete, closed `IEvent` type found by scanning, named by an exact closed handler contract, or registered with `AddEvent`.
- A known event without an applicable handler produces `RF0114` at startup unless exempted. An exempt event gets an empty plan: built-in strategies do no work; a custom strategy still resolves and runs with `delivery.Count == 0`.
- Abstract event bases, interfaces, and open generic definitions can be handler targets but get no plans of their own.
- Unknown runtime types throw `EventNotRegisteredException`, including unregistered derived types and proxies of known events. Missing-handler exemptions do not register event types.

Use `AllowUnhandledEvent<TEvent>()` for one exact event, `AllowUnhandledEventsFromAssembly(assembly)` for one assembly, or `AllowAllUnhandledEvents()` for all known events. See [Missing-handler exemptions](registration.md#missing-handler-exemptions).

## Cancellation

Each built-in strategy checks its token before any handler runs, empty plan included. Sequential and fail-fast check again before each remaining handler. A check that stops the walk produces an `EventPublishCanceledException` carrying the publishing token and any failures collected so far. Publication is not atomic, so earlier handlers may have finished before cancellation skips the rest. A custom strategy decides when to acknowledge cancellation and whether every entry has already started.

There is no check once the final handler is underway, and no second publisher check after parallel publication has begun. If cancellation stops no work, publication reports what the handlers did: success, or an `EventPublishException`.

| Token outcome | Sequential or fail-fast | Parallel |
| --- | --- | --- |
| Canceled before publication starts, including an empty plan | Nothing runs; `EventPublishCanceledException` has an empty `Failures` list | Same |
| Canceled between handler k and k+1 | Remaining handlers are skipped; the exception keeps failures collected so far | No between-handler check after fan-out starts |
| Canceled after the final handler starts | Success, or `EventPublishException` when a handler failed | Same |
| A handler throws `OperationCanceledException` or returns a canceled task | Recorded as that handler's failure. Sequential continues the walk; fail-fast stops and throws `EventPublishException` with the later entries as `SkippedHandlerCount` | Recorded as that handler's failure; every entry has already started |

When the publisher acknowledges cancellation, the returned task has `Status == Canceled` and a null `Task.Exception`. Awaiting it surfaces the exact `EventPublishCanceledException`, not a stand-in `TaskCanceledException`, and its inherited `CancellationToken` is the token that was passed to `PublishAsync`.

## Parallel publication and scopes

`PublishEventsInParallel()` selects the global `ParallelPublishStrategy`. Entries still begin serially, in frozen plan order, on the thread that called `PublishAsync`. A handler's synchronous prefix runs until it either yields a completed task or hits its first incomplete await. There is no `Task.Run` in RequestFlow; any overlap is limited to unfinished asynchronous work.

The publisher waits for every entry. Failures are then collected in frozen plan order rather than completion order, which keeps fast and slow failures from reordering `EventPublishException.Failures`.

Parallel handlers operate within the scope where `IEventPublisher` was resolved, so two scoped event handlers can end up using the same scoped dependency at once. The same holds for a class with two applicable contracts being invoked concurrently on a single scoped instance. Do not turn on parallel publication if those handlers share something that is not thread-safe, such as an EF Core `DbContext`.

Sequential mode orders one publish, not two. Two `PublishAsync` calls running at the same time can still overlap the same scoped handler and its dependencies. If the scoped services in question cannot be shared, give concurrent units of work separate scopes.

## Registration options

Event declarations and validation flags accumulate through additive `AddRequestFlow` calls:

| Option | Effect |
| --- | --- |
| `PublishAllEventsWith<TStrategy>()` | Selects the global fallback strategy |
| `PublishEventsWith<TEvent, TStrategy>()` | Selects a strategy for an assignable event target |
| `PublishEventsInParallel()` | Selects `ParallelPublishStrategy` globally |
| `PublishEventsSequentially()` | Selects `SequentialPublishStrategy` globally; explicit form of the default |
| `PublishEventsFailFast()` | Selects `FailFastPublishStrategy` globally |
| `PublishEventsInParallel<TEvent>()`, `PublishEventsSequentially<TEvent>()`, `PublishEventsFailFast<TEvent>()` | Select that strategy for an assignable event target |
| `AllowAllUnhandledEvents()` | Suppresses `RF0114` and permits known empty plans |
| `DisallowUnusedEventHandlers()` | Enables `RF0115` for a dead subscription and `RF0122` for a dead strategy declaration |

Missing-handler exemptions do not disable `DisallowUnusedEventHandlers()`: a handler subscription or typed strategy must still reach a known event.

`WithScopedHandlers()` covers the event handlers discovered by that particular registration call. The publisher follows the dispatcher lifetime: scoped by default, transient when `WithTransientDispatcher()` is set. See [service lifetimes](lifetimes.md) for the full registration table.

Events bypass request and stream stages. A `PublishAsync` call goes straight through its frozen event plan to the applicable handlers.

## Notes for MediatR migrations

The behavior warnings below come from black-box probes against MediatR 12.5.0 and 14.2.0 on Microsoft.Extensions.DependencyInjection. They describe those versions and that container, not every MediatR release.

- Delivery to base-class, interface, and catch-all notification handlers was gated on the published type also having an exact-type handler. With one, all of them ran; with none, the publish reached no handler at all. RequestFlow's closure is unconditional: every applicable handler runs whether or not an exact-type handler exists.
- A notification that reached nobody in the probes is delivered under RequestFlow when a base, interface, or `IEvent` handler applies, and becomes an `RF0114` startup problem when none does.
- Do not mistake RequestFlow's same-tier order for registration order or a compatibility guarantee. Review handlers whose side effects depend on the order of exact-type registrations.
- RequestFlow runs every applicable handler unless a publisher cancellation check stops the walk, and aggregates handler failures. Code that catches a handler exception directly around `Publish` needs to inspect `EventPublishException.Failures` instead.
- Where MediatR 12.5.0 permitted a notification without handlers, RequestFlow needs a missing-handler exemption for that event.
- Repeated `AddMediatR` registration delivered notification handlers more than once in the 12.5.0 probes; MediatR 14.1.0 stopped that duplicate delivery. RequestFlow deduplicates scanned assemblies, and manual `AddEventHandler` entries, across additive registration calls.
- Conditional registration ports directly: `if (flag) services.AddTransient<INotificationHandler<X>, H>()` becomes `if (flag) options.AddEventHandler<H>()` inside the configure delegate. Leave the bare container registration behind, because under RequestFlow it does nothing.
- A `TypeEvaluator` scan filter maps to `ExcludeEventHandler<THandler>()` per handler kept out, and an open generic notification handler ports as manually added closings, `options.AddEventHandler<AuditHandler<OrderPlaced>>()` per event.
- Map a MediatR notification publisher to `IEventPublishStrategy` ([the port recipe](#port-a-mediatr-notification-publisher)). Its callback throws on failure, while `EventDelivery.StartAsync` returns a failure. Use `RunAsync` or collect and call `ThrowIfAny`; discarding `StartAsync` results drops failures.
- RequestFlow has no event stages in v1.

See [registration](registration.md), [service lifetimes](lifetimes.md), and [exceptions](exceptions.md) for the surrounding APIs.
