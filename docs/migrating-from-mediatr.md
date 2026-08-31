# Migrating from MediatR

Most request and handler files need only namespace, interface, and method-name changes. Registration, stages, events, and dynamic dispatch behave differently and need a closer review.

This guide compares public APIs and behavior visible to callers. RequestFlow is a clean-room implementation and does not use MediatR source.

## Choose the packages

- Install `RequestFlow` for plain request, stream, and event dispatch.
- Install `RequestFlow.Cqrs` when commands and queries should use distinct contracts and dispatcher interfaces. It includes the core runtime transitively.
- Reference `RequestFlow.Abstractions` or `RequestFlow.Cqrs.Abstractions` directly from projects that should not depend on runtime registration.

```bash
dotnet remove package MediatR
dotnet add package RequestFlow --prerelease
```

For the typed command/query split:

```bash
dotnet add package RequestFlow.Cqrs --prerelease
```

## Concept map

| MediatR | RequestFlow |
| --- | --- |
| `IRequest<TResponse>` | `IRequest<TResponse>` |
| `IRequest` | `IRequest` |
| `IRequestHandler<TRequest, TResponse>` | `IRequestHandler<TRequest, TResponse>` |
| handler `Handle` | handler `HandleAsync` |
| `ISender` or `IMediator.Send` | `IRequestDispatcher.SendAsync` |
| `IPipelineBehavior<TRequest, TResponse>` | `IRequestStage<TRequest, TResponse>` |
| `RequestHandlerDelegate<TResponse>` | `Continuation<TResponse>` |
| `IStreamRequest<TItem>` | `IStreamRequest<TItem>` |
| `IStreamRequestHandler<TRequest, TItem>` | `IStreamRequestHandler<TRequest, TItem>` |
| `IStreamPipelineBehavior<TRequest, TItem>` | `IStreamRequestStage<TRequest, TItem>` |
| `CreateStream` | `IStreamDispatcher.Stream` |
| `INotification` | `IEvent` |
| `INotificationHandler<TEvent>` | `IEventHandler<TEvent>` |
| notification handler `Handle` | event handler `HandleAsync` |
| `IPublisher.Publish` or `IMediator.Publish` | `IEventPublisher.PublishAsync` |
| `INotificationPublisher` | `IEventPublishStrategy` |
| `services.AddMediatR(...)` | `services.AddRequestFlow(...)` |

All RequestFlow core contracts use the `RequestFlow` namespace. CQRS contracts use `RequestFlow.Cqrs`.

## Migrate requests and handlers

Response requests keep the same generic shape. Rename `Handle` to `HandleAsync`; RequestFlow handler interfaces require a cancellation token.

```csharp
using RequestFlow;

public sealed record GetOrderQuery(Guid Id) : IRequest<Order?>;

public sealed class GetOrderQueryHandler(OrderStore store)
    : IRequestHandler<GetOrderQuery, Order?>
{
    public Task<Order?> HandleAsync(
        GetOrderQuery query,
        CancellationToken cancellationToken)
        => Task.FromResult(store.Find(query.Id));
}
```

A void request handler returns plain `Task`:

```csharp
public sealed record CancelOrderCommand(Guid Id) : IRequest;

public sealed class CancelOrderCommandHandler(OrderStore store)
    : IRequestHandler<CancelOrderCommand>
{
    public Task HandleAsync(
        CancelOrderCommand command,
        CancellationToken cancellationToken)
    {
        store.Cancel(command.Id);
        return Task.CompletedTask;
    }
}
```

Do not put `NoResult` in request or handler signatures. It exists only to keep the internal request machinery uniform.

Request handlers keep `Task` and `Task<T>`, so their await and multiple-await behavior does not change.

## Replace sender injection

Replace `ISender` or `IMediator` with the narrow dispatcher used by the caller:

```csharp
public sealed class OrdersEndpoint(IRequestDispatcher requests)
{
    public Task<Order?> GetAsync(Guid id, CancellationToken cancellationToken)
        => requests.SendAsync(new GetOrderQuery(id), cancellationToken);
}
```

With `RequestFlow.Cqrs`, use `ICommandDispatcher`, `IQueryDispatcher`, or `IStreamQueryDispatcher`. A query-only caller then cannot send a command through that dependency.

RequestFlow has no untyped `Send(object)` overload. If the application relies on untyped dispatch for deserialization or generic endpoints, keep that path on a mediator that supports it or add a typed adapter.

## Replace registration

Register every assembly that contributes request handlers, stream handlers, event handlers, or known event types:

```csharp
builder.Services
    .AddRequestFlow(options =>
    {
        options.RegisterHandlersFromCallingAssembly();
        options.RegisterHandlersFromAssemblyContaining<OrdersModuleMarker>();
    })
    .AddCqrs();
```

Repeated `AddRequestFlow` calls are additive. A modular monolith can keep one registration method per module:

```csharp
builder.Services.AddOrdersModule().AddCqrs();
builder.Services.AddAuditModule();
```

