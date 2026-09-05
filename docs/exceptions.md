# Exceptions

Every exception RequestFlow throws, when it surfaces, and how to fix it.

## At a glance

| Exception                        | Thrown from              | When                                                          |
| -------------------------------- | ------------------------ | -------------------------------------------------------------- |
| [`RequestFlowValidationException`](#requestflowvalidationexception) | Startup validation | Any registration problem, or a validation rule that threw; one throw lists all of them |
| [`HandlerNotFoundException`](#handlernotfoundexception) | `SendAsync`, `Stream` | The dispatched request type has no registered handler |
| [`ResponseTypeMismatchException`](#responsetypemismatchexception) | `SendAsync`, `Stream` | The call site's response or item type differs from the registered one |
| [`HandlerNullTaskException`](#handlernulltaskexception) | `SendAsync` | A handler returned a null task from `HandleAsync` |
| [`StageNullTaskException`](#stagenulltaskexception) | `SendAsync` | A stage returned a null task from `HandleAsync` |
| [`HandlerNullStreamException`](#handlernullstreamexception) | Enumeration | A stream handler returned a null sequence from `Handle` |
| [`StageNullStreamException`](#stagenullstreamexception) | Enumeration | A stream stage returned a null sequence from `Handle` |
| [`EventNotRegisteredException`](#eventnotregisteredexception) | `PublishAsync` call | The event's exact runtime type has no frozen plan |
| [`EventPublishException`](#event-publication-failures) | Returned publish task | A publish strategy reported one or more event-handler failures |
| [`EventPublishCanceledException`](#eventpublishcanceledexception) | Returned publish task | A publish strategy acknowledged cancellation |
| [`EventHandlerNullTaskException`](#eventhandlernulltaskexception) | `EventHandlerFailure.Exception` | An event handler returned a null task from `HandleAsync` |
| [`EventStrategyNullTaskException`](#eventstrategynulltaskexception) | Returned publish task | An event publish strategy returned a null task from `PublishAsync` |
| [`InvalidOperationException`](#plain-invalidoperationexception) | `WhereHandlerImplements` | A second handler filter added to one stage |
| [`InvalidOperationException`](#plain-invalidoperationexception) | Startup validation | A validation rule returned null, or a null problem |
| [The container's `InvalidOperationException`](#plain-invalidoperationexception) | Startup validation | A validation rule depends on a RequestFlow dispatcher or publisher and the provider validates scopes; without that check nothing throws and startup hangs |
| [`ArgumentNullException`](#argument-validation) | All public entry points | A required argument is null |
| [`ArgumentException`](#argument-validation) | `RegisterGenericHandler` | `closingTypes` contains a null element |

The RequestFlow types all live in the `RequestFlow` namespace and ship in `RequestFlow.Abstractions`. Most derive from `InvalidOperationException`. `EventPublishException` derives from `AggregateException`, and `EventPublishCanceledException` derives from `OperationCanceledException`. All are sealed except the two abstract bases, `NullTaskException` and `NullStreamException`.

The null-task exceptions apply only to Task handlers, Task stages, event handlers, and event
strategies. ValueTask is a struct and cannot be null. `default(ValueTask<TResponse>)` succeeds with
`default(TResponse)`, so the ValueTask request family has no null-task exception counterpart. See
[ValueTask requests](value-tasks.md#default-is-a-valid-result).

## RequestFlowValidationException

Thrown when RequestFlow validates the whole registration. The first resolution of a dispatcher or event publisher triggers validation, unless `ValidateRequestFlow` runs earlier at startup (see [lifetimes.md](lifetimes.md) for validation timing). Problems accumulate across every `AddRequestFlow` call and surface as one exception. The message and the `Problems` property list all of them, so one failed start reports everything at once. Failed validation does not stick: a later resolution of either a dispatcher or an event publisher retries validation and throws the same list.

`Problems` holds `RequestFlowValidationProblem` values: a stable `Code`, a `Message` saying what to fix, and the `Subject` type at fault where the problem has one. Each line of the exception message is one problem, printed as `CODE: message`. A rule of your own reports into the same list, and [validation-rules.md](validation-rules.md) covers writing one.

### Validation code index

Scan by code, or use the Area column when you only remember what failed. Each code links to its section in the grouped reference.

| Code | Area | Meaning |
| --- | --- | --- |
| [`RF0001`](#generic-handler-registration) | Generic handler registration | Handler type is not an open generic definition |
| [`RF0002`](#generic-handler-registration) | Generic handler registration | Handler type is abstract |
| [`RF0003`](#generic-handler-registration) | Generic handler registration | Handler type has the wrong generic arity |
| [`RF0004`](#generic-handler-registration) | Generic handler registration | Type does not implement a handler contract |
| [`RF0005`](#generic-handler-registration) | Generic handler registration | No closing types were supplied |
| [`RF0006`](#generic-handler-registration) | Generic handler registration | A closing type is not closed |
| [`RF0007`](#generic-handler-registration) | Generic handler registration | A closing type violates the handler's constraints |
| [`RF0008`](#stage-registration) | Stage registration | Stage type is an interface |
| [`RF0009`](#stage-registration) | Stage registration | Stage type is abstract |
| [`RF0010`](#stage-registration) | Stage registration | Stage type is partially closed |
| [`RF0011`](#stage-registration) | Stage registration | Type does not implement a stage contract |
| [`RF0012`](#stage-registration) | Stage registration | Stage type uses its generic parameters incorrectly |
| [`RF0013`](#events) | Events | Event publish strategy type is an interface |
| [`RF0014`](#events) | Events | Event publish strategy type is abstract |
| [`RF0015`](#events) | Events | Manually added event handler type is an interface |
| [`RF0016`](#events) | Events | Manually added event handler type is abstract |
| [`RF0017`](#events) | Events | Manually added type does not implement the event handler contract |
| [`RF0018`](#manual-handler-registration) | Manual handler registration | Manually added handler type is an interface |
| [`RF0019`](#manual-handler-registration) | Manual handler registration | Manually added handler type is abstract |
| [`RF0020`](#manual-handler-registration) | Manual handler registration | Manually added type does not implement a handler contract |
| [`RF0101`](#requests-and-handlers) | Requests and handlers | Request has more than one handler |
| [`RF0102`](#requests-and-handlers) | Requests and handlers | Request has no handler |
| [`RF0103`](#stages) | Stages | Stage type is registered more than once |
| [`RF0104`](#stages) | Stages | Stage declarations resolve to the same closed type |
| [`RF0105`](#stages) | Stages | Stage applies to no registered request |
| [`RF0106`](#requests-and-handlers) | Requests and handlers | Request implements more than one request contract |
| [`RF0107`](#validation-rules) | Validation rules | Validation rule threw from `Validate` |
| [`RF0108`](#requests-and-handlers) | Requests and handlers | Stream request implements more than one stream contract |
| [`RF0109`](#requests-and-handlers) | Requests and handlers | Type implements both request and stream request contracts |
| [`RF0110`](#requests-and-handlers) | Requests and handlers | Stream handler item type does not match the request |
| [`RF0111`](#stages) | Stages | Stream stage item type does not match the request |
| [`RF0112`](#requests-and-handlers) | Requests and handlers | Handler response type does not match the request |
| [`RF0113`](#stages) | Stages | Stage response type does not match the request |
| [`RF0114`](#events) | Events | Event has no applicable handler |
| [`RF0115`](#events) | Events | Event-handler subscription reaches no known event |
| [`RF0116`](#events) | Events | Type implements both request and event contracts |
| [`RF0117`](#events) | Events | Type implements both stream request and event contracts |
| [`RF0118`](#events) | Events | Stage class also handles events under a different lifetime |
| [`RF0119`](#events) | Events | One strategy target names different strategy types |
| [`RF0120`](#events) | Events | Event strategy declarations tie at the winning tier |
| [`RF0121`](#events) | Events | Strategy type is declared with different lifetimes |
| [`RF0122`](#events) | Events | Event strategy declaration reaches no known event |
| [`RF0123`](#events) | Events | Strategy class holds an event handler or stage role under a different lifetime |
| [`RF0124`](#requests-and-handlers) | Requests and handlers | Handler's interface or abstract request type cannot be an exact runtime type |
| [`RF0125`](#requests-and-handlers) | Requests and handlers | ValueTask request implements more than one response contract |
| [`RF0126`](#requests-and-handlers) | Requests and handlers | Type implements both Task and ValueTask request contracts |
| [`RF0127`](#requests-and-handlers) | Requests and handlers | Type implements both stream and ValueTask request contracts |
| [`RF0128`](#events) | Events | Type implements both ValueTask request and event contracts |
| [`RF0129`](#requests-and-handlers) | Requests and handlers | ValueTask handler response type does not match the request |
| [`RF0130`](#stages) | Stages | ValueTask stage response type does not match the request |
| [`CQRS0001`](#cqrs) | CQRS | Request is a command and a query, or a command and a stream query |

### Codes by area

#### Generic handler registration

| Code | Problem message starts with | Cause | Fix |
| --- | --- | --- | --- |
| `RF0001` | `'...' is not an open generic type definition...` | `RegisterGenericHandler(typeof(AuditHandler<Foo>), ...)` or a non-generic type | Pass the open definition: `typeof(AuditHandler<>)` |
| `RF0002` | `'...' is abstract...` | An abstract class passed to `RegisterGenericHandler` | Register a concrete handler class |
| `RF0003` | `'...' has N generic parameters...` | An open generic with more than one type parameter | Only single-parameter generic handlers are supported |
| `RF0004` | `'...' does not implement IRequestHandler, IValueRequestHandler, or IStreamRequestHandler.` | The type is not a handler | Implement a Task, ValueTask, or stream handler contract |
| `RF0004` | `'...' has an interface that could not be loaded...` | An interface on the definition passed to `RegisterGenericHandler` lives in an assembly the application did not deploy, so the contract cannot be read | Deploy the assembly that defines the interface |
| `RF0005` | `Generic handler '...' declares no closing types...` | `RegisterGenericHandler(typeof(AuditHandler<>))` with no closings | Declare at least one closing type |
| `RF0006` | `Closing type '...' ... is not a closed type.` | An open generic passed as a closing type | Close it first: `typeof(Audit<Order>)`, not `typeof(Audit<>)` |
| `RF0007` | `Generic handler '...' cannot be closed over '...'...` | The closing type violates the handler's generic constraints | Pick a closing type that satisfies the `where` clauses |

#### Stage registration

Stages registered with `AddStage` bring their own checks (see [stages.md](stages.md)); their problems land in the same exception. ValueTask stages registered with `AddValueStage` and stream stages registered with `AddStreamStage` use the same codes. The stage tables use `IRequestStage` and `AddStage` for brevity, but each message names the contract and call for the selected family. The stream family has no void form, so its messages name one contract where the Task and ValueTask families name two (see [streaming.md](streaming.md)).

| Code | Problem message starts with | Cause | Fix |
| --- | --- | --- | --- |
| `RF0008` | `'...' is an interface; only concrete stage classes...` | An interface passed to `AddStage` | Register the implementing class |
| `RF0009` | `'...' is abstract; only concrete stage classes...` | An abstract class passed to `AddStage` | Register a concrete stage class |
| `RF0010` | `'...' is partially closed...` | A stage type with some type parameters bound and some open | Pass the open definition or a fully closed type |
| `RF0011` | `'...' does not implement IRequestStage...` | The registered type is not a stage | Implement `IRequestStage<TRequest, TResponse>` or `IRequestStage<TRequest>` |
| `RF0012` | `'...' declares generic parameters <...> that its IRequestStage implementation does not use...` | An open generic stage whose contract does not name its own parameters as the request | Implement the contract with the stage's own parameters, request first |

#### Requests and handlers

| Code | Problem message starts with | Cause | Fix |
| --- | --- | --- | --- |
| `RF0101` | `Request '...' has more than one handler...` | Two handlers cover the same request, via scan or generic closings | Remove one; exactly one handler per request |
| `RF0102` | `Request '...' has no handler.` | A scanned request type no handler covers | Write the handler, scan its assembly, or call `AllowUnhandledRequests` |
| `RF0106` | `Request '...' implements more than one request contract...` | The type implements two `IRequest<TResponse>` contracts, directly or through interfaces; a void request carries `IRequest<NoResult>` | Keep one contract; split the type if both responses are needed |
| `RF0108` | `Stream request '...' implements more than one stream request contract...` | The type implements two `IStreamRequest<TItem>` contracts, directly or through interfaces | Keep one contract; split the type if both sequences are needed |
| `RF0109` | `Request '...' implements both IRequest and IStreamRequest...` | One type carries a request contract and a stream contract. The map holds one plan per request type, so one would overwrite the other | Keep one contract; split the type if both are needed |
| `RF0110` | `Stream handler '...' produces '...' items for request '...'` | The handler's item type is wider than the one the request declares. `IStreamRequest<TItem>` is covariant, so the pair compiles, but `Stream` infers the declared item type and the plan holds the handler's, so dispatching the request throws | Give the handler the item type the request declares |
| `RF0112` | `Handler '...' produces '...' for request '...', which declares IRequest<...>...` | The handler's response type is wider than the one the request declares. `IRequest<TResponse>` is covariant, so the pair compiles, but `SendAsync` infers the declared response type and the plan holds the handler's, so dispatching the request throws | Give the handler the response type the request declares |
| `RF0124` | `Handler '...' handles '...', which is an interface...` | The target cannot produce an instance whose exact runtime type matches the handler's interface or abstract request type. The `net462` asset permits interfaces and abstract `MarshalByRefObject` types because `RealProxy` can expose either through `GetType()` | Use a concrete request type, or a `net462` transparent proxy over an interface or abstract `MarshalByRefObject` request |
| `RF0125` | `ValueTask request '...' implements more than one ValueTask request contract...` | The type implements two `IValueRequest<TResponse>` contracts; the void marker carries `IValueRequest<NoResult>` | Keep one contract; split the type if both responses are needed |
| `RF0126` | `Type '...' implements both IRequest and IValueRequest...` | One type belongs to both Task and ValueTask request families | Keep one family; split the type if both are needed |
| `RF0127` | `Type '...' implements both IStreamRequest and IValueRequest...` | One type belongs to both stream and ValueTask request families | Keep one family; split the type if both are needed |
| `RF0129` | `ValueTask handler '...' produces '...' for request '...'` | The handler's response type differs from the request's sole `IValueRequest<TResponse>` contract | Give the handler the response type the request declares |

#### Stages

| Code | Problem message starts with | Cause | Fix |
| --- | --- | --- | --- |
| `RF0103` | `Stage '...' from assembly '...' is registered more than once...` | The same stage type appears in any two `AddStage`, `AddValueStage`, or `AddStreamStage` calls. A type implementing several families' contracts can still be declared only once, so it serves one chain | Remove the duplicate; a handler filter does not make it distinct. To wrap several families, write one stage class per family |
| `RF0104` | `Stages '...' and '...' both resolve to '...'` / `Stages ... are the same stage class...` | An open definition registered next to its own closed form, or several closings of one class reaching the same request; one problem names every declaration in the group. A request with more than one handler has one chain per handler, so only declarations resolving to one closed type collide there | Keep one of the named `AddStage` calls and remove the rest |
| `RF0105` | `Stage '...' from assembly '...' applies to no registered request...` | `DisallowUnusedStages` is on and the stage reached nothing. A request with no handler gets no stage chain, so a stage aimed only at unhandled requests lands here too | Widen its constraints, scan the assembly holding its requests, add the missing handler, or drop the opt-in |
| `RF0111` | `Stream stage '...' takes '...' items for request '...'` | The stage's item type is wider than the one the request declares. `IStreamRequest<TItem>` is covariant, so the stage compiles, but closing matches the item type exactly, so the stage would wrap no handler and the chain would run without it | Give the stage the item type the request declares |
| `RF0113` | `Stage '...' takes '...' for request '...'` | The stage's response type is wider than the one the request declares. `IRequest<TResponse>` is covariant, so the stage compiles, but closing matches the response type exactly, so the stage would wrap no handler and the chain would run without it | Give the stage the response type the request declares |
| `RF0130` | `ValueTask stage '...' takes '...' for request '...'` | The stage's fixed response type differs from the request's sole `IValueRequest<TResponse>` contract, so the stage would never run | Give the stage the response type the request declares |

#### Validation rules

| Code | Problem message starts with | Cause | Fix |
| --- | --- | --- | --- |
| `RF0107` | `Validation rule '...' threw ...` | A rule of yours or a package's threw out of `Validate`. That rule's findings were dropped, every other rule still reported, and the message names the exception type and text | Fix the rule, or catch inside it and report a problem so the message can name what was being checked |

#### Events

| Code | Problem message starts with | Cause | Fix |
| --- | --- | --- | --- |
| `RF0013` | `Event publish strategy '...' is an interface...` | A strategy declaration names an interface, which the container cannot construct | Name a concrete strategy class |
| `RF0014` | `Event publish strategy '...' is abstract...` | A strategy declaration names an abstract class | Name a concrete strategy class |
| `RF0015` | `'...' is an interface; only concrete event handler classes...` | An interface passed to `AddEventHandler` | Register the implementing class |
| `RF0016` | `'...' is abstract; only concrete event handler classes...` | An abstract class passed to `AddEventHandler` | Register a concrete subclass |
| `RF0017` | `'...' does not implement IEventHandler.` | The type passed to `AddEventHandler` is not an event handler | Implement `IEventHandler<TEvent>` |
| `RF0017` | `'...' has an interface that could not be loaded...` | An interface on the type passed to `AddEventHandler` lives in an assembly the application did not deploy, so the contract cannot be read | Deploy the assembly that defines the interface |
| `RF0114` | `Event '...' has no handler.` | A known concrete event has no applicable exact, base, interface, or `IEvent` handler | Add or scan a handler, or call `AllowUnhandledEvents` when a known empty plan is intentional |
| `RF0115` | `Event subscription '...' declared for '...' reaches no known event...` | `DisallowUnusedEventHandlers` is on and one handler contract reaches no known concrete event | Scan the targeted event assembly, remove the dead contract, or drop the opt-in |
| `RF0116` | `Type '...' implements both IRequest and IEvent...` | One concrete type belongs to the request and event contract families | Keep one role; split the type when both messages are needed |
| `RF0117` | `Type '...' implements both IStreamRequest and IEvent...` | One concrete type belongs to the stream request and event contract families | Keep one role; split the type when both messages are needed |
| `RF0118` | `Class '...' is registered as a ... stage and as a ... event handler...` | One class holds both roles, which share the concrete service key, so the descriptor registered last decides the lifetime both roles resolve under | Split the two roles into two classes, or give the stage the handler lifetime |
| `RF0119` | `The global event strategy is declared as both...` or `Event target '...' is declared with both...` | Two additive declarations disagree for the same global or typed target | Keep one strategy declaration for that target |
| `RF0120` | `Event '...' has equally specific strategy declarations...` | Two unrelated assignable targets tie at the winning specificity tier | Declare the strategy on the exact event type |
| `RF0121` | `Event publish strategy '...' is declared with both...` | One strategy type has different lifetimes across declarations | Use one lifetime for the strategy type |
| `RF0122` | `Event strategy '...' targets '...', but that declaration applies to no known event...` | `DisallowUnusedEventHandlers` is on and a per-event target reaches no known event | Scan the event assembly, correct the target, remove the declaration, or drop the opt-in |
| `RF0123` | `Class '...' is registered as a ... event publish strategy and as a ... event handler or stage...` | One class is a publish strategy and also an event handler or reached stage under a different lifetime. Those roles share the concrete service key, so the descriptor registered last decides the lifetime both resolve under. A Task, ValueTask, or stream handler role is keyed on the handler interface and does not conflict | Use one lifetime or split the roles |
| `RF0128` | `Type '...' implements both IValueRequest and IEvent...` | One concrete type belongs to the ValueTask request and event families | Keep one role; split the type when both messages are needed |

`RF0115` and `RF0122` are opt-in and independent of `AllowUnhandledEvents`. The unhandled option suppresses `RF0114`; it never suppresses a dead subscription or strategy declaration requested through `DisallowUnusedEventHandlers`.

#### Manual handler registration

| Code | Problem message starts with | Cause | Fix |
| --- | --- | --- | --- |
| `RF0018` | `'...' is an interface; only concrete handler classes...` | An interface passed to `AddHandler` | Register the implementing class |
| `RF0019` | `'...' is abstract; only concrete handler classes...` | An abstract class passed to `AddHandler` | Register a concrete subclass |
| `RF0020` | `'...' does not implement IRequestHandler, IValueRequestHandler, or IStreamRequestHandler.` | The type passed to `AddHandler` is not a Task, ValueTask, or stream handler | Implement a handler contract, or use `AddEventHandler` for an event handler |
| `RF0020` | `'...' has an interface that could not be loaded...` | An interface on the type passed to `AddHandler` lives in an assembly the application did not deploy, so the contract cannot be read | Deploy the assembly that defines the interface |

#### CQRS

`AddCqrs` contributes one package-specific check as a validation rule.

| Code | Problem message starts with | Cause | Fix |
| --- | --- | --- | --- |
| `CQRS0001` | `Request '...' is classified as both a command and a query...` | The request type implements a Task or ValueTask command contract next to a Task or ValueTask query contract. The check reads the two classifications, not their response types | Keep one side of the split; split the type if it must represent both operations |
| `CQRS0001` | `Request '...' is classified as both a command and a stream query...` | The request type implements a Task or ValueTask command contract next to `IStreamQuery<TItem>`. A cross-family pair also reports its core RF conflict | Keep one side of the split; split the type if it must represent both operations |

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

Thrown by `IRequestDispatcher.SendAsync`, `IValueRequestDispatcher.SendAsync`, the typed CQRS
dispatchers, and `IStreamDispatcher.Stream` or `IStreamQueryDispatcher.Stream` when the request's
runtime type has no registered handler. The `RequestType` property holds the request type that had
no handler. Both `Stream` methods throw it from the call rather than from the first enumeration,
because the lookup is not part of the sequence they hand back.

With default validation a scanned request without a handler already fails at startup, so only three paths lead here:

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

The second call compiles because `ExpressCreateOrder` is an `IRequest<OrderId>`, but no handler is registered for its runtime type and `CreateOrderHandler` is never considered. Register a handler per concrete request type, and mark request types `sealed` so the compiler rules this out.

## EventNotRegisteredException

Thrown directly from `PublishAsync` when the event's exact runtime type has no entry in the frozen `EventMap`. `EventType` holds that runtime type. The call throws before it returns a task and before any handler runs.

This is an unknown event, not a known event with an empty plan. `AllowUnhandledEvents` permits the latter and has no effect on a map miss. Include the event's assembly in a `RegisterHandlersFromAssembly*` call. An unscanned derived type or runtime proxy remains unknown even when its base event is registered, because closure is computed at freeze rather than at publication.

## Event publication failures

The built-in sequential and parallel strategies run every applicable entry unless a cancellation check stops the walk. Fail-fast stops on its first entry failure. A custom strategy chooses which entries to start and which failures to report. The single-failure case still uses `EventPublishException` when the strategy calls `EventDelivery.ThrowIfAny`.

### EventPublishException

`EventPublishException` derives from `AggregateException`. `EventType` names the runtime event, and `Failures` holds one `EventHandlerFailure` per reported subscription invocation. The built-ins use frozen entry order; a custom strategy controls collection order. Each `EventHandlerFailure` has:

- `HandlerType`, the concrete handler class;
- `DeclaredEventType`, the event contract used for this invocation;
- `Exception`, the original exception, or the handler task's `AggregateException` when that task held several exceptions.

`InnerExceptions` contains the same exceptions in the same order. The declared event type matters when one class implements two applicable contracts: RequestFlow invokes the class twice, and either or both invocations can fail independently.

Resolution failures, synchronous throws before a task is returned, canceled handler tasks, and null handler tasks all become entries in `Failures`. They do not stop later handlers. A null task's `Exception` is an `EventHandlerNullTaskException`.

`SkippedHandlerCount` reports entries the strategy never started. It is zero for run-all outcomes and positive for fail-fast outcomes with later entries. An entry that started and remains unfinished is not skipped. The public constructor reports no skipped count and no handler total; a test that needs an exception carrying both builds a delivery with `EventDelivery.Over` and calls `ThrowIfAny(failures, skippedHandlerCount)`.

## EventPublishCanceledException

Produced through the returned task when a publish strategy acknowledges the publisher token. It derives from `OperationCanceledException`. `EventType` names the runtime event, the inherited `CancellationToken` is the token passed to `PublishAsync`, `Failures` keeps any handler failures collected before the stop, and `SkippedHandlerCount` is how many handlers the stop kept from starting.

The built-ins check before any handler, including for an empty plan. Sequential and fail-fast check before each remaining handler. They do not check after the final sequential handler starts or after parallel fan-out begins. A custom strategy owns its acknowledgment points. A handler that throws `OperationCanceledException` or returns a canceled task is an ordinary entry failure.

The task returned by a publisher-acknowledged cancellation has `Status == Canceled` and `Task.Exception == null`. Awaiting it throws the exact `EventPublishCanceledException`, not a replacement `TaskCanceledException`. Earlier handlers may already have completed, so cancellation does not make publication atomic. [events.md](events.md#cancellation) contains the complete outcome table.

## ResponseTypeMismatchException

Thrown by Task or ValueTask `SendAsync` when the request type has a registered handler, but its
response type differs from the call site's `TResponse` argument. `RequestType`,
`ExpectedResponseType`, and `ActualResponseType` identify the three types involved. The two
`Stream` methods throw the same exception, from the call, when the call site's `TItem` differs from
the registered item type; the covariant upcast below is the way to reach it there too.

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

`IValueRequest<out TResponse>` follows the same covariance and exact-response rule. A widened
`IValueRequest<TResponse>` or ValueTask CQRS contract compiles, then throws this exception when sent.

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

All four null-task types derive from `NullTaskException`, so one catch clause covers a request handler, event handler, stage, or event publish strategy:

```csharp
catch (NullTaskException exception)
{
    Console.Error.WriteLine(exception.Message);
}
```

The base class is abstract with no public constructor. The checks make the failure name the handler or stage at fault instead of surfacing as a `NullReferenceException` at the await.

## EventHandlerNullTaskException

An event handler that returns a null task contributes an `EventHandlerNullTaskException` to `EventPublishException.Failures`; it is not thrown directly from the public call. `EventType` names the runtime event and `HandlerType` names the concrete class. Later event handlers still run.

## EventStrategyNullTaskException

Thrown through the returned publish task when a custom `IEventPublishStrategy` returns null from `PublishAsync`. `StrategyType` names the strategy. It is not converted to an `EventHandlerFailure`, because strategy execution is outside any one handler entry.

## HandlerNullStreamException

Thrown when a stream handler returns a null sequence from `Handle`. The `RequestType` property holds the request whose handler returned it.

It surfaces from enumeration, never from the `Stream` call: the check runs when the chain enters the handler's level. With no stages that is the first `MoveNextAsync`. A stage enters the levels below it on each `next.Invoke()`, at whatever point it makes that call, so a stream that has already yielded items can still throw this from a later `MoveNextAsync`.

Return a sequence from every path. An empty sequence is how a handler yields nothing; `null` is not. The usual source is a test double left without a configured return value.

## StageNullStreamException

Thrown when a stream stage returns a null sequence from `Handle`. The `StageType` property holds the stage class at fault. It surfaces when the chain enters that stage's level, on the same terms as the handler case.

Return the sequence from `next.Invoke()`, or an empty one when short-circuiting.

Both null-stream types derive from `NullStreamException`, which mirrors `NullTaskException` on the task path:

```csharp
catch (NullStreamException e)
{
    // e is a HandlerNullStreamException or a StageNullStreamException
}
```

The two bases are separate types with no common base of their own beyond `InvalidOperationException`, so a catch clause written for the task path does not pick up a stream failure by accident.

## Plain InvalidOperationException

Two cases are left with no type of their own. Adding a second `WhereHandlerImplements` to one stage throws from the `AddStage` configure delegate, with a message starting `This stage already filters on '...'`. A stage takes one handler filter, so give the target handlers one shared contract instead.

The second comes from a broken validation rule: a rule that returns null instead of an empty sequence, or a sequence with a null problem in it, throws at the freeze with a message naming the rule. Those two are the only rule failures that come out this way. An exception the rule throws from its own code is reported as `RF0107` in the validation exception instead, and the rules after it still run (see [validation-rules.md](validation-rules.md)).

One case that looks like it belongs here throws nothing at all. A rule that takes a dispatcher or publisher needs the plans the freeze is still building, so the container waits on a result only that freeze can produce and startup hangs. A provider that validates scopes, which is what ASP.NET Core does in Development, can reject the rule earlier because the dispatch surface is scoped and a rule is a singleton. Both point at the same fix: take `IRequestDispatcher`, `IValueRequestDispatcher`, `IStreamDispatcher`, `IEventPublisher`, `ICommandDispatcher`, `IQueryDispatcher`, `IValueCommandDispatcher`, `IValueQueryDispatcher`, and `IStreamQueryDispatcher` out of the rule's constructor. A handler or a stage in there triggers neither, though [validation-rules.md](validation-rules.md) covers why it is still the wrong dependency.

## Argument validation

Argument checks at the public surface throw immediately at the call site:

| Member                                | Throws                  | When                                    |
| ------------------------------------- | ----------------------- | --------------------------------------- |
| `IRequestDispatcher.SendAsync` (both) | `ArgumentNullException` | `request` is null                       |
| `IValueRequestDispatcher.SendAsync` (both) | `ArgumentNullException` | `request` is null                  |
| `IStreamDispatcher.Stream`            | `ArgumentNullException` | `request` is null                       |
| `ICommandDispatcher.SendAsync` (both) | `ArgumentNullException` | `command` is null                       |
| `IQueryDispatcher.SendAsync`          | `ArgumentNullException` | `query` is null                         |
| `IValueCommandDispatcher.SendAsync` (both) | `ArgumentNullException` | `command` is null                 |
| `IValueQueryDispatcher.SendAsync`     | `ArgumentNullException` | `query` is null                         |
| `IStreamQueryDispatcher.Stream`       | `ArgumentNullException` | `query` is null                         |
| `IEventPublisher.PublishAsync`        | `ArgumentNullException` | `event` is null                         |
| Task, ValueTask, and stream continuation `Over` methods | `ArgumentNullException` | `rest` is null       |
| `AddRequestFlow`                      | `ArgumentNullException` | `services` or `configure` is null       |
| `RegisterHandlersFromAssembly`        | `ArgumentNullException` | `assembly` is null                      |
| `RegisterGenericHandler`              | `ArgumentNullException` | `handlerType` or `closingTypes` is null |
| `RegisterGenericHandler`              | `ArgumentException`     | `closingTypes` contains a null element  |
| `AddStage`, `AddValueStage`, `AddStreamStage` | `ArgumentNullException` | `stageType` is null              |
| `ValidateRequestFlow`                 | `ArgumentNullException` | `provider` is null                      |
| `new RequestFlowValidationException`  | `ArgumentNullException` | `problems` is null                      |
| `RequestFlowModelBuilder`, `RequestModelBuilder` | `ArgumentNullException` | A required `Type` argument is null |
| `RequestFlowModelBuilder`, `RequestModelBuilder` | `ArgumentException` | `contractType` is not an open generic interface ([validation-rules.md](validation-rules.md#the-model)) |
| Event failure type constructors       | `ArgumentNullException` | A required type, exception, or failure list is null |
| Event aggregate constructors          | `ArgumentException` | A failure list contains a null entry |

## What RequestFlow never wraps

Task and ValueTask handler and stage exceptions propagate unchanged. Their dispatchers and stage
chains add no wrapper exception, so `await dispatcher.SendAsync(...)` observes exactly what the
failing `HandleAsync` threw. A stage that wants to translate exceptions does so itself, in a
try/catch around `next`.

The stream path is the same rule, observed later. Nothing of the handler's runs until the first `MoveNextAsync`, so what it throws comes out of the `await foreach` rather than out of the `Stream` call.

Cancellation follows the same rule. The token reaches `HandleAsync` as the caller gave it, unless a stage in between passes a different one to `next`, and an `OperationCanceledException` surfaces from the handler like any other exception. The library never inspects the token and never throws on it by itself. `Stream` is the one place RequestFlow builds a token of its own, and only to join the dispatch token with the one a caller passed to `WithCancellation`; it still never checks it.

Events are the fan-out exception to that rule. Several handler failures cannot all propagate directly, so publication keeps each original exception in an `EventHandlerFailure` and throws one ordered `EventPublishException` after every started handler finishes. Event cancellation is also publisher-aware: the checks and `EventPublishCanceledException` are documented above. Events do not use stages.

Container failures keep the container's own exception types. The dispatcher resolves the handler from the service provider on every dispatch, so a handler with a missing constructor dependency throws the container's `InvalidOperationException` at dispatch time, and so does a scoped handler resolved from the root provider while scope validation is on. On a stream nothing resolves until the chain runs, so both failures surface from the first enumeration rather than the `Stream` call. With scope validation off the second case throws nothing: the root provider builds the handler and reuses that instance for the life of the process. See [lifetimes.md](lifetimes.md) for the lifetime rules that prevent these.

On a request with stages, that failure lands inside the chain. A level resolves as it runs, so the container's exception comes out of the `next.InvokeAsync()` call that reached the broken level, and the stages wrapped around it can catch it like any other exception. A retry stage with a broad `catch` will retry a missing registration until it runs out of attempts. Catch the exceptions you mean to handle, and turn on `ServiceProviderOptions.ValidateOnBuild` so a registration mistake fails at startup instead.
