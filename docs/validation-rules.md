# Validation rules

A validation rule is a check of your own that runs inside RequestFlow's startup validation. An application adds one to enforce a convention across its requests. A package adds one to check its own contracts, which is how `AddCqrs` rejects a request classified as both a command and a query.

Rules run once, when the dispatch map freezes: at the first dispatcher resolution, or at startup under `ValidateRequestFlow` (see [lifetimes.md](lifetimes.md) for the timing). Their problems land in the same `RequestFlowValidationException` as the built-in ones, so one failed start reports everything at once.

The built-in checks are fixed. A rule adds checks to the pass and cannot remove or replace one.

[`samples/Orders.Api`](../samples/README.md) has two working rules, and a command-line flag that makes them fail alongside a built-in check and the CQRS one.

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

The context carries `Model`, the registration as frozen, plus `UnhandledRequestsAllowed` and `UnusedStagesDisallowed`, the two opt-ins registration made. One context is built per pass and handed to every rule, so a rule of yours reads what a built-in one reads.

A problem carries three things. `Code` is a stable identifier for the kind of problem, which is what a caller matches on. `Message` says what is wrong and how to fix it. `Subject` is the type at fault, and it is optional, since not every problem has one. `ToString` renders `Code: Message`, and that is the line the exception message shows.

Return an empty sequence when nothing is wrong. Returning null, or a sequence with a null in it, throws an `InvalidOperationException` naming the rule.

An exception out of a rule becomes a problem of its own. The pass records it as `RF0107`, naming the rule and the exception, drops that rule's findings, and runs the rules after it, so a rule that throws while validating cannot hide what the others found. Startup still fails, because `RF0107` counts like any other problem. What goes missing is the stack trace, so catch inside the rule and report a problem yourself: your message can name the registration you were checking, and `RF0107` cannot.