Call `ValidateRequestFlow()` during startup to validate the complete registration and freeze its plans:

```csharp
WebApplication app = builder.Build();
app.Services.ValidateRequestFlow();
```

Without the explicit call, the first request dispatcher, stream dispatcher, or event publisher resolution performs the same freeze.

### Conditional handlers

A handler registered only in the DI container does not enter RequestFlow's dispatch map. Put conditional registration inside the RequestFlow options:

```csharp
builder.Services.AddRequestFlow(options =>
{
    options.RegisterHandlersFromCallingAssembly();
    options.ExcludeHandler<ExternalPricingHandler>();

    if (features.UseExternalPricing)
        options.AddHandler<ExternalPricingHandler>();
});
```

The exclusion keeps the scan from registering `ExternalPricingHandler` when the flag is off. The manual add restores it when the flag is on. Use the same exclusion-first pattern with `ExcludeEventHandler<THandler>()` and `AddEventHandler<THandler>()` for conditional event handlers.

## Replace pipeline behaviors with stages

A request stage receives the request, a readonly continuation for the chain below it, and the cancellation token:

```csharp
public sealed class LoggingStage<TRequest, TResponse>(ILogger<LoggingStage<TRequest, TResponse>> logger)
    : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        Continuation<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        return await next.InvokeAsync(cancellationToken);
    }
}
```

Register stages in execution order, outermost first:

```csharp
options.AddStage(typeof(LoggingStage<,>));
options.AddStage(typeof(ValidationStage<,>));
```

RequestFlow closes each applicable stage for each handler at startup. Filters and generic constraints decide which handlers a stage reaches. Invalid stage declarations fail startup validation. A declaration that reaches no request is allowed unless registration calls `DisallowUnusedStages()`.

RequestFlow has no separate request pre-processor, post-processor, exception-handler, or exception-action abstractions. Implement those concerns as stages or keep an application-owned adapter during migration.

## Migrate streams

Stream request and handler contracts keep their generic shape. Task and stream handlers use the same assembly scan.

- Replace stream creation with `IStreamDispatcher.Stream(request, cancellationToken)`.
- Replace stream pipeline behaviors with `IStreamRequestStage<TRequest, TItem>`.
- Register open stages with `AddStreamStage(typeof(Stage<,>))`.
- Keep `Handle`, `Stream`, and the stream continuation's `Invoke` without an `Async` suffix. Each returns `IAsyncEnumerable<TItem>` synchronously.

Handler and stage execution begins when the returned sequence is enumerated, not when `Stream` returns.

## Migrate notifications as events

Rename notification contracts and handler methods:

```csharp
public sealed record OrderPlaced(Guid OrderId) : IEvent;

public sealed class AuditOrderPlaced : IEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced placed, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

Publish through `IEventPublisher.PublishAsync`.

### Delivery closure

At startup, RequestFlow matches every known concrete event to its applicable handler contracts. Delivery tiers are:

1. exact event type;
2. base classes, nearest first;
3. interfaces, most specific first;
4. `IEvent`.

A base or interface handler can receive a derived event even when no exact-type handler exists. One handler class that implements two applicable contracts is invoked once for each contract.

### Ordering, concurrency, and failures

The default strategy runs handlers sequentially and attempts every applicable entry. Failures are collected into `EventPublishException.Failures` after all started entries finish.

`PublishEventsInParallel<TEvent>()` starts applicable handlers in frozen order and overlaps only incomplete asynchronous work for the selected event family. It waits for every handler. `PublishEventsFailFast<TEvent>()` stops the sequential walk after a failure. Custom strategies implement `IEventPublishStrategy`.

Tier order is part of the contract. Same-tier handler order is not a compatibility promise. Remove code that depends on notification registration order or make the order explicit inside an application handler.

RequestFlow does not have event stages. Put event-wide policy in an event publish strategy or put handler-specific policy in the handler.

Open generic event handler classes are not discovered automatically. Register each closed handler with `AddEventHandler<THandler>()`, use a closed `IEventHandler<IEvent>` catch-all, or keep an application adapter.

## Validate the migrated application

Call `ValidateRequestFlow()` during startup, then fix every reported problem:

- every request and stream request has exactly one handler;
- a request does not declare conflicting response contracts;
- every stage declaration is valid; a declaration that reaches no request is reported only under `DisallowUnusedStages()`;
- every known event has a handler unless unhandled events are explicitly allowed;
- every event strategy declaration selects an applicable concrete strategy;
- every command and query stays on one side of the CQRS split;
- application-defined validation rules pass.

Add integration tests for event fan-out and any code that previously depended on untyped dispatch, processor abstractions, exception abstractions, or notification ordering.

## Compatibility limits

RequestFlow supports .NET 10, .NET 8, `netstandard2.0`, and .NET Framework 4.6.2. Trimming and NativeAOT are not supported. Read [Compatibility](compatibility.md) before changing publish settings.
