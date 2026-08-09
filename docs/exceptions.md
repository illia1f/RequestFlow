# Exceptions

Every exception RequestFlow throws, when it surfaces, and how to fix it.

## At a glance

| Exception                        | Thrown from              | When                                                          |
| -------------------------------- | ------------------------ | -------------------------------------------------------------- |
| `RequestFlowValidationException` | Startup validation       | Any registration problem, or a validation rule that threw; one throw lists all of them |
| `HandlerNotFoundException`       | `SendAsync`              | The dispatched request type has no registered handler          |
| `ResponseTypeMismatchException`  | `SendAsync`              | The call site's response type differs from the registered one  |
| `HandlerNullTaskException`       | `SendAsync`              | A handler returned a null task from `HandleAsync`              |
| `StageNullTaskException`         | `SendAsync`              | A stage returned a null task from `HandleAsync`                |
| `InvalidOperationException`      | `WhereHandlerImplements` | A second handler filter added to one stage                     |
| `InvalidOperationException`      | Startup validation       | A validation rule returned null, or a null problem             |
| `InvalidOperationException` (the container's) | Startup validation | A validation rule depends on a RequestFlow dispatcher and the provider validates scopes; without that check nothing throws and startup hangs |
| `ArgumentNullException`          | All public entry points  | A required argument is null                                    |
| `ArgumentException`              | `RegisterGenericHandler` | `closingTypes` contains a null element                         |

The RequestFlow types live in the `RequestFlow` namespace in the `RequestFlow.Abstractions` package and derive from `InvalidOperationException`. All are sealed except `NullTaskException`, the abstract base the two null-task types share. All of them signal programmer errors: fix the registration or the call site instead of catching them.

## RequestFlowValidationException

Thrown when RequestFlow validates everything registered: the first time a dispatcher is resolved, or earlier if `ValidateRequestFlow` runs at startup (see [lifetimes.md](lifetimes.md) for validation timing). Problems accumulate across every `AddRequestFlow` call and surface as one exception. The message and the `Problems` property list all of them, so one failed start reports everything at once. Failed validation does not stick: every later dispatcher resolution validates again and throws the same list.

`Problems` holds `RequestFlowValidationProblem` values: a stable `Code`, a `Message` saying what to fix, and the `Subject` type at fault where the problem has one. Each line of the exception message is one problem, printed as `CODE: message`. A rule of your own reports into the same list, and [validation-rules.md](validation-rules.md) covers writing one.

| Code     | Problem message starts with                          | Cause                                                                         | Fix                                                                    |
| -------- | ---------------------------------------------------- | ----------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `RF0101` | `Request '...' has more than one handler...`         | Two handlers cover the same request, via scan or generic closings              | Remove one; exactly one handler per request                             |
| `RF0102` | `Request '...' has no handler.`                      | A scanned request type no handler covers                                       | Write the handler, scan its assembly, or call `AllowUnhandledRequests`  |
| `RF0106` | `Request '...' implements more than one request contract...` | The type implements two `IRequest<TResponse>` contracts, directly or through interfaces; a void request carries `IRequest<NoResult>` | Keep one contract; split the type if both responses are needed |
| `RF0107` | `Validation rule '...' threw ...`                    | A rule of yours or a package's threw out of `Validate`. That rule's findings were dropped, every other rule still reported, and the message names the exception type and text | Fix the rule, or catch inside it and report a problem so the message can name what was being checked |
| `RF0001` | `'...' is not an open generic type definition...`    | `RegisterGenericHandler(typeof(AuditHandler<Foo>), ...)` or a non-generic type | Pass the open definition: `typeof(AuditHandler<>)`                      |
| `RF0002` | `'...' is abstract...`                               | An abstract class passed to `RegisterGenericHandler`                           | Register a concrete handler class                                       |
| `RF0003` | `'...' has N generic parameters...`                  | An open generic with more than one type parameter                              | Only single-parameter generic handlers are supported                    |
| `RF0004` | `'...' does not implement IRequestHandler.`          | The type is not a handler                                                      | Implement `IRequestHandler<TRequest, TResponse>` or `IRequestHandler<TRequest>` |
| `RF0005` | `Generic handler '...' declares no closing types...` | `RegisterGenericHandler(typeof(AuditHandler<>))` with no closings              | Declare at least one closing type                                       |
| `RF0006` | `Closing type '...' ... is not a closed type.`       | An open generic passed as a closing type                                       | Close it first: `typeof(Audit<Order>)`, not `typeof(Audit<>)`           |
| `RF0007` | `Generic handler '...' cannot be closed over '...'...` | The closing type violates the handler's generic constraints                  | Pick a closing type that satisfies the `where` clauses                  |

Stages registered with `AddStage` bring their own checks (see [stages.md](stages.md)); their problems land in the same exception:

| Code     | Problem message starts with                              | Cause                                                                        | Fix                                                                     |
| -------- | -------------------------------------------------------- | ----------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| `RF0008` | `'...' is an interface; only concrete stage classes...`  | An interface passed to `AddStage`                                              | Register the implementing class                                          |
| `RF0009` | `'...' is abstract; only concrete stage classes...`      | An abstract class passed to `AddStage`                                         | Register a concrete stage class                                          |
| `RF0010` | `'...' is partially closed...`                           | A stage type with some type parameters bound and some open                     | Pass the open definition or a fully closed type                           |
| `RF0011` | `'...' does not implement IRequestStage...`              | The registered type is not a stage                                             | Implement `IRequestStage<TRequest, TResponse>` or `IRequestStage<TRequest>` |
| `RF0012` | `'...' declares generic parameters <...> that its IRequestStage implementation does not use...` | An open generic stage whose contract does not name its own parameters as the request | Implement the contract with the stage's own parameters, request first    |
| `RF0103` | `Stage '...' from assembly '...' is registered more than once...` | The same stage type in two `AddStage` calls                             | Remove the duplicate; a handler filter does not make it distinct          |
| `RF0104` | `Stages '...' and '...' both resolve to '...'` / `Stages ... are the same stage class...` | An open definition registered next to its own closed form, or several closings of one class reaching the same request; one problem names every declaration in the group. A request with more than one handler has one chain per handler, so only declarations resolving to one closed type collide there | Keep one of the named `AddStage` calls and remove the rest |
| `RF0105` | `Stage '...' from assembly '...' applies to no registered request...` | `DisallowUnusedStages` is on and the stage reached nothing. A request with no handler gets no stage chain, so a stage aimed only at unhandled requests lands here too | Widen its constraints, scan the assembly holding its requests, add the missing handler, or drop the opt-in |

One more code comes from the CQRS package: `CQRS0001` for a request classified as both a command and a query. `AddCqrs` contributes that check as a rule.

Example: a contracts assembly scanned without its handlers fails at startup, not per request.

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Contracts.CreateOrder>());

