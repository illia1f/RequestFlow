# Stages

A stage wraps the handler of every request it applies to: code before and after the handler, a replaced response, or no handler call at all. Coming from MediatR, `IRequestStage<TRequest, TResponse>` is the `IPipelineBehavior<TRequest, TResponse>` counterpart.

## Writing a stage

Implement `IRequestStage<TRequest, TResponse>`. Invoking `next` runs the rest of the chain, ending at the handler:

```csharp
using RequestFlow;

public sealed class LoggingStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request, IContinuation<TResponse> next, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        TResponse response = await next.InvokeAsync();
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return response;
    }
}
```

A stage has three ways to use `next`:

- Await `next.InvokeAsync()` once and return its result: the normal pass-through.
- Return without invoking it to short-circuit. The handler, and every stage inside this one, never runs. Nothing inside is built either: each level resolves from the container the first time it runs, and that includes the handler, so a cache stage that answers from memory never pays for the repository behind it.
- Invoke it again after its task completes to run the rest of the chain again, the shape of a retry stage. A repeated call walks the same stage instances resolved for the dispatch, and reaches the same handler instance, so state a stage kept from the first pass is still there. Invoking `next` while an earlier call is still running throws `OverlappingNextCallException`.

## Registering

`AddStage` chains on the same configure delegate as the scanning options. Registration order is execution order, outermost first:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .AddStage(typeof(LoggingStage<,>))
    .AddStage(typeof(ValidationStage<,>)));
```

Every request both stages apply to runs logging, then validation, then its handler. A closed stage type can also register through the generic form: `AddStage<AuditStage>()`.

One stage type belongs to a chain once. Registering the same type twice fails startup validation, whatever the two calls filtered on. So does a pair of declarations that reach one request as the same stage class, an open definition next to its own closed form for example. `AddStage` calls from separate `AddRequestFlow` calls combine into one chain, in call order, and validate together (see [registration.md](registration.md) on additive calls).

## Which requests a stage reaches

An open generic stage applies to every request its own generic constraints admit. Constrain `TRequest` and the chain follows:

```csharp
public interface IAudited
{ }

public sealed class AuditStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IAudited
{
    public async Task<TResponse> HandleAsync(
        TRequest request, IContinuation<TResponse> next, CancellationToken cancellationToken)
    {
        TResponse response = await next.InvokeAsync();
        // write the audit record
        return response;
    }
}
```

`AuditStage` wraps every request that implements `IAudited` and no others. There is no list of types to maintain next to the registration; the constraints are checked once, at startup.

A closed stage targets the request contract it names. `TRequest` is contravariant, so a stage closed over a base request type also wraps the requests that derive from it.

A stage can also declare one type parameter and fix the response, the shape codebases with a shared result type use:

```csharp
public sealed class ErrorTranslationStage<TRequest> : IRequestStage<TRequest, Result>
    where TRequest : IRequest<Result>
{
    public async Task<Result> HandleAsync(
        TRequest request, IContinuation<Result> next, CancellationToken cancellationToken)
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
    .RegisterHandlersFromAssemblyContaining<Program>()
    .AddStage(typeof(TransactionStage<,>), s => s.WhereHandlerImplements<IWriteHandler>()));
```

The filter looks at the handler class, not the request, so a module can mark its write handlers with one empty interface and wrap them all. A stage takes one filter; a second `WhereHandlerImplements` call throws.

## Void requests

A stage for void requests implements `IRequestStage<TRequest>`, takes the void form `IContinuation`, and returns plain `Task`:

```csharp
public sealed class CacheClearGuard : IRequestStage<ClearCache>
{
    public Task HandleAsync(ClearCache request, IContinuation next, CancellationToken cancellationToken)
        => next.InvokeAsync();
}
```

Both forms mix in one chain, in registration order. An open two-parameter stage whose constraints admit a void request wraps it too, with `NoResult` as the response.

## Unused stages

A stage that reaches no registered request is a silent no-op by default. `DisallowUnusedStages` makes it a startup validation problem instead:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .AddStage(typeof(AuditStage<,>))
    .DisallowUnusedStages());
```

The setting is sticky, like `AllowUnhandledRequests`: once any call opts in, every registered stage is checked.

## Lifetime

RequestFlow registers each closed stage type transient and resolves the instances on every dispatch, so a stage can hold per-dispatch state. To pick a different lifetime, register the closed stage type yourself before calling `AddRequestFlow`; a stage type already in the collection is left alone. [lifetimes.md](lifetimes.md) covers the wider lifetime picture.

## Validation

Stage problems surface with every other registration problem, in the one `RequestFlowValidationException` thrown at first dispatcher resolution or at `ValidateRequestFlow`. The checks:

- The stage type implements `IRequestStage<TRequest, TResponse>` or `IRequestStage<TRequest>` and is a concrete class.
- An open generic stage uses its own type parameters as its contract's request, so it can close over the requests it dispatches with.
- A partially closed generic is rejected; register the open definition or a fully closed type.
- No stage type is registered twice, and no two declarations reach one request as the same stage class.
- With `DisallowUnusedStages`, every stage reaches at least one request.
