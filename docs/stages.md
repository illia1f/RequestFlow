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

A stage has four ways to use `next`:

- Await `next.InvokeAsync()` once and return its result: the normal pass-through.
- Return without invoking it to short-circuit. The handler, and every stage inside this one, never runs. Nothing inside is built either: a level resolves from the container only when it runs, and that includes the handler, so a cache stage that answers from memory never pays for the repository behind it.
- Await it, then invoke it again to run the rest of the chain a second time, the shape of a retry stage. Each call resolves the levels below it again, so the lifetime a stage was registered with decides what the second call gets. A transient stage is built fresh for the retry, which gives it a clean slate; a scoped one is the same instance for the whole scope.
- Invoke it again without awaiting the first call to run the rest of the chain twice at once, the shape of a hedging or shadow-comparison stage. Each call enters the levels below on its own and keeps the token it was handed, so the library holds no state the two walks can collide over. What they do share is whatever the container hands to both of them: a transient stage below is resolved again per call, so each walk gets an instance of its own, while a scoped or singleton one is a single instance running inside two walks at once and has to be thread safe. The handler and any scoped service either walk reaches are on the same rule.

A stage that fans out owns every call it has started. A stage that awaits each call to completion before starting the next has nothing to do here. A stage holding two live calls has two ways to lose one, and both end the same way: the abandoned walk keeps going and its own failure surfaces later as an `UnobservedTaskException`.

- The second `next.InvokeAsync` throws instead of handing back a task. Resolving the level below and checking what that level returned both happen before there is a task, so the call can fail outright while the first walk is already running.
- The first walk faults. `await first` throws, and the code never reaches `await second`.

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

`Task.WhenAll` reads the outcome of every task handed to it, so a fault on one walk leaves the other observed. `return await first + await second` does not: the awaits run in order, and the first one to throw skips the rest.

`ObserveAsync` is yours to write: await the walk and swallow whatever it threw, since the failure being reported is the one from the second call.

A hedging stage cannot use `WhenAll`, which waits for the slowest walk when the point is to return on the fastest. The walk whose result it discards needs the same treatment as the one above: await it, cancelled through the token it was given, and swallow what comes out.

## Cancellation

`next.InvokeAsync()` continues under the token the stage was handed, so by default the token given to `SendAsync` reaches every stage and the handler. Pass a token to put the rest of the chain on a different one, which is what a timeout needs:

```csharp
public sealed class TimeoutStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    public async Task<TResponse> HandleAsync(
        TRequest request, IContinuation<TResponse> next, CancellationToken cancellationToken)
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

The substituted token holds for every level below the stage, the handler included, and for inner stages that call `next` without naming a token of their own. Levels above the stage keep the token they had.

`CancellationToken.None` is not a substitution: passing it, `default` and an empty variable included, reads as omitting the token, so the rest of the chain continues under the one the stage received. To put the levels below on no cancellation at all, pass the token of a source nobody cancels.

Awaiting the cancelled call is the part to get right. A timeout that starts the chain and walks away from it leaves the handler running with its connection open, and an outer retry then sets a second walk going beside the first rather than replacing it. Cancel the call, wait for it to end, and then throw. Written that way, retry around timeout composes.

A handler that never looks at its token cannot be stopped by any of this. Cancellation is cooperative here as it is everywhere else in .NET.

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

Each stage declares its own lifetime, because a chain is rarely homogeneous. A logging stage holds nothing and can be a singleton; a unit-of-work stage in the same chain has to be scoped:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .AddStage(typeof(LoggingStage<,>), s => s.AsSingleton())
    .AddStage(typeof(UnitOfWorkStage<,>), s => s.AsScoped()));
```

Say nothing and the stage is transient, which means a fresh instance every time the chain enters its level. It is free to hold state for that one pass, but a repeated `next` call from the stage above builds a new instance, so nothing carries from one pass to the next. A stage takes one lifetime, so `AsSingleton().AsScoped()` throws; naming the same one twice is fine. An open generic stage passes its lifetime to every closed type it produces.

The two lifetime methods sit on the same delegate as `WhereHandlerImplements`, and chain in either order:

```csharp
.AddStage(typeof(TransactionStage<,>), s => s
    .WhereHandlerImplements<IWriteHandler>()
    .AsScoped())
```

Singleton is worth a moment's thought. The instance outlives every scope, so the stage must be thread safe, and anything it injects is pinned for the life of the process. A singleton stage holding a scoped `DbContext` is a captive dependency.

Every closed stage type is a registered service, so the container can catch that at startup. Whether it does depends on the options:

- `ValidateOnBuild` and `ValidateScopes` both on: the captive dependency is reported. ASP.NET Core turns this pair on in Development.
- `ValidateOnBuild` alone: it builds the constructor graph without comparing lifetimes, and says nothing.

Scoped stages have the mirror-image problem, quieter still. A root-resolved dispatcher resolves the stage from the root provider, so with scope validation off one instance sits there for the life of the process. [lifetimes.md](lifetimes.md) covers both.

Thread safety is not only a question of separate dispatches. A stage above that overlaps its `next` calls runs the levels below it side by side, so inside one dispatch a scoped or singleton stage under it is entered twice at once. Transient is the lifetime that stays clear of it: every call resolves an instance of its own.

### Replacing a stage registration

`AddStage` appends a descriptor for each closed stage type whatever the collection already holds, and the container takes the last descriptor registered for a type. That gives three rules:

- The declared lifetime is the one that applies.
- The declaration wins over anything registered before it.
- Anything registered after it wins instead.

Register your own stage after the *last* `AddRequestFlow` call, not the first. Closing runs again on every call, so a stage declared in the first call gains descriptors for the request types the second call scans, and those land after anything registered between the two.

To register a stage on terms the declaration cannot express, a factory or an instance you built yourself, replace it after that call:

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .AddStage(typeof(LoggingStage<,>)));

services.Replace(ServiceDescriptor.Singleton(new LoggingStage<Ping, string>(sink)));
```

`Replace`, not `AddSingleton`. Both resolve to your instance, since the container takes the last descriptor. The difference shows up under `ServiceProviderOptions.ValidateOnBuild`, which walks every descriptor including the one `AddStage` left behind. That one names the stage's constructor, so if the container cannot supply `sink`, and not having to supply it is why you built the stage by hand, startup fails over a stage that never runs. `Replace` drops that descriptor and leaves nothing to fail on.

`Replace` drops exactly one descriptor. Register the same stage type yourself before `AddRequestFlow` as well and one survives, so `ValidateOnBuild` still walks it. To clear every descriptor for the type, call `services.RemoveAll<LoggingStage<Ping, string>>()` and then `AddSingleton`.

## Validation

Stage problems surface with every other registration problem, in the one `RequestFlowValidationException` thrown at first dispatcher resolution or at `ValidateRequestFlow`. The checks:

- The stage type implements `IRequestStage<TRequest, TResponse>` or `IRequestStage<TRequest>` and is a concrete class.
- An open generic stage uses its own type parameters as its contract's request, so it can close over the requests it dispatches with.
- A partially closed generic is rejected; register the open definition or a fully closed type.
- No stage type is registered twice, and no two declarations reach one request as the same stage class.
- With `DisallowUnusedStages`, every stage reaches at least one request.
