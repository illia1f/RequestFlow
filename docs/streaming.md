# Streaming

A stream request is dispatched to exactly one handler, like any other request, but the handler hands back an `IAsyncEnumerable<TItem>` instead of a response. Items reach the caller as the handler produces them. If you are coming from MediatR, `IStreamRequest<TItem>` is its `IStreamRequest<TResponse>` and `IStreamDispatcher.Stream` is its `CreateStream`.

## Packages

- The composition root installs `RequestFlow`, as it already does.
- An assembly that declares stream requests, writes stream handlers, or writes stream stages, but does no registration, references `RequestFlow.Abstractions` alone. The core streaming contracts live there: `IStreamRequest<TItem>`, `IStreamRequestHandler`, `IStreamRequestStage`, `StreamContinuation<TItem>`, and `IStreamDispatcher`.
- An assembly on the CQRS split references `RequestFlow.Cqrs.Abstractions` instead and declares `IStreamQuery<TItem>` with `IStreamQueryHandler<TQuery, TItem>`. Both derive from the core contracts, so everything on this page applies to them unchanged. `AddCqrs` registers `IStreamQueryDispatcher`, the read-side entry point.

`RequestFlow.Abstractions` carries `Microsoft.Bcl.AsyncInterfaces` on `netstandard2.0` and `net462`, the two targets where `IAsyncEnumerable<T>` is not in the framework. On `net8.0` and `net10.0` it has no dependencies.

## A stream request and its handler

Implement `IStreamRequest<TItem>` on the request and `IStreamRequestHandler<TRequest, TItem>` on the handler. The method is `Handle`, with no `Async` suffix, because it returns its sequence synchronously and there is nothing to await:

```csharp
using RequestFlow;

public interface IHasVisibility
{
    bool IsVisible { get; }
}

public sealed record OrderRow(Guid Id, string CustomerEmail, bool IsVisible) : IHasVisibility;

public sealed record ExportOrders(DateTimeOffset Since) : IStreamRequest<OrderRow>;
```

```csharp
using System.Runtime.CompilerServices;
using RequestFlow;

public sealed class ExportOrdersHandler(OrderStore store) : IStreamRequestHandler<ExportOrders, OrderRow>
{
    public async IAsyncEnumerable<OrderRow> Handle(
        ExportOrders request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (OrderRow row in store.ReadSince(request.Since, cancellationToken))
        {
            yield return row;
        }
    }
}
```

There is no void form and no `NoResult` here. A handler that has nothing to yield returns an empty sequence.

One request type carries one contract. Two `IStreamRequest<TItem>` interfaces on one type fail startup validation as `RF0108`, and an `IRequest<TResponse>` and an `IStreamRequest<TItem>` together fail as `RF0109`, because the dispatch map holds one plan per request type.

The handler's item type has to be the one the request declares. `IStreamRequest<TItem>` is covariant, so a handler declaring a wider item, `IStreamRequestHandler<ExportOrders, object>` over a request declaring `IStreamRequest<OrderRow>`, compiles; `Stream` infers the declared item type, so dispatching it would only ever throw. Startup validation reports the pair as `RF0110`.

## Dispatching

Inject `IStreamDispatcher` and call `Stream`:

```csharp
await foreach (OrderRow row in dispatcher.Stream(new ExportOrders(since), ct))
{
    Console.WriteLine(row.Id);
}
```