Two failures stay outside that net. A rule whose constructor throws fails while the container builds the rule list, before any rule validates, and takes the pass down with it. A rule that resolves a dispatcher inside `Validate` never reaches `RF0107` either, because the resolution never returns: see [Registering a rule](#registering-a-rule) below.

## Registering a rule

`AddRequestFlow` returns a builder, and `AddValidationRule` takes the rule type from there:

```csharp
services.AddRequestFlow(o => o
        .RegisterHandlersFromAssemblyContaining<Program>())
    .AddValidationRule<RequestNameRule>();
```

The rule resolves from the container, so constructor dependencies work. `AddValidationRule` registers it as a singleton and offers no other lifetime. The dispatch map is a singleton, so a rule is resolved from the root provider, and a scoped rule or a scoped dependency throws there on a provider that validates scopes.

One singleton instance serves every validation pass, and a failed pass runs again on the next dispatcher resolution ([exceptions.md](exceptions.md#requestflowvalidationexception)). Keep the rule stateless, or register it transient yourself instead of calling `AddValidationRule`:

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

services.TryAddEnumerable(
    ServiceDescriptor.Transient<IRequestFlowValidationRule, RequestNameRule>());
```

That trades one problem for another when the rule is or owns an `IDisposable`. The rule resolves from the root provider, which tracks every transient disposable it creates and releases none of them until the provider is disposed, so a start that keeps failing leaves one instance behind per dispatcher resolution. A rule that clears its state at the top of `Validate` stays a singleton and avoids both.

Do not take `IRequestDispatcher`, `ICommandDispatcher`, or `IQueryDispatcher` in a rule. Resolving one needs the dispatch map the freeze is still building, so the container waits on a result only that freeze can produce. Nothing throws and the stack never overflows. The process never finishes starting.

Most providers reject the rule before it gets that far. The dispatcher is scoped by default and a rule is a singleton, so a provider that validates scopes fails first:

```
Cannot consume scoped service 'RequestFlow.IRequestDispatcher' from singleton
'RequestFlow.IRequestFlowValidationRule'.
```

That is what ASP.NET Core shows in Development, and `ValidateOnBuild` reports it at `BuildServiceProvider`. The hang is what you get on a provider that does not validate scopes, or after `WithTransientDispatcher` makes the dispatcher resolvable from the root. If startup produces no output and no error while the process stays alive, this is why. The fix either way is to drop the dependency.

Handlers and stages are a different case. They resolve without touching the map, so a rule taking one starts fine. The cost is quieter: the rule is a singleton resolved from the root provider, so it pins a transient handler for as long as the provider lives, and once the application calls `WithScopedHandlers` the same rule stops resolving on any provider that validates scopes. Read the model instead; it already names every handler and stage type.

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

    Assert.Equal("ACME0001", Assert.Single(problems).Code);
}
```

`BuildContext` wraps a freshly built model, with both flags where registration leaves them when the application opts out of nothing: a request with no handler is a problem, a stage that reached nothing is not. Pass the one your rule reads to cover the other case, as `BuildContext(unhandledRequestsAllowed: true)`. `Build()` is still there for a test that wants the model on its own.

`RequestFlowValidationContext`, `RequestFlowModel`, `RequestModel`, `HandlerModel`, `StageDeclarationModel`, and `ClosedStageModel` all take internal constructors, so `RequestFlowModelBuilder` is how a test outside the library builds one. Build only the part the rule reads and leave the rest empty: a rule that never looks at stages is tested against requests that have none. Repeated `AddRequest` calls for one request type configure a single entry, the same way the freeze groups handlers by request type. The builder itself only grows by gaining methods, never by widening the ones it has, so a model-building test written against today's builder still compiles once the model gains members.

Two problems are equal when their code, message, and subject match, so a test can assert the whole value rather than pick it apart.

## The model

`context.Model` is the registration as recorded, minus what the shape checks threw out. A declaration reported under `RF0001` to `RF0012` never reaches a rule, so a rule auditing every registered stage type sees only the ones that could run. Past that nothing is cleaned up: a request no handler covers is in the list with an empty `Handlers`, and a stage registered twice appears twice. Every rule reads the same snapshot, and every list on it is read-only, so one rule cannot change what the next one reads.

| Type | Carries |
| --- | --- |
| `RequestFlowModel` | `Requests`, every known request type; `StageDeclarations`, every stage declaration in registration order |
| `RequestModel` | `RequestType`, the `Handlers` covering it, and `Stages`, its closed stage chain in execution order |
| `HandlerModel` | `HandlerType`; `ResponseType`, the item type for a stream handler and null for a void one, which `IsVoid` reports as a `bool`; `Lifetime`, what this handler is registered with; and `ContractType`, the handler contract it implements |
| `StageDeclarationModel` | `StageType`, the type `AddStage` or `AddStreamStage` was given; `ReachedRequests`, the requests that stage type reached; `Lifetime`, what the stage is registered with; and `ContractType`, the stage contract it implements |
| `ClosedStageModel` | `DeclaredType`, the type the registering call was given; `ClosedType`, the stage that runs for this request; and `ContractType` |

`ReachedRequests` is derived from the chains rather than recorded at registration: it holds every request whose `Stages` contains a closing of that `StageType`, in request order. A declaration that closed for nothing has an empty list, and so does one aimed only at requests without a handler, because those get no chain to close into. A model a test builds by hand derives it the same way, from the `AddStage` calls under `AddRequest`, matched on the declared type. Pass `AddStageDeclaration`'s type as `AddStage`'s `declaredType` there; naming the closed type instead leaves `ReachedRequests` empty for a stage the freeze would report as reaching the request.

`StageDeclarationModel.Lifetime` is a `RequestFlowLifetime`: `Transient`, `Scoped`, or `Singleton`. It comes from the declaration's own `AddStage` call, which is where all three are reachable, `AsSingleton` and `AsScoped` included ([lifetimes.md](lifetimes.md)). A singleton stage is shared across concurrent dispatches, so a rule of your own can hold a convention about which stages may take one. The enum mirrors the container's `ServiceLifetime` rather than naming it, because `RequestFlow.Abstractions` does not reference the DI abstractions, so an assembly referencing only the contracts can still read a lifetime.

`HandlerModel.Lifetime` is the same enum, transient or scoped. Each `AddRequestFlow` call decides for the handlers it found ([lifetimes.md](lifetimes.md#lifetime-is-per-registration-call)), so two handlers in one registration can differ and each carries its own:

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

`ContractType` is a `Type`, not an enum, so a package can implement its own handler or stage contract and have it recorded there without RequestFlow needing to know that contract exists ahead of time. The freeze records the most derived contract the type implements: a handler written against `ICommandHandler<TCommand, TResponse>` comes through as `typeof(ICommandHandler<,>)`, and a plain one as `typeof(IRequestHandler<,>)`. When two contracts apply and neither derives from the other, the core contract is recorded rather than one of the two. A rule matching on it compares against the exact contract it declared itself, rather than switching over a fixed set of cases the core would otherwise have to enumerate. The builder takes an open generic interface and throws `ArgumentException` on anything else. A handler entry usually names `IRequestHandler<TRequest>`, `IRequestHandler<TRequest, TResponse>`, or an interface deriving from one, and a stage entry does the same for `IRequestStage`. A contract from another family is recorded as it was given, since `RequestFlow.Abstractions` cannot name every contract a package might add and so does not test membership of a family. A class and a closed interface are still rejected, because neither matches a comparison a rule would write.

Requests come in scan order, followed by request types only a handler brought in. Closing a stage needs a handler, so a request nothing handles has an empty chain whatever stages would otherwise reach it.

`Stages` is a single chain only when the request has one handler, which is what every registration that starts produces. A request two handlers cover is already an `RF0101` failure, and its `Stages` holds what each of them closes, merged into one list, so a single `AddStage` call can show up there twice. A rule that counts or orders stages should skip any request whose `Handlers.Count` is not one, the same way it skips the unhandled case.

`ClosedType` names the type the container resolves, so a two-parameter stage over a void request reads `LoggingStage<Purge, NoResult>` there while the handler's `ResponseType` is null. The two describe different things: what the handler returns, and what the container constructs. Take the response from `HandlerModel.ResponseType` and leave `ClosedType`'s type arguments alone.

## The opt-in flags

The two opt-in flags sit on the context, beside the model. `UnhandledRequestsAllowed` is true once `AllowUnhandledRequests` has been called, `UnusedStagesDisallowed` once `DisallowUnusedStages` has. They decide which built-in rules run, and a rule reads them to word a finding around the choice. `UnusedStageRule` does exactly that: the stage is unused either way, and the flag decides whether its message offers the missing handler as the likely fix or says the missing handler was permitted.

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

## Built-in codes

Codes are stable and never renumbered. `RF0001` to `RF0012` are shape checks on a single declaration, recorded by the `AddRequestFlow` call that made it. The `RF01xx` codes are whole-picture checks that run at the freeze, except `RF0107`, which the pass reports about a rule that threw rather than about a registration. Numbering is allocation order rather than run order: `RF0108` to `RF0113` are freeze checks like `RF0101` to `RF0106`, and they run after all of them. [exceptions.md](exceptions.md) has the cause and the fix behind each one. The `RF` constants live on `ProblemCodes` in `RequestFlow.Abstractions`, and the CQRS one on `CqrsProblemCodes` in `RequestFlow.Cqrs.Abstractions`, so a caller matches `ProblemCodes.UnhandledRequest` rather than a literal without referencing the runtime packages.

| Code | Problem |
| --- | --- |
| `RF0001` | `RegisterGenericHandler` got a type that is not an open generic definition |
| `RF0002` | `RegisterGenericHandler` got an abstract class |
| `RF0003` | A generic handler declares more than one type parameter |
| `RF0004` | A declared handler type does not implement `IRequestHandler` or `IStreamRequestHandler` |
| `RF0005` | A generic handler declaration names no closing types |
| `RF0006` | A closing type is itself open |
| `RF0007` | A closing type violates the handler's generic constraints |
| `RF0008` | `AddStage` got an interface |
| `RF0009` | `AddStage` got an abstract class |
| `RF0010` | `AddStage` got a partially closed type |
| `RF0011` | A registered stage type does not implement `IRequestStage` |
| `RF0012` | An open generic stage does not use its own parameters as the request in its contract |
| `RF0101` | A request has more than one handler |
| `RF0102` | A request has no handler; skipped under `AllowUnhandledRequests` |
| `RF0103` | A stage type is registered more than once |
| `RF0104` | Two or more stage declarations reach one request as the same stage class |
| `RF0105` | A stage applies to no registered request; reported only under `DisallowUnusedStages` |
| `RF0106` | A request implements more than one `IRequest<TResponse>` contract |
| `RF0107` | A validation rule threw; reported by the pass, not by a rule |
| `RF0108` | A request implements more than one `IStreamRequest<TItem>` contract |
| `RF0109` | A request implements both `IRequest<TResponse>` and `IStreamRequest<TItem>` |
| `RF0110` | A stream handler's item type is not the one its request declares |
| `RF0111` | A stream stage's item type is not the one its request declares; reported only when the stage wrapped no handler |
| `RF0112` | A handler's response type is not the one its request declares |
| `RF0113` | A stage's response type is not the one its request declares; reported only when the stage wrapped no handler |
| `CQRS0001` | A request is classified as both a command and a query; contributed by `AddCqrs` |

Problems come out in a fixed order: the shape problems first, then the built-in checks `RF0101` to `RF0106` in the order above, then the stream codes `RF0108` to `RF0111`, then `RF0112` and `RF0113`, then the rules the container holds, in registration order. `RF0108` to `RF0110` come from one rule that walks the requests in scan order and reports each request's problems together, so they group by request rather than by code. The remaining three come from a rule each: `RF0112` walks the requests in scan order, `RF0111` and `RF0113` walk the stage declarations in registration order. An `RF0107` takes the place of whatever the rule that threw would have reported, so it lands in that rule's position in the list.

Give your own codes a prefix that names where they come from, the way `CQRS0001` names the CQRS package. `RF` is reserved for RequestFlow.
