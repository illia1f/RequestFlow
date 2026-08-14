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
- Return without invoking it to short-circuit. The handler and every stage inside this one never run, and nothing inside is even built. A level resolves from the container only when it runs, the handler included, so a cache stage that answers from memory never pays for the repository behind it.
- Await it, then invoke it again to run the rest of the chain a second time. That is the shape of a retry stage. Each call resolves the levels below it again, so the lifetime a stage was registered with decides what the second call gets. A transient stage is built fresh for the retry and starts clean. A scoped one is the same instance for the whole scope.
- Invoke it again without awaiting the first call to run the rest of the chain twice at once. That is the shape of a hedging or shadow-comparison stage. Each call enters the levels below on its own and keeps the token it was handed, so RequestFlow holds no state the two walks can collide over. What they do share is whatever the container hands to both. A transient stage below is resolved again per call, so each walk gets an instance of its own. A scoped or singleton one is a single instance running inside two walks at once, so it has to be thread safe. The handler and any scoped service either walk reaches follow the same rule.

`next` is a value, not an object, so a fan-out stage can copy it freely: into a local, into a closure per walk, into an array of walks to start. Every copy enters the same level. That level is built when the dispatch map freezes and holds nothing belonging to a single walk, which is what makes the copies interchangeable. The value does carry the request, the provider, and the token of the dispatch it was handed to, so do not hold on to it past that call.

A stage that fans out owns every call it started. None of this applies to a stage that awaits each call to completion before starting the next. Hold two live calls and there are two ways to lose one. Both end the same way: the abandoned walk keeps going, and its own failure surfaces later as an `UnobservedTaskException`.

- The second `next.InvokeAsync` throws instead of handing back a task. It resolves the level below and checks what that level returned before there is any task to hand back, so it can fail outright while the first walk is already running.
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

A hedging stage cannot use `WhenAll`: it waits for the slowest walk, when the point is to return on the fastest. Give the walk whose result you discard the same treatment as above. Cancel it through the token you handed it, await it, and swallow what comes out.

## Cancellation

`next.InvokeAsync()` continues under the token the stage was handed, so by default the token given to `SendAsync` reaches every stage and the handler. Pass a token to put the rest of the chain on a different one, which is what a timeout needs:

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

The substituted token holds for every level below the stage, the handler included, and for inner stages that call `next` without naming a token of their own. Levels above the stage keep the token they had.

`CancellationToken.None` is not a substitution. Passing it reads as omitting the token, and so do `default` and an empty variable, so the rest of the chain continues under the token the stage received. To put the levels below on no cancellation at all, pass the token of a source nobody cancels.

Awaiting the cancelled call is the part to get right. A timeout that starts the chain and walks away leaves the handler running with its connection open. An outer retry then sets a second walk going beside the first rather than replacing it. Cancel the call, wait for it to end, then throw. Written that way, retry around timeout composes.

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

`AuditStage` wraps every request that implements `IAudited` and no others. There is no list of types to maintain next to the registration. The constraints are checked once, at startup.

A closed stage targets the request contract it names. `TRequest` is contravariant, so a stage closed over a base request type also wraps the requests that derive from it.

A stage can also declare one type parameter and fix the response, the shape codebases with a shared result type use:

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

Stream requests are a chain of their own. `IRequestStage` constrains `TRequest` to `IRequest<TResponse>`, so no stage registered with `AddStage` reaches a stream handler. Those stages implement `IStreamRequestStage<TRequest, TItem>` and register with `AddStreamStage` (see [streaming.md](streaming.md)).

### Filtering on a handler contract

`WhereHandlerImplements` narrows a stage to requests whose handler implements a contract:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
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

A stage is a unit, and running one needs no container. `Continuation<TResponse>.Over` builds the `next` a stage expects from a delegate standing in for the rest of the chain, and `Continuation.Over` does the same for the void form:

