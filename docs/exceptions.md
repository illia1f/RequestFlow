# Exceptions

Every exception RequestFlow throws, when it surfaces, and how to fix it.

## At a glance

| Exception                        | Thrown from              | When                                                          |
| -------------------------------- | ------------------------ | -------------------------------------------------------------- |
| `RequestFlowValidationException` | Startup validation       | Any registration problem; one throw lists all of them          |
| `HandlerNotFoundException`       | `SendAsync`              | The dispatched request type has no registered handler          |
| `ResponseTypeMismatchException`  | `SendAsync`              | The call site's response type differs from the registered one  |
| `InvalidOperationException`      | `SendAsync`              | A handler or stage returned a null task, or a stage overlapped two `next` calls |
| `InvalidOperationException`      | `WhereHandlerImplements` | A second handler filter added to one stage                     |
| `ArgumentNullException`          | All public entry points  | A required argument is null                                    |
| `ArgumentException`              | `RegisterGenericHandler` | `closingTypes` contains a null element                         |

The three RequestFlow types are sealed, live in the `RequestFlow` namespace in the `RequestFlow.Abstractions` package, and derive from `InvalidOperationException`. All of them signal programmer errors: fix the registration or the call site instead of catching them.

## RequestFlowValidationException

Thrown when RequestFlow validates everything registered: the first time a dispatcher is resolved, or earlier if `ValidateRequestFlow` runs at startup (see [lifetimes.md](lifetimes.md) for validation timing). Problems accumulate across every `AddRequestFlow` call and surface as one exception. The message and the `Problems` property list all of them, so one failed start reports everything at once. Failed validation does not stick: every later dispatcher resolution validates again and throws the same list.

| Problem message starts with                          | Cause                                                                         | Fix                                                                    |
| ---------------------------------------------------- | ----------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `Request '...' has no handler.`                      | A scanned request type no handler covers                                       | Write the handler, scan its assembly, or call `AllowUnhandledRequests`  |
| `Request '...' has more than one handler...`         | Two handlers cover the same request, via scan or generic closings              | Remove one; exactly one handler per request                             |
| `'...' is not an open generic type definition...`    | `RegisterGenericHandler(typeof(AuditHandler<Foo>), ...)` or a non-generic type | Pass the open definition: `typeof(AuditHandler<>)`                      |
| `'...' is abstract...`                               | An abstract class passed to `RegisterGenericHandler`                           | Register a concrete handler class                                       |
| `'...' has N generic parameters...`                  | An open generic with more than one type parameter                              | Only single-parameter generic handlers are supported                    |
| `'...' does not implement IRequestHandler.`          | The type is not a handler                                                      | Implement `IRequestHandler<TRequest, TResponse>` or `IRequestHandler<TRequest>` |
| `Generic handler '...' declares no closing types...` | `RegisterGenericHandler(typeof(AuditHandler<>))` with no closings              | Declare at least one closing type                                       |
| `Closing type '...' ... is not a closed type.`       | An open generic passed as a closing type                                       | Close it first: `typeof(Audit<Order>)`, not `typeof(Audit<>)`           |
| `Generic handler '...' cannot be closed over '...'...` | The closing type violates the handler's generic constraints                  | Pick a closing type that satisfies the `where` clauses                  |

Stages registered with `AddStage` bring their own checks (see [stages.md](stages.md)); their problems land in the same exception:

