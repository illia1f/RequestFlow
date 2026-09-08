# Stages

A stage wraps the handler of every request it applies to. It can run code before and after the handler, replace the response, or skip the handler entirely. If you are coming from MediatR, `IRequestStage<TRequest, TResponse>` is its `IPipelineBehavior<TRequest, TResponse>`.

## Writing a stage

Implement `IRequestStage<TRequest, TResponse>`. Invoking `next` runs the rest of the chain, ending at the handler:

```csharp
using RequestFlow;

public sealed class LoggingStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        TResponse response = await next.InvokeAsync();
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return response;
    }
}
```

A stage has four ways to use `next`:

- Await `next.InvokeAsync()` once and return its result: the normal pass-through.
- Return without invoking `next` to skip the handler and inner stages. None of them are resolved from DI.
- Await `next.InvokeAsync()`, then invoke it again to retry the rest of the chain. Each call resolves inner stages and the handler again: transient instances are new; scoped instances are reused.
- Call `next.InvokeAsync()` again before the first call finishes to run two copies of the chain concurrently. Each call keeps its own token. Scoped and singleton stages, handlers, and dependencies can be shared, so they must support concurrent use.

`next` is a struct that can be copied. Every copy enters the same inner chain with the same request, provider, and token. Do not retain it after the dispatch ends.

A stage must observe every call it starts. Otherwise, an abandoned call can keep running and leave failures unobserved:

- A second `next.InvokeAsync` can throw before returning a task while the first call is still running.
- If `await first` throws, execution never reaches `await second`.

Start the second call inside a `try`, then await both together rather than one after the other:

```csharp
Task<TResponse> first = next.InvokeAsync();

Task<TResponse> second;
try
{
    second = next.InvokeAsync();
}
catch
{
    // Nobody else will await the first walk.
    await ObserveAsync(first);
    throw;
}

TResponse[] responses = await Task.WhenAll(first, second);
```

`Task.WhenAll` observes both outcomes even if one fails. Sequential awaits can leave the second task unobserved.

Implement `ObserveAsync` to await the first task and suppress its exception, preserving the second call's failure.

`WhenAll` waits for the slowest call. For hedging, cancel the unused call through its token and observe its completion and failure.

## ValueTask stages

ValueTask requests use `IValueRequestStage<TRequest, TResponse>` and the void
`IValueRequestStage<TRequest>` form. Their continuations are `ValueContinuation<TResponse>` and
`ValueContinuation`, and they register through `AddValueStage`.

`AddStage` wraps Task handlers, `AddValueStage` wraps ValueTask handlers, and `AddStreamStage` wraps stream handlers. See [ValueTask requests](value-tasks.md#valuetask-stages) for examples and consumption rules.

## Cancellation

`next.InvokeAsync()` keeps the stage's token. Pass another token to replace it for the rest of the chain, as in this timeout stage:

```csharp
public sealed class TimeoutStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    public async Task<TResponse> HandleAsync(
        TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(Limit);

        try
        {
            return await next.InvokeAsync(linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{typeof(TRequest).Name} took longer than {Limit}.");
        }
    }
}
```

The replacement token applies to all inner stages and the handler unless another stage replaces it. Outer stages keep their original tokens.

`CancellationToken.None` and `default` preserve the stage's token. To disable cancellation below a stage, pass a token from a source that nobody cancels.

After canceling a timed-out call, await its completion before throwing. Otherwise, an outer retry can start while the first handler still holds resources.

Cancellation is cooperative: a handler must observe its token to stop.

## Registering

`AddStage` chains on the same configure delegate as the scanning options. Registration order is execution order, outermost first:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .AddStage(typeof(LoggingStage<,>))
    .AddStage(typeof(ValidationStage<,>)));
```

Every request both stages apply to runs logging, then validation, then its handler. A closed stage type can also register through the generic form: `AddStage<AuditStage>()`.

One stage type belongs to a chain once. Registering the same type twice fails startup validation, whatever the two calls filtered on. Two declarations that reach one request as the same stage class fail too, an open definition next to its own closed form for example. `AddStage` calls from separate `AddRequestFlow` calls combine into one chain, in call order, and validate together (see [registration.md](registration.md) on additive calls).

## Which requests a stage reaches

An open generic stage applies to every request its own generic constraints admit. Constrain `TRequest` and the chain follows:

```csharp
public interface IAudited
{ }

public sealed class AuditStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IAudited
{
    public async Task<TResponse> HandleAsync(
        TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
    {
        TResponse response = await next.InvokeAsync();
        // write the audit record
        return response;
    }
}
```

`AuditStage` wraps requests implementing `IAudited`. Its constraints are checked at startup.

A closed stage targets the request contract it names. `TRequest` is contravariant, so a stage closed over a base request type also wraps the requests that derive from it.

A stage can also declare one type parameter and fix the response, the shape a codebase with a shared result type uses:

```csharp
public sealed class ErrorTranslationStage<TRequest> : IRequestStage<TRequest, Result>
    where TRequest : IRequest<Result>
{
    public async Task<Result> HandleAsync(
        TRequest request, Continuation<Result> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next.InvokeAsync();
        }
        catch (DomainException e)
        {
            return Result.Fail(e.Message);
        }
    }
}
```

It wraps every request that returns `Result` and nothing else.

### Filtering on a handler contract

`WhereHandlerImplements` narrows a stage to requests whose handler implements a contract:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .AddStage(typeof(TransactionStage<,>), s => s.WhereHandlerImplements<IWriteHandler>()));
```

