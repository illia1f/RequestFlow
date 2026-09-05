# ValueTask requests

`IValueRequest` is the opt-in request family for handlers that return `ValueTask` or
`ValueTask<TResponse>`. Existing `IRequest` contracts keep their `Task` return types.

## When to use them

Use `IValueRequest` only after measuring a synchronously completing hot path. Keep `IRequest` as
the default because a `Task` can be awaited more than once and is harder to misuse.

## Typed requests

A typed ValueTask request declares its response through `IValueRequest<TResponse>`. Its handler
implements `IValueRequestHandler<TRequest, TResponse>`:

```csharp
public sealed record GetOrder(Guid Id) : IValueRequest<Order?>;

public sealed class GetOrderHandler(IOrderStore store)
    : IValueRequestHandler<GetOrder, Order?>
{
    public ValueTask<Order?> HandleAsync(
        GetOrder request,
        CancellationToken cancellationToken)
        => new(store.Find(request.Id));
}
```

Constructing the `ValueTask` directly keeps a completed response inline. An `async ValueTask<T>`
handler is still valid when the work can suspend.

## Void requests

A void request implements `IValueRequest`. Its handler returns plain `ValueTask` and does not name
`NoResult`:

```csharp
public sealed record WarmOrder(Guid Id) : IValueRequest;

public sealed class WarmOrderHandler(IOrderStore store)
    : IValueRequestHandler<WarmOrder>
{
    public ValueTask HandleAsync(
        WarmOrder request,
        CancellationToken cancellationToken)
    {
        store.Warm(request.Id);
        return default;
    }
}
```

`return default;` is a successfully completed `ValueTask`. Use that form for direct void completion
on every target. The downlevel `System.Threading.Tasks.Extensions` 4.5.4 reference has no static
helper for a completed void value.

## Register and send

Assembly scanning and manual handler registration recognize both ValueTask handler shapes. No
ValueTask-specific handler registration call is needed:

```csharp
services.AddRequestFlow(options => options
    .RegisterHandlersFromCallingAssembly());

IServiceProvider provider = services.BuildServiceProvider().ValidateRequestFlow();

using IServiceScope scope = provider.CreateScope();
var dispatcher = scope.ServiceProvider.GetRequiredService<IValueRequestDispatcher>();

Order? order = await dispatcher.SendAsync(new GetOrder(orderId), cancellationToken);
await dispatcher.SendAsync(new WarmOrder(orderId), cancellationToken);
```

`AddHandler<THandler>()`, `ExcludeHandler<THandler>()`, `RegisterGenericHandler`, handler lifetimes,
and startup validation apply to this family under the same rules as Task handlers. See
[Registration](registration.md) and [Service lifetimes](lifetimes.md).

## ValueTask stages

ValueTask stages implement `IValueRequestStage<TRequest, TResponse>` or the void
`IValueRequestStage<TRequest>` form. They receive `ValueContinuation<TResponse>` or
`ValueContinuation` for the chain below them:

```csharp
public sealed class LoggingValueStage<TRequest, TResponse>
    : IValueRequestStage<TRequest, TResponse>
    where TRequest : IValueRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        ValueContinuation<TResponse> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        return await next.InvokeAsync();
    }
}

services.AddRequestFlow(options => options
    .RegisterHandlersFromCallingAssembly()
    .AddValueStage(typeof(LoggingValueStage<,>)));
```

Task, ValueTask, and stream stage chains are homogeneous. `AddStage` reaches Task handlers,
`AddValueStage` reaches ValueTask handlers, and `AddStreamStage` reaches stream handlers. The
continuations otherwise share the same ordering, retry, token-replacement, filtering, lifetime,
and isolated-testing rules described in [Stages](stages.md).

## CQRS commands and queries

`RequestFlow.Cqrs` provides ValueTask twins for commands and queries:

- `IValueCommand<TResponse>`, `IValueCommand`, and their handler contracts;
- `IValueQuery<TResponse>` and `IValueQueryHandler<TQuery, TResponse>`;
- `IValueCommandDispatcher` and `IValueQueryDispatcher`.

Call `AddCqrs()` after `AddRequestFlow()`, then inject the narrow dispatcher. ValueTask commands and
queries use the core scan, validation, handler lifetimes, and ValueTask stage chain. There is no
ValueTask stream-query twin because streaming already returns `IAsyncEnumerable<TItem>`.

## Consume once

Normally await each returned `ValueTask` once. This applies to results from `SendAsync`, handler
calls, and every `ValueContinuation.InvokeAsync` call. Some `ValueTask` instances wrap an
`IValueTaskSource` that permits only one consumption.

If several consumers must observe one result, call `AsTask()` once and share the returned `Task`:

```csharp
ValueTask<Order?> pending = dispatcher.SendAsync(new GetOrder(orderId), cancellationToken);
Task<Order?> shared = pending.AsTask();

Order? first = await shared;
Order? second = await shared;
```

## Default is a valid result

`default(ValueTask)` is a successful completed operation. `default(ValueTask<T>)` is also
successful and yields `default(T)`. A typed handler that accidentally returns `default` can
therefore produce a valid-looking null, zero, or default struct value.

ValueTask is a struct and cannot be null, so this family has no counterpart to
`HandlerNullTaskException`, `StageNullTaskException`, or `NullTaskException`. Return an explicit
typed value from every typed path. Reserve direct `return default;` for an intentional void
completion.

## Exceptions and cancellation

ValueTask dispatch uses the same request errors as Task dispatch:

- A null request throws `ArgumentNullException`.
- A missing plan throws `HandlerNotFoundException`.
- Dispatch requires the exact declared response type. `IValueRequest<out TResponse>` is covariant,
  but sending a widened contract throws `ResponseTypeMismatchException`.
- Synchronous throws and faulted ValueTasks propagate without a RequestFlow wrapper.
- Awaiting a canceled ValueTask surfaces its cancellation outcome.

Cancellation is cooperative. RequestFlow passes the caller's token to each stage and the handler.
A stage can pass another token to `next.InvokeAsync(token)` for the levels below it. Omitting the
token, or passing `CancellationToken.None`, preserves the token handed to the stage. RequestFlow
does not cancel work on its own. See [Exceptions](exceptions.md) for the complete reference.
