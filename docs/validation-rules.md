# Validation rules

A validation rule is a check of your own that runs inside RequestFlow's startup validation. An application adds one to enforce a convention across its requests. A package adds one to check its own contracts, which is how `AddCqrs` rejects a request that lands on both sides of the command and query split.

Rules run once, when the request and event maps freeze together: at the first dispatcher or publisher resolution, or at startup under `ValidateRequestFlow` (see [lifetimes.md](lifetimes.md) for the timing). Their problems land in the same `RequestFlowValidationException` as the built-in ones, so one failed start reports everything at once.

The built-in checks below are fixed. A rule adds checks to the pass and cannot remove or replace one.

[`samples/Orders.Api`](../samples/README.md) has two working rules, and a command-line flag that makes them fail alongside a built-in check and the CQRS one.

## Built-in codes

Each row says what the check stops you doing. [exceptions.md](exceptions.md) has the cause and the fix behind each one.

- Codes are stable and never renumbered. Numbering is allocation order, not run order.
- `RF0001` to `RF0020` are shape checks on a single declaration. The `RF01xx` codes look at the whole registration at the freeze.
- `RF0107` is the odd one out: the pass reports it about a rule that threw, not about a registration.
- The `RF` constants live on `ProblemCodes` in `RequestFlow.Abstractions`, and the CQRS one on `CqrsProblemCodes` in `RequestFlow.Cqrs.Abstractions`, so a caller matches `ProblemCodes.UnhandledRequest` rather than a literal without referencing the runtime packages.

| Code | You cannot |
| --- | --- |
| `RF0001` | pass a closed or non-generic type to `RegisterGenericHandler` |
| `RF0002` | pass an abstract class to `RegisterGenericHandler` |
| `RF0003` | declare a generic handler with more than one type parameter |
| `RF0004` | register a handler that implements none of `IRequestHandler`, `IValueRequestHandler`, or `IStreamRequestHandler` |
| `RF0005` | declare a generic handler and name no closing types |
| `RF0006` | close a generic handler with a type that is itself open |
| `RF0007` | close a generic handler with a type its constraints reject |
| `RF0008` | pass an interface to `AddStage` |
| `RF0009` | pass an abstract class to `AddStage` |
| `RF0010` | pass a partially closed type to `AddStage` |
| `RF0011` | register a stage that does not implement the stage contract selected by `AddStage`, `AddValueStage`, or `AddStreamStage` |
| `RF0012` | write an open generic stage whose contract does not use its own type parameters as the request |
| `RF0013` | register an interface as an event publish strategy |
| `RF0014` | register an abstract class as an event publish strategy |
| `RF0015` | pass an interface to `AddEventHandler` |
| `RF0016` | pass an abstract class to `AddEventHandler` |
| `RF0017` | pass a type without an `IEventHandler<TEvent>` contract to `AddEventHandler` |
| `RF0018` | pass an interface to `AddHandler` |
| `RF0019` | pass an abstract class to `AddHandler` |
| `RF0020` | pass a type without a Task, ValueTask, or stream handler contract to `AddHandler` |
| `RF0101` | cover one request with two handlers |
| `RF0102` | leave a request unhandled, unless you call `AllowUnhandledRequests` |
| `RF0103` | register one stage type twice |
| `RF0104` | reach one request from two stage declarations that close to the same stage class |
| `RF0105` | keep a stage that applies to no registered request, once you call `DisallowUnusedStages` |
| `RF0106` | implement `IRequest<TResponse>` more than once on one request |
| `RF0107` | throw out of a validation rule and still start |
| `RF0108` | implement `IStreamRequest<TItem>` more than once on one request |
| `RF0109` | implement `IRequest<TResponse>` and `IStreamRequest<TItem>` on one type |
| `RF0110` | give a stream handler an item type its request does not declare |
| `RF0111` | give a stream stage an item type its request does not declare; caught only when the stage wrapped no handler |
| `RF0112` | give a handler a response type its request does not declare |
| `RF0113` | give a stage a response type its request does not declare; caught only when the stage wrapped no handler |
| `RF0114` | leave a known event with no applicable handler, unless you call `AllowUnhandledEvents` |
| `RF0115` | keep an event-handler subscription that reaches no known event, once you call `DisallowUnusedEventHandlers` |
| `RF0116` | implement `IRequest<TResponse>` and `IEvent` on one type |
| `RF0117` | implement `IStreamRequest<TItem>` and `IEvent` on one type |
| `RF0118` | register one class as a stage and as an event handler under different lifetimes |
| `RF0119` | name two different strategies on one global or per-event target |
| `RF0120` | leave one event with two equally specific winning strategy declarations; declare the exact event type to resolve it |
| `RF0121` | declare one strategy type with two different lifetimes |
| `RF0122` | keep a per-event strategy declaration that reaches no known event, once you call `DisallowUnusedEventHandlers` |
| `RF0123` | register one class as a strategy and as a handler or stage under different lifetimes |
| `RF0124` | give a handler an interface or abstract request type that the target cannot expose as an exact runtime type |
| `RF0125` | implement `IValueRequest<TResponse>` more than once on one request |
| `RF0126` | implement `IRequest<TResponse>` and `IValueRequest<TOther>` on one type |
| `RF0127` | implement `IStreamRequest<TItem>` and `IValueRequest<TResponse>` on one type |
| `RF0128` | implement `IValueRequest<TResponse>` and `IEvent` on one type |
| `RF0129` | give a ValueTask handler a response type its request does not declare |
| `RF0130` | give a ValueTask stage a response type its request does not declare; caught only when the stage wrapped no handler |
| `CQRS0001` | classify one request as both a command and a query, or as both a command and a stream query; contributed by `AddCqrs` |