| Problem message starts with                              | Cause                                                                        | Fix                                                                     |
| -------------------------------------------------------- | ----------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| `'...' is an interface; only concrete stage classes...`  | An interface passed to `AddStage`                                              | Register the implementing class                                          |
| `'...' is abstract; only concrete stage classes...`      | An abstract class passed to `AddStage`                                         | Register a concrete stage class                                          |
| `'...' is partially closed...`                           | A stage type with some type parameters bound and some open                     | Pass the open definition or a fully closed type                           |
| `'...' does not implement IRequestStage...`              | The registered type is not a stage                                             | Implement `IRequestStage<TRequest, TResponse>` or `IRequestStage<TRequest>` |
| `'...' declares generic parameters <...> that its IRequestStage implementation does not use...` | An open generic stage whose contract does not name its own parameters as the request | Implement the contract with the stage's own parameters, request first    |
| `Stage '...' from assembly '...' is registered more than once...` | The same stage type in two `AddStage` calls                             | Remove the duplicate; a handler filter does not make it distinct          |
| `Stages '...' and '...' both resolve to '...'` / `Stages '...' and '...' are the same stage class...` | An open definition registered next to its own closed form, or two closings of one class reaching the same request | Remove one of the two `AddStage` calls |
| `Stage '...' from assembly '...' applies to no registered request...` | `DisallowUnusedStages` is on and the stage reached nothing            | Widen its constraints, scan the assembly holding its requests, or drop the opt-in |

Example: a contracts assembly scanned without its handlers fails at startup, not per request.

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Contracts.CreateOrder>());

// First dispatcher resolution (or ValidateRequestFlow) throws:
// RequestFlowValidationException: RequestFlow registration is invalid:
// Request 'Contracts.CreateOrder' has no handler.
// Request 'Contracts.CancelOrder' has no handler.
```

If the assembly intentionally contains only requests, opt out with `AllowUnhandledRequests`:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Contracts.CreateOrder>()
    .AllowUnhandledRequests());
```

## HandlerNotFoundException

Thrown by `SendAsync` when the request's runtime type has no registered handler. The `RequestType` property holds the request type that had no handler.

With default validation a scanned request without a handler already fails startup validation, so only three paths lead here:

1. The request's assembly was never scanned. RequestFlow never saw the type, so startup validation could not flag it. Include the assembly in a `RegisterHandlersFromAssembly*` call.
2. A call opted out with `AllowUnhandledRequests`, so a missing handler surfaces at dispatch instead of at startup.
3. The request is a derived type. Dispatch matches the request's exact runtime type and never walks up the inheritance chain to a base type's handler.

```csharp
public class CreateOrder : IRequest<OrderId> { }
public class ExpressCreateOrder : CreateOrder { }   // no handler of its own

public sealed class CreateOrderHandler : IRequestHandler<CreateOrder, OrderId>
{
    public Task<OrderId> HandleAsync(CreateOrder request, CancellationToken cancellationToken)
        => Task.FromResult(new OrderId());
}

await dispatcher.SendAsync(new CreateOrder());          // works
await dispatcher.SendAsync(new ExpressCreateOrder());   // HandlerNotFoundException
```

The second call compiles because `ExpressCreateOrder` is an `IRequest<OrderId>`, but no handler is registered for its runtime type and `CreateOrderHandler` is never considered. Register a handler per concrete request type, and prefer marking request types `sealed` so the compiler prevents the situation outright.

## ResponseTypeMismatchException

Thrown by `SendAsync` when the request type has a registered handler, but its response type differs from the call site's `TResponse` argument. `RequestType`, `ExpectedResponseType`, and `ActualResponseType` identify the three types involved.

The compiler normally infers `TResponse` from the request's `IRequest<TResponse>` interface, so plain call sites never hit this. Two things make it reachable.

The first is a covariant upcast. `IRequest<out TResponse>` is covariant, so a request handled as `IRequest<SyncReport>` also converts to `IRequest<IReport>` when `SyncReport : IReport`. The call compiles, but dispatch matches the registered response type exactly, with no variance:

```csharp
public sealed class SyncReport : IReport { }
public sealed class SyncInventory : IRequest<SyncReport> { }

public sealed class SyncInventoryHandler : IRequestHandler<SyncInventory, SyncReport>
{
    public Task<SyncReport> HandleAsync(SyncInventory request, CancellationToken cancellationToken)
        => Task.FromResult(new SyncReport());
}

await dispatcher.SendAsync(new SyncInventory());   // infers SyncReport: works

IRequest<IReport> request = new SyncInventory();   // compiles: IRequest<out TResponse> is covariant
await dispatcher.SendAsync(request);               // asks for IReport, registered SyncReport:
                                                   // ResponseTypeMismatchException
```