The filter looks at the handler class, not the request, so a module can mark its write handlers with one empty interface and wrap them all. A stage takes one filter; a second `WhereHandlerImplements` call throws.

## Void requests

A stage for void requests implements `IRequestStage<TRequest>`, takes the void form `Continuation`, and returns plain `Task`:

```csharp
public sealed class CacheClearGuard : IRequestStage<ClearCache>
{
    public Task HandleAsync(ClearCache request, Continuation next, CancellationToken cancellationToken)
        => next.InvokeAsync();
}
```

Both forms mix in one chain, in registration order. An open two-parameter stage whose constraints admit a void request wraps it too, with `NoResult` as the response.

## Testing a stage on its own

Use `Continuation<TResponse>.Over` or `Continuation.Over` to test a stage without a container. The delegate stands in for the rest of the chain:

```csharp
[Fact]
public async Task Given_A_Failing_Chain_Then_The_Stage_Retries_Once()
{
    int calls = 0;
    Continuation<Receipt> next = Continuation<Receipt>.Over(_ =>
        ++calls == 1 ? Task.FromException<Receipt>(new TimeoutException()) : Task.FromResult(new Receipt()));

    await new RetryStage().HandleAsync(new PlaceOrder(), next, CancellationToken.None);

    calls.ShouldBe(2);
}
```

The delegate receives the token passed to `InvokeAsync`, or the optional second argument to `Over` when no token is passed:

```csharp
CancellationToken observed = default;
Continuation<Receipt> next = Continuation<Receipt>.Over(
    token =>
    {
        observed = token;
        return Task.FromResult(new Receipt());
    },
    ambient);
```

A default continuation has no chain; `InvokeAsync` throws `InvalidOperationException`. Create test continuations with `Over`.

## Unused stages

A stage that reaches no registered request is a silent no-op by default. `DisallowUnusedStages` makes it a startup validation problem instead:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .AddStage(typeof(AuditStage<,>))
    .DisallowUnusedStages());
```

Once any `AddRequestFlow` call enables `DisallowUnusedStages`, every registered stage is checked.

## Lifetime

Each stage has its own lifetime. For example, a stateless logging stage can be singleton while a unit-of-work stage is scoped:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .AddStage(typeof(LoggingStage<,>), s => s.AsSingleton())
    .AddStage(typeof(UnitOfWorkStage<,>), s => s.AsScoped()));
```

- Stages are transient by default: each entry, including a retry, gets a new instance.
- Naming different lifetimes, such as `AsSingleton().AsScoped()`, throws. Repeating the same lifetime is allowed.
- An open generic stage applies its lifetime to every closed type it produces.

The two lifetime methods sit on the same delegate as `WhereHandlerImplements`, and chain in either order:

```csharp
.AddStage(typeof(TransactionStage<,>), s => s
    .WhereHandlerImplements<IWriteHandler>()
    .AsScoped())
```

A singleton instance outlives every scope, so the stage must be thread safe, and anything it injects is pinned for the life of the process. A singleton stage holding a scoped `DbContext` is a captive dependency.

Every closed stage type is a registered service, so the container can catch that at startup. Whether it does depends on the options:

- `ValidateOnBuild` and `ValidateScopes` both on: the captive dependency is reported. ASP.NET Core turns this pair on in Development.
- `ValidateOnBuild` alone: it builds the constructor graph without comparing lifetimes, and says nothing.

A dispatcher resolved from the root provider also resolves stages there. Without scope validation, a scoped stage then lives until provider disposal. See [Service lifetimes](lifetimes.md).

Overlapping `next` calls can enter a scoped or singleton stage concurrently within one dispatch. Transient stages get a separate instance per call.

### Replacing a stage registration

`AddStage` appends a descriptor for each closed stage type. The container uses the last descriptor registered for that type:

- Stage declarations override earlier registrations.
- Application registrations added afterwards override the declared lifetime.

Register replacements after the last `AddRequestFlow` call. Later calls can discover requests that produce additional closed stage registrations.

To register a stage on terms the declaration cannot express, a factory or an instance you built yourself, replace it after that call:

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .AddStage(typeof(LoggingStage<,>)));

services.Replace(ServiceDescriptor.Singleton(new LoggingStage<Ping, string>(sink)));
```

`Replace` removes the old descriptor before adding yours. `AddSingleton` leaves it behind, so `ValidateOnBuild` still checks the old constructor and can fail if a dependency such as `sink` is missing.

`Replace` removes one descriptor. If several descriptors exist for the stage type, call `services.RemoveAll<LoggingStage<Ping, string>>()` before adding the replacement.

## Validation

`provider.InspectRequestFlow<TRequest>()` shows the stages selected for a request and why other registered stages were excluded. Inspection requires successful validation. See [Pipeline inspection](pipeline-inspection.md).

Stage problems surface with every other registration problem, in the one `RequestFlowValidationException` thrown at first dispatcher resolution or at `ValidateRequestFlow`. The checks:

- The stage type implements the typed or void stage contract for the family selected by
  `AddStage`, `AddValueStage`, or `AddStreamStage`, and is a concrete class.
- An open generic stage uses its own type parameters as its contract's request, so it can close over the requests it dispatches with.
- A partially closed generic is rejected; register the open definition or a fully closed type.
- No stage type is registered twice, and no two declarations reach one request as the same stage class. A request with more than one handler has one chain per handler, so only declarations resolving to one closed type count as a collision there.
- With `DisallowUnusedStages`, every stage reaches at least one request.