Problems come out in fixed rule order: registration shape problems first, then the request and stage built-ins, then the event rules, then `RF0124`. Stable code allocation is not run order.

- The stream contract rule reports `RF0108` and `RF0110`, so both precede the `RF0109` conflict from the rule after it.
- The ValueTask request rule reports `RF0125` and `RF0129` beside the Task and stream contract rules. The pairwise family conflicts then report `RF0109`, `RF0126`, and `RF0127` in that order.
- ValueTask stage mismatch `RF0130` runs after Task stage mismatch `RF0113`.
- Event-family conflicts report `RF0116`, `RF0117`, and `RF0128` in that order.
- `EventStrategyRule` runs after `RF0118` and reports its shape codes `RF0013` and `RF0014` before `RF0119` to `RF0123`.

Four rules can be missing from the pass. Registration options control these checks:

- `AllowUnhandledRequests` disables `RF0102`.
- `AllowUnhandledEvents` disables `RF0114`.
- `DisallowUnusedStages` enables `RF0105`.
- `DisallowUnusedEventHandlers` enables `RF0115` and `RF0122`.

Rules registered through DI run afterwards in registration order. Within a rule, request, stage, event, and subscription scan order determines its problems. An `RF0107` takes the place of whatever a DI-resolved rule that threw would have reported, so it lands in that rule's position.

## Writing a rule

Implement `IRequestFlowValidationRule`. `Validate` receives a `RequestFlowValidationContext` and returns every problem it found:

```csharp
using RequestFlow;

public sealed class RequestNameRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        List<RequestFlowValidationProblem> problems = [];
        foreach (RequestModel request in context.Model.Requests)
        {
            if (request.RequestType.Name.EndsWith("Request", StringComparison.Ordinal))
                continue;

            problems.Add(new RequestFlowValidationProblem(
                "ACME0001",
                $"Request '{request.RequestType.FullName}' does not end in 'Request'; rename it.",
                request.RequestType));
        }

        return problems;
    }
}
```

The context carries `Model`, the registration as frozen, plus four registration flags: `UnhandledRequestsAllowed`, `UnusedStagesDisallowed`, `UnhandledEventsAllowed`, and `UnusedEventHandlersDisallowed`. One context is built per pass and handed to every rule, so a rule of yours reads what a built-in one reads.

A problem carries three things. `Code` is a stable identifier for the kind of problem, which is what a caller matches on. `Message` says what is wrong and how to fix it. `Subject` is the type at fault, and it is optional, since not every problem has one. `ToString` renders `Code: Message`, and that is the line the exception message shows.

Give your own codes a prefix that names where they come from, the way `CQRS0001` names the CQRS package. `RF` is reserved for RequestFlow.

Return an empty sequence when nothing is wrong. Returning null, or a sequence with a null in it, throws an `InvalidOperationException` naming the rule.

An exception out of a rule becomes a problem of its own. The pass records it as `RF0107`, naming the rule and the exception, drops that rule's findings, and runs the rules after it, so a rule that throws while validating cannot hide what the others found. Startup still fails, because `RF0107` counts like any other problem. What goes missing is the stack trace, so catch inside the rule and report a problem yourself: your message can name the registration you were checking, and `RF0107` cannot.