Dispatch with the exact response type the handler declares, and cast the response afterwards if you want the base type.

The second is a request with two response contracts: a request type implementing more than one `IRequest<TResponse>` interface. The void `IRequest` counts, since it is `IRequest<NoResult>`:

```csharp
// Smell: one request, two response contracts.
public sealed class SyncInventory : IRequest, IRequest<SyncReport> { }
```

With a handler registered as `IRequestHandler<SyncInventory, SyncReport>`, the natural call `SendAsync(new SyncInventory())` cannot infer `TResponse` from two candidate interfaces. It silently binds the void `SendAsync(IRequest)` overload, asks for `NoResult`, and throws. The fix belongs in the model, not the call site: give each request type exactly one `IRequest<TResponse>` interface, and split it in two if both shapes are needed.

## InvalidOperationException

Plain `InvalidOperationException` signals a broken handler or stage contract. Three cases surface at dispatch, one at registration:

| Message starts with                                        | Thrown from                | Fix                                                                      |
| ----------------------------------------------------------- | --------------------------- | -------------------------------------------------------------------------- |
| `The handler for '...' returned a null task from HandleAsync.` | `SendAsync`             | Return a task from every path; use `Task.CompletedTask` or `Task.FromResult` for synchronous results |
| `Stage '...' returned a null task from HandleAsync...`      | `SendAsync`                 | Return the task from `next`, or a completed task when short-circuiting      |
| `Stage '...' called next while the task from its earlier call was still running.` | `SendAsync` | Await each `next` call before calling it again; each call runs the rest of the chain |
| `This stage already filters on '...'`                       | `AddStage` configure delegate | One `WhereHandlerImplements` per stage; give the target handlers one shared contract |

The null-task checks exist so the failure names the handler or stage at fault instead of surfacing as a `NullReferenceException` at the await. The overlap check stops a stage from running the rest of the chain twice at the same time; a sequential second call, the retry shape, is allowed (see [stages.md](stages.md)).

## Argument validation

Argument checks at the public surface throw immediately at the call site:

| Member                                | Throws                  | When                                    |
| ------------------------------------- | ----------------------- | --------------------------------------- |
| `IRequestDispatcher.SendAsync` (both) | `ArgumentNullException` | `request` is null                       |
| `AddRequestFlow`                      | `ArgumentNullException` | `services` or `configure` is null       |
| `RegisterHandlersFromAssembly`        | `ArgumentNullException` | `assembly` is null                      |
| `RegisterGenericHandler`              | `ArgumentNullException` | `handlerType` or `closingTypes` is null |
| `RegisterGenericHandler`              | `ArgumentException`     | `closingTypes` contains a null element  |
| `AddStage`                            | `ArgumentNullException` | `stageType` is null                     |
| `ValidateRequestFlow`                 | `ArgumentNullException` | `provider` is null                      |

## What RequestFlow never wraps

Handler and stage exceptions propagate as thrown. The dispatcher and the stage chain add no try/catch and no wrapper exception, so `await dispatcher.SendAsync(...)` observes exactly what the failing `HandleAsync` threw. A stage that wants to translate exceptions does so itself, in a try/catch around `next`.

Cancellation follows the same rule. The token passes to `HandleAsync` untouched, and an `OperationCanceledException` surfaces from the handler like any other exception.

Container failures keep the container's own exception types. The dispatcher resolves the handler from the service provider on every dispatch, so a handler with a missing constructor dependency, or a scoped handler resolved from the root provider, throws the container's `InvalidOperationException` at dispatch time. See [lifetimes.md](lifetimes.md) for the lifetime rules that prevent these.