For an HTTP endpoint, see [HTTP responses](#http-responses) before returning a stream.

There is no `AddStreaming` call. `AddRequestFlow` finds stream handlers in the same scan as the rest and registers `IStreamDispatcher` beside `IRequestDispatcher`, on the same lifetime and over the same frozen map:

```csharp
services.AddRequestFlow(o => o.RegisterHandlersFromCallingAssembly());
```

Resolving either dispatcher runs the one validation pass and freezes the one map, so a stream handler and a task handler in the same assembly are validated together.

## What throws, and when

`Stream` is not an iterator, so the lookup runs on the call rather than on the first `MoveNextAsync`. That splits the failures in two.

From the `Stream` call:

- `ArgumentNullException` when `request` is null.
- `HandlerNotFoundException` when the request's runtime type has no registered handler. This can happen for an unscanned, exempt, or derived type. Other discovered requests without handlers fail startup validation as `RF0102`. See [exceptions.md](exceptions.md#handlernotfoundexception).
- `ResponseTypeMismatchException` when the call site's `TItem` differs from the registered item type. `IStreamRequest<out TItem>` is covariant, so an upcast compiles and then misses, exactly as on the task path.

From enumeration:

- `HandlerNullStreamException` when the handler returned `null` instead of a sequence, and `StageNullStreamException` for a stage in the same position. Both derive from `NullStreamException`. Return an empty sequence to yield nothing.
- The container's own exception when a level fails to resolve, a handler with a missing constructor dependency for example. Levels resolve when the chain runs, not on the `Stream` call ([exceptions.md](exceptions.md#what-requestflow-never-wraps)).
- Anything the handler or a stage throws while producing items.

Execution and scope:

- `Stream` checks for a matching plan. Handlers and stages start on the first `MoveNextAsync`, normally through `await foreach`, and resolve their services as execution reaches them.
- Finish enumeration or dispose the enumerator before disposing the dispatching scope. See [lifetimes.md](lifetimes.md#why-the-dispatcher-is-scoped) for the worker pattern.

## HTTP responses

- Complete checks that determine the HTTP status, such as authorization and request validation, before returning a stream or starting the response body. Returning from `Stream` does not mean a stage has approved the request.
- `ValidateRequestFlow()` checks registration, not an individual request's values or permissions.
- Later items can fail even after the first succeeds. Once the response starts, a failure cannot change its status.

## Cancellation

Two tokens reach the walk, and either one cancels the levels running under them:

```csharp
await foreach (OrderRow row in dispatcher.Stream(request, dispatchToken))
{
}

await foreach (OrderRow row in dispatcher.Stream(request).WithCancellation(enumerationToken))
{
}
```

RequestFlow joins the two once, when the walk starts, and passes the joined token to every level and to the handler. Supplying only one of them, or the same token to both, allocates no linked source, because there is nothing to join.

A stage decides what runs below it. `next.Invoke(token)` replaces the joined token for every level under that stage, the handler included, so neither of the caller's tokens reaches them. RequestFlow does not put the join back on the sequence it returns, because that would override the stage. A stage that means to keep the caller's reach passes `cancellationToken` through, or calls `Invoke()` with no argument.

Cancellation stays cooperative. The library never inspects the token and never throws on it by itself; a handler that ignores its token cannot be stopped by either path.

## [EnumeratorCancellation]

A stream handler or stage written as an async iterator decorates its `CancellationToken` parameter with `[EnumeratorCancellation]`, from `System.Runtime.CompilerServices`. Every handler and stage on this page does. Without it the compiler raises the warning `CS8425`, which builds as an error in a project that treats warnings as errors.

The attribute links the enumeration token with the one the dispatch passed. It does not replace either, so the dispatch token still reaches the body. RequestFlow has already joined both tokens before the handler sees either one, so the attribute finds them equal or finds one of them default, and adds nothing.

A handler written without the attribute loses nothing. The token it receives as an ordinary argument is already the joined one, the `WithCancellation` token included, so what the attribute buys here is silencing `CS8425`.

## Stream stages

A stream stage implements `IStreamRequestStage<TRequest, TItem>` and registers with `AddStreamStage`. It receives a `StreamContinuation<TItem>`, and `next.Invoke()` hands back the sequence of the rest of the chain rather than a task. So a stage can observe items, drop them, replace them, add its own, or stop the walk early.

An open generic stage applies to every stream request its constraints admit. `OrderRow` implements `IHasVisibility`, so this one reaches `ExportOrders` and every other stream request whose items do:

```csharp
using System.Runtime.CompilerServices;
using RequestFlow;

public sealed class VisibleOnlyStage<TRequest, TItem> : IStreamRequestStage<TRequest, TItem>
    where TRequest : IStreamRequest<TItem>
    where TItem : IHasVisibility
{
    public async IAsyncEnumerable<TItem> Handle(
        TRequest request,
        StreamContinuation<TItem> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (TItem item in next.Invoke(cancellationToken))
        {
            if (item.IsVisible)
                yield return item;
        }
    }
}
```

A closed stage targets one request contract:

```csharp
public sealed class RedactStage : IStreamRequestStage<ExportOrders, OrderRow>
{
    public async IAsyncEnumerable<OrderRow> Handle(
        ExportOrders request,
        StreamContinuation<OrderRow> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (OrderRow row in next.Invoke(cancellationToken))
        {
            yield return row with { CustomerEmail = "redacted" };
        }
    }
}
```

Registration order is execution order, outermost first:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .AddStreamStage(typeof(VisibleOnlyStage<,>))
    .AddStreamStage<RedactStage>());
```

Everything [stages.md](stages.md) says about constraints, contravariance, `WhereHandlerImplements`, lifetimes, duplicate registrations, and `DisallowUnusedStages` holds here too. Four things differ:

- The two chains never mix. A stream stage never wraps a task handler, and a stage added with `AddStage` never wraps a stream handler. Neither says so at startup; the stage is absent from the other chain. Passing a type to the wrong call is what fails, as `RF0011`, because it implements no contract of the family it was declared in.
- There is no void form, so no `IStreamRequestStage<TRequest>` and no `NoResult`.
- A stage's item type has to be the one its request declares, under the same covariance trap handlers fall into: `IStreamRequestStage<ExportOrders, object>` compiles, but closing matches the item type exactly, so the stage would wrap nothing. Startup validation reports a stage that wraps nothing anywhere as `RF0111`; a stage that wraps at least one request is read as scoped to those requests on purpose and is not reported.
- Skipping `next` short-circuits into the stage's own sequence rather than into a single response. Nothing below is built, the handler included, so a cache stage that answers from memory never pays for the repository behind it. Yield nothing to end the stream there.

Calling `Invoke` is what builds the level below, not enumerating what it hands back. A stage that invokes and then drops the sequence has already resolved the stage or handler under it and called its `Handle`, so short-circuiting means not calling `Invoke` at all.

A caller that breaks out of the `await foreach` early ends the stage where it stands. The stage never resumes past its last `yield return`, so code written after its own loop does not run. Disposal does run, and it cascades to every level below, the handler included, so a timing or transaction stage puts its closing work in a `finally` around the loop rather than after it.

## Testing a stage on its own

`StreamContinuation<TItem>.Over` builds the `next` a stage expects from a delegate standing in for the rest of the chain, so a stage runs in a test with no container:

```csharp
[Fact]
public async Task Given_A_Row_With_An_Email_When_The_Stage_Runs_Then_It_Is_Redacted()
{
    StreamContinuation<OrderRow> next = StreamContinuation<OrderRow>.Over(_ => OneRow());

    List<OrderRow> rows = [];
    await foreach (OrderRow row in new RedactStage().Handle(
        new ExportOrders(default), next, CancellationToken.None))
    {
        rows.Add(row);
    }

    rows.ShouldHaveSingleItem().CustomerEmail.ShouldBe("redacted");
}

private static async IAsyncEnumerable<OrderRow> OneRow()
{
    await Task.CompletedTask;
    yield return new OrderRow(Guid.NewGuid(), "buyer@example.com", IsVisible: true);
}
```

The stand-in chain is a method like `OneRow` rather than an inline lambda, because `yield` inside a lambda is `CS1621`.

The delegate is handed whatever token the stage passed to `Invoke`, so a stage that substitutes a token for the levels below it is testable through the token the delegate sees. A call to `Invoke()` with no token falls back to the token the stage itself received, and so does a call passing `CancellationToken.None`, which reads as omitting it. `Over`'s optional second argument is what the fallback lands on in a test.

The default value of the type has no chain under it. Hand a stage `default(StreamContinuation<TItem>)` and the first `Invoke` throws an `InvalidOperationException` saying so. Build one with `Over` instead.

## What a stream stage costs

A request stage costs nothing per level: a staged dispatch allocates what a stageless one allocates. A stream stage is not free, and what it costs depends on how it is written.

- A stage that returns `next.Invoke(...)` directly is not an async iterator. It has no state machine and no enumerator of its own, and allocates nothing over a chain without it.
- A stage written as an async iterator, which is what filtering and transforming need, allocates its state machine and its enumerator once per enumeration. A second such stage costs the same again, so the cost is per level and additive.
- Neither cost grows with the number of items. It is paid when the enumeration starts, not per `yield return`, so a long stream is no more expensive per stage than a short one.

`StreamChainAllocationTests` in the repository pins each of those.