Two failures stay outside that net. A rule whose constructor throws fails while the container builds the rule list, before any rule validates, and takes the pass down with it. A rule that resolves a dispatcher inside `Validate` never reaches `RF0107` either, because the resolution never returns: see [Registering a rule](#registering-a-rule) below.

## Registering a rule

`AddRequestFlow` returns a builder, and `AddValidationRule` takes the rule type from there:

```csharp
services.AddRequestFlow(o => o
        .RegisterHandlersFromCallingAssembly())
    .AddValidationRule<RequestNameRule>();
```

The rule resolves from the container, so constructor dependencies work. `AddValidationRule` registers it as a singleton and offers no other lifetime. `FrozenPlans` is a singleton, so its freeze resolves rules from the root provider. A scoped rule or scoped dependency throws there on a provider that validates scopes.

One singleton instance serves every validation pass, and a failed pass runs again on the next dispatcher or publisher resolution ([exceptions.md](exceptions.md#requestflowvalidationexception)). Keep the rule stateless, or register it transient yourself instead of calling `AddValidationRule`:

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

services.TryAddEnumerable(
    ServiceDescriptor.Transient<IRequestFlowValidationRule, RequestNameRule>());
```

That trades one problem for another when the rule is or owns an `IDisposable`. The rule resolves from the root provider, which tracks every transient disposable it creates and releases none of them until the provider is disposed, so a start that keeps failing leaves one instance behind per failed freeze attempt. A rule that clears its state at the top of `Validate` stays a singleton and avoids both.

Do not take a dispatch surface in a rule, whether `IRequestDispatcher`,
`IValueRequestDispatcher`, `IStreamDispatcher`, `IEventPublisher`, `ICommandDispatcher`,
`IQueryDispatcher`, `IValueCommandDispatcher`, `IValueQueryDispatcher`, or
`IStreamQueryDispatcher`. Resolving one needs the frozen plans that the current pass is still
building, so the container waits on a result only that pass can produce. Nothing throws and the
stack never overflows. The process never finishes starting.

Most providers reject the rule before it gets that far. The dispatcher is scoped by default and a rule is a singleton, so a provider that validates scopes fails first:

```
Cannot consume scoped service 'RequestFlow.IRequestDispatcher' from singleton
'RequestFlow.IRequestFlowValidationRule'.
```

That is what ASP.NET Core shows in Development, and `ValidateOnBuild` reports it at `BuildServiceProvider`. The message names `IRequestDispatcher`, `IValueRequestDispatcher`, or `IStreamDispatcher` even when the rule took a CQRS dispatcher, because the typed dispatchers are transient wrappers over those three. The hang is what you get on a provider that does not validate scopes, or after `WithTransientDispatcher` makes the dispatcher resolvable from the root. If startup produces no output and no error while the process stays alive, this is why. The fix either way is to drop the dependency.

Handlers and stages are a different case. They resolve without touching the map, so a rule taking one starts fine. Event handlers are reached by their concrete class rather than their handler interface, but carry the same lifetime risk. The rule is a singleton resolved from the root provider, so it pins a transient handler for as long as the provider lives, and once the application calls `WithScopedHandlers` the same rule stops resolving on any provider that validates scopes. Read the model instead; it already names every handler, event subscription, and stage type.

A package registers its rule on the service collection directly, the way `AddCqrs` does:

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IRequestFlowValidationRule, CommandQuerySplitRule>());
```

`TryAddEnumerable` keys on the implementation type, so the rule registers once however many times that code runs. `AddValidationRule` does the same, so calling it twice for one rule type costs nothing.

## Testing a rule

A rule reads a context and returns problems, so a test needs no container and no dispatcher. Build the part of the model the rule reads and leave the rest empty:

```csharp
[Fact]
public void Given_A_Request_Without_The_Suffix_When_Validating_Then_The_Rule_Reports_It()
{
    RequestFlowValidationContext context = new RequestFlowModelBuilder()
        .AddRequest(typeof(PlaceOrder), r => r.AddHandler(typeof(PlaceOrderHandler), typeof(OrderId)))
        .BuildContext();

    RequestFlowValidationProblem[] problems = [.. new RequestNameRule().Validate(context)];

    problems.ShouldHaveSingleItem().Code.ShouldBe("ACME0001");
}
```

`BuildContext` wraps a freshly built model. Its four flags are optional and default to what registration does with no opt-in, so a request-only test calls it bare and a test that reads an event flag names only that one:

```csharp
RequestFlowValidationContext context = new RequestFlowModelBuilder()
    .AddEvent(typeof(OrderPlaced))
    .AddEventHandler(typeof(SendReceipt), typeof(OrderPlaced))
    .BuildContext(unusedEventHandlersDisallowed: true);
```

`Build()` returns the model without a context, for a test that wants it on its own.

`RequestFlowValidationContext`, `RequestFlowModel`, `RequestModel`, `HandlerModel`, `StageDeclarationModel`, `ClosedStageModel`, `EventModel`, `EventHandlerModel`, and `EventSubscriptionModel` all take internal constructors, so `RequestFlowModelBuilder` is how a test outside the library builds them.

- Repeated `AddRequest` calls for one request type configure a single entry.
- `AddEvent` records a known event and takes a concrete closed type implementing `IEvent`, which is what the freeze plans for. It rejects an interface, a base class, or an open generic with an `ArgumentException`; those go to `AddEventHandler` as a subscription contract.
- Every `AddEventHandler` call records one handler-contract subscription, and the builder derives both event entry lists and reached events from it.
- `PublishEventsWith(declaredEventType, strategyType, lifetime)` records a global declaration when the first argument is null, and an assignable event target otherwise.

Two problems are equal when their code, message, and subject match, so a test can assert the whole value rather than pick it apart.

## The model

`context.Model` is the registration as recorded, minus what the shape checks threw out. A declaration reported under `RF0001` to `RF0012` or `RF0015` to `RF0020` never reaches a rule, so a rule auditing every registered stage type sees only the ones that could run. Past that nothing is cleaned up: a request no handler covers is in the list with an empty `Handlers`, and a stage registered twice appears twice. Every rule reads the same snapshot, and every list on it is read-only, so one rule cannot change what the next one reads.

| Type | Carries |
| --- | --- |
| `RequestFlowModel` | `Requests`, every known request type; `StageDeclarations`, every stage declaration; `Events`, every known event; `EventSubscriptions`, every event handler-contract pair; `EventStrategies`, every publish strategy declaration |
| `RequestModel` | `RequestType`, the `Handlers` covering it, and `Stages`, its closed stage chain in execution order |
| `HandlerModel` | `HandlerType`; `ResponseType`, the response for a typed Task or ValueTask handler, the item type for a stream handler, and null for a void one, which `IsVoid` reports as a `bool`; `Lifetime`, what this handler is registered with; and `ContractType`, the handler contract it implements |
| `StageDeclarationModel` | `StageType`, the type `AddStage`, `AddValueStage`, or `AddStreamStage` was given; `ReachedRequests`, the requests that stage type reached; `Lifetime`, what the stage is registered with; and `ContractType`, the stage contract it implements |
| `ClosedStageModel` | `DeclaredType`, the type the registering call was given; `ClosedType`, the stage that runs for this request; and `ContractType` |
| `EventModel` | `EventType`, `Handlers` in frozen entry order, and the winning `PublishStrategy`, which is null when no declaration wins: an `RF0120` ambiguity, or an `RF0119` conflict on one target or on the global declaration |
| `EventHandlerModel` | `HandlerType`, `DeclaredEventType`, and `Lifetime` for one entry |
| `EventSubscriptionModel` | `HandlerType`, `DeclaredEventType`, `Lifetime`, and `ReachedEvents` for one scanned handler contract |
| `EventStrategyModel` | `DeclaredEventType`, null for global; `StrategyType`; `Lifetime`; and the known events for which it wins in `ReachedEvents` |

An event handler class with two contracts appears twice in `EventSubscriptions`. It can also appear twice in one event's `Handlers` when both contracts apply. `ReachedEvents` is empty when a subscription reaches no known concrete event, which is the fact `RF0115` reads. The hand-built model uses the same closure and ordering code as the registry. Same-tier event order is deterministic in that model, but it is not a public compatibility contract for publication.

`ReachedRequests` is derived from the chains rather than recorded at registration: it holds every request whose `Stages` contains a closing of that `StageType`, in request order. A declaration that closed for nothing has an empty list, and so does one aimed only at requests without a handler, because those get no chain to close into. A model a test builds by hand derives it the same way, from the `AddStage` calls under `AddRequest`, matched on the declared type. Pass `AddStageDeclaration`'s type as `AddStage`'s `declaredType` there; naming the closed type instead leaves `ReachedRequests` empty for a stage the freeze would report as reaching the request.

`StageDeclarationModel.Lifetime` is a `RequestFlowLifetime`: `Transient`, `Scoped`, or `Singleton`. It comes from the declaration's own `AddStage` call, which is where all three are reachable, `AsSingleton` and `AsScoped` included ([lifetimes.md](lifetimes.md)). A singleton stage is shared across concurrent dispatches, so a rule of your own can hold a convention about which stages may take one. The enum mirrors the container's `ServiceLifetime` rather than naming it, because `RequestFlow.Abstractions` does not reference the DI abstractions, so an assembly referencing only the contracts can still read a lifetime.

`HandlerModel.Lifetime` and the two event model lifetimes use the same enum, transient or scoped. Each `AddRequestFlow` call decides for the handlers it found ([lifetimes.md](lifetimes.md#lifetime-is-per-registration-call)), so two handlers in one registration can differ and each carries its own:

```csharp
foreach (RequestModel request in context.Model.Requests)
{
    foreach (HandlerModel handler in request.Handlers)
    {
        if (handler.Lifetime != RequestFlowLifetime.Scoped)
        {
            problems.Add(new RequestFlowValidationProblem(
                "ACME0002", $"Handler '{handler.HandlerType.FullName}' must be scoped.", handler.HandlerType));
        }
    }
}
```

Naming the handler is the point of reading it there: the rule can say which one to change. What it reports is what `AddRequestFlow` registered. A handler the application registers by hand afterwards wins at resolution without changing this value. A model a test builds by hand names the lifetime on `AddHandler`, and records transient without it.

`ContractType` is a `Type`, not an enum, so a package can record its own handler or stage contract there without RequestFlow knowing that contract ahead of time. A rule matching on it compares against the exact contract it declared itself, instead of switching over a fixed set of cases the core would have to enumerate.

- The freeze records the most derived contract the type implements: a handler written against `ICommandHandler<TCommand, TResponse>` comes through as `typeof(ICommandHandler<,>)`, a plain one as `typeof(IRequestHandler<,>)`.
- When two contracts apply and neither derives from the other, the core contract is recorded rather than one of the two.
- A handler entry usually names a Task, ValueTask, or stream handler contract, or an interface deriving from one. A stage entry does the same for its family's stage contract.
- A contract from another family is recorded as it was given. `RequestFlow.Abstractions` cannot name every contract a package might add, so it does not test membership of a family.
- The builder takes an open generic interface and throws `ArgumentException` on anything else. A class and a closed interface are rejected, because neither matches a comparison a rule would write.

Requests come in scan order, followed by request types only a handler brought in. Closing a stage needs a handler, so a request nothing handles has an empty chain whatever stages would otherwise reach it.

`Stages` is a single chain only when the request has one handler, which is what every registration that starts produces. A request two handlers cover is already an `RF0101` failure, and its `Stages` holds what each of them closes, merged into one list, so a single `AddStage` call can show up there twice. A rule that counts or orders stages should skip any request whose `Handlers.Count` is not one, the same way it skips the unhandled case.

`ClosedType` names the type the container resolves, so a two-parameter stage over a void request reads `LoggingStage<Purge, NoResult>` there while the handler's `ResponseType` is null. The two describe different things: what the handler returns, and what the container constructs. Take the response from `HandlerModel.ResponseType` and leave `ClosedType`'s type arguments alone.

## The registration flags

The four flags sit on the context beside the model. `UnhandledRequestsAllowed` is true once `AllowUnhandledRequests` has been called, and `UnusedStagesDisallowed` once `DisallowUnusedStages` has. `UnhandledEventsAllowed` records `AllowUnhandledEvents`, while `UnusedEventHandlersDisallowed` records `DisallowUnusedEventHandlers`.

The event flags are independent. `AllowUnhandledEvents` suppresses `RF0114` for a known event with no handler. It does not suppress `RF0115` or `RF0122` when `DisallowUnusedEventHandlers` asks the freeze to report a subscription or per-event strategy declaration that reaches no known event.

`RF0122` tests applicability, not whether the declaration won. A family declaration shadowed by an exact declaration is still applicable and reports nothing. A declaration involved in an `RF0120` tie also reports no `RF0122`. Global declarations state policy and never report `RF0122`.

A rule with its own opinion about a request should still skip the unhandled case and leave it to `RF0102`:

```csharp
foreach (RequestModel request in context.Model.Requests)
{
    if (request.Handlers.Count == 0)
        continue;

    // your check here
}
```

Skipping it is right whether or not the application opted in. With the flag off, `RF0102` already reports the request; with it on, the missing handler is deliberate. Read `UnhandledRequestsAllowed` when the two cases deserve different wording, not to decide whether to skip.