// First dispatcher resolution (or ValidateRequestFlow) throws:
// RequestFlowValidationException: RequestFlow registration is invalid:
// RF0102: Request 'Contracts.CreateOrder' has no handler.
// RF0102: Request 'Contracts.CancelOrder' has no handler.
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

## HandlerNullTaskException

Thrown by `SendAsync` when a handler returns a null task from `HandleAsync`. The `RequestType` property holds the request whose handler returned it.

Return a task from every path: `Task.FromResult(value)` for a synchronous result, `Task.CompletedTask` for the void form. The usual source is a test double left without a configured return value.

## StageNullTaskException

Thrown by `SendAsync` when a stage returns a null task from `HandleAsync`. The `StageType` property holds the stage class at fault.

Return the task from `next.InvokeAsync()`, or a completed task when short-circuiting.

Both null-task types derive from `NullTaskException`, so one catch clause covers a handler and a stage:

```csharp
catch (NullTaskException e)
{
    // e is a HandlerNullTaskException or a StageNullTaskException
}
```

The base class is abstract with no public constructor, so those two are the only cases it ever holds. Both checks exist so the failure names the handler or stage at fault instead of surfacing as a `NullReferenceException` at the await.

## Plain InvalidOperationException

Two cases are left with no type of their own. Adding a second `WhereHandlerImplements` to one stage throws from the `AddStage` configure delegate, with a message starting `This stage already filters on '...'`. A stage takes one handler filter, so give the target handlers one shared contract instead.

The second comes from a broken validation rule: a rule that returns null instead of an empty sequence, or a sequence with a null problem in it, throws at the freeze with a message naming the rule. Those two are the only rule failures that come out this way. An exception the rule throws from its own code is reported as `RF0107` in the validation exception instead, and the rules after it still run (see [validation-rules.md](validation-rules.md)).

One case that looks like it belongs here throws nothing at all. A rule that takes a dispatcher needs the map the freeze is still building, so the container waits on a result only that freeze can produce and startup hangs. A provider that validates scopes, which is what ASP.NET Core does in Development, rejects the rule earlier with `Cannot consume scoped service 'RequestFlow.IRequestDispatcher' from singleton 'RequestFlow.IRequestFlowValidationRule'`, since the dispatcher is scoped and a rule is a singleton. Both point at the same fix: take `IRequestDispatcher`, `ICommandDispatcher`, and `IQueryDispatcher` out of the rule's constructor. A handler or a stage in there triggers neither, though [validation-rules.md](validation-rules.md) covers why it is still the wrong dependency.

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
| `new RequestFlowValidationException`  | `ArgumentNullException` | `problems` is null                      |
| `RequestFlowModelBuilder`, `RequestModelBuilder` | `ArgumentNullException` | A required `Type` argument is null |
| `RequestFlowModelBuilder`, `RequestModelBuilder` | `ArgumentException` | `contractType` is not an open generic interface built on the handler or stage contract ([validation-rules.md](validation-rules.md#the-model)) |

## What RequestFlow never wraps

Handler and stage exceptions propagate as thrown. The dispatcher and the stage chain add no try/catch and no wrapper exception, so `await dispatcher.SendAsync(...)` observes exactly what the failing `HandleAsync` threw. A stage that wants to translate exceptions does so itself, in a try/catch around `next`.

Cancellation follows the same rule. The token reaches `HandleAsync` as the caller gave it, unless a stage in between passes a different one to `next`, and an `OperationCanceledException` surfaces from the handler like any other exception. The library never inspects the token and never throws on it by itself.

Container failures keep the container's own exception types. The dispatcher resolves the handler from the service provider on every dispatch, so a handler with a missing constructor dependency throws the container's `InvalidOperationException` at dispatch time, and so does a scoped handler resolved from the root provider while scope validation is on. With scope validation off the second case throws nothing: the root provider builds the handler and reuses that instance for the life of the process. See [lifetimes.md](lifetimes.md) for the lifetime rules that prevent these.

On a request with stages, that failure lands inside the chain. A level resolves as it runs, so the container's exception comes out of the `next.InvokeAsync()` call that reached the broken level, and the stages wrapped around it can catch it like any other exception. A retry stage with a broad `catch` will retry a missing registration until it runs out of attempts. Catch the exceptions you mean to handle, and turn on `ServiceProviderOptions.ValidateOnBuild` so a registration mistake fails at startup instead.