```csharp
[Fact]
public async Task Given_A_Failing_Chain_Then_The_Stage_Retries_Once()
{
    int calls = 0;
    Continuation<Receipt> next = Continuation<Receipt>.Over(_ =>
        ++calls == 1 ? Task.FromException<Receipt>(new TimeoutException()) : Task.FromResult(new Receipt()));

    await new RetryStage().HandleAsync(new PlaceOrder(), next, CancellationToken.None);

    Assert.Equal(2, calls);
}
```

The delegate is handed whatever token the stage passed to `InvokeAsync`. So a stage that replaces the token for the levels below it is testable through the token the delegate sees. A call to `InvokeAsync()` with no token falls back to the token the stage itself received, and the optional second argument to `Over` is what it falls back to in a test:

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

The default value of either type has no chain under it. Hand a stage `default(Continuation<TResponse>)` and the first `InvokeAsync` throws an `InvalidOperationException` saying so. Build one with `Over` instead.

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

Say nothing and the stage is transient: a fresh instance every time the chain enters its level. It is free to hold state for that one pass. A repeated `next` call from the stage above builds a new instance, so nothing carries from one pass to the next. A stage takes one lifetime, so `AsSingleton().AsScoped()` throws. Naming the same one twice is fine. An open generic stage passes its lifetime to every closed type it produces.

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

Scoped stages have the mirror-image problem, and it is quieter still. A root-resolved dispatcher resolves the stage from the root provider. With scope validation off, one instance sits there for the life of the process. [lifetimes.md](lifetimes.md) covers both.

Thread safety is not only a question of separate dispatches. A stage above that overlaps its `next` calls runs the levels below it side by side. So inside one dispatch, a scoped or singleton stage under it is entered twice at once. Only transient stays clear of that, because every call resolves an instance of its own.

### Replacing a stage registration

`AddStage` appends a descriptor for each closed stage type whatever the collection already holds, and the container takes the last descriptor registered for a type. That gives three rules:

- The declared lifetime is the one that applies.
- The declaration wins over anything registered before it.
- Anything registered after it wins instead.

Register your own stage after the *last* `AddRequestFlow` call, not the first. Closing runs again on every call. So a stage declared in the first call gains descriptors for the request types the second call scans, and those land after anything you registered between the two.

To register a stage on terms the declaration cannot express, a factory or an instance you built yourself, replace it after that call:

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .AddStage(typeof(LoggingStage<,>)));

services.Replace(ServiceDescriptor.Singleton(new LoggingStage<Ping, string>(sink)));
```

`Replace`, not `AddSingleton`. Both resolve to your instance, since the container takes the last descriptor. The difference shows up under `ServiceProviderOptions.ValidateOnBuild`, which walks every descriptor, including the one `AddStage` left behind. That leftover names the stage's constructor. If the container cannot supply `sink`, startup fails over a stage that never runs, and not having to supply `sink` is usually the reason you built the stage by hand. `Replace` drops that descriptor and leaves nothing to fail on.

`Replace` drops exactly one descriptor. Register the same stage type yourself before `AddRequestFlow` as well and one survives, so `ValidateOnBuild` still walks it. To clear every descriptor for the type, call `services.RemoveAll<LoggingStage<Ping, string>>()` and then `AddSingleton`.

## Validation

Stage problems surface with every other registration problem, in the one `RequestFlowValidationException` thrown at first dispatcher resolution or at `ValidateRequestFlow`. The checks:

- The stage type implements `IRequestStage<TRequest, TResponse>` or `IRequestStage<TRequest>` and is a concrete class.
- An open generic stage uses its own type parameters as its contract's request, so it can close over the requests it dispatches with.
- A partially closed generic is rejected; register the open definition or a fully closed type.
- No stage type is registered twice, and no two declarations reach one request as the same stage class. A request with more than one handler has one chain per handler, so only declarations resolving to one closed type count as a collision there.
- With `DisallowUnusedStages`, every stage reaches at least one request.
