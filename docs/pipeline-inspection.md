# Pipeline inspection

Run the [sample inspection example](../samples/README.md#inspect-the-pipelines) to see a command with validation and queries where the handler filter excludes it:

```bash
dotnet run --project samples/Orders.Api -- --inspect-pipelines
```

Sample output:

```text
RequestFlow pipeline inspection
Declared registration; DI replacements and factories can supply different instances.

Request: CreateOrderCommand
Family: Task
Response: Guid
Declared handler: CreateOrderCommandHandler (Transient)
Handler service: IRequestHandler<CreateOrderCommand, Guid>
Stages, outermost first:
  1. LoggingStage<CreateOrderCommand, Guid> (Transient)
  2. ValidationStage<CreateOrderCommand, Guid> (Transient)
Excluded stages:
  (none)

Request: GetOrderQuery
Family: Task
Response: OrderDto
Declared handler: GetOrderQueryHandler (Transient)
Handler service: IRequestHandler<GetOrderQuery, OrderDto>
Stages, outermost first:
  1. LoggingStage<GetOrderQuery, OrderDto> (Transient)
Excluded stages:
  ValidationStage<TRequest, TResponse>: HandlerFilterNotMatched

Request: GetOrderActivityQuery
Family: Task
Response: OrderActivity
Declared handler: GetOrderActivityQueryHandler (Transient)
Handler service: IRequestHandler<GetOrderActivityQuery, OrderActivity>
Stages, outermost first:
  1. LoggingStage<GetOrderActivityQuery, OrderActivity> (Transient)
Excluded stages:
  ValidationStage<TRequest, TResponse>: HandlerFilterNotMatched
```

Inspect a request's declared handler, ordered stages, and excluded stages without sending a request:

```csharp
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

var pipeline = provider.InspectRequestFlow<PlaceOrder>();

Console.WriteLine(pipeline.RequestType);
Console.WriteLine(pipeline.Family);
Console.WriteLine(pipeline.DeclaredHandler.HandlerType);
Console.WriteLine(pipeline.HandlerServiceType);

foreach (var stage in pipeline.Stages)
{
    Console.WriteLine($"{stage.ClosedType}: {stage.DeclaredLifetime}");
}

foreach (var stage in pipeline.ExcludedStages)
{
    Console.WriteLine($"{stage.DeclaredType}: {stage.ReasonCode}");
}
```

- Call after building the provider and completing registration.
- Inspection validates and freezes the same maps used by dispatch. A prior `ValidateRequestFlow()` call is optional.
- Invalid registration throws the existing `RequestFlowValidationException`, including event and application-defined validation problems.
- Inspection throws `HandlerNotFoundException` for unknown requests and requests without handlers. Missing-handler exemptions permit startup but do not create pipelines.
- Metadata is read-only and retained for the provider. Repeated inspection returns the same description.
- Inspection does not resolve handlers or stages, invoke their factories, send requests, or enumerate streams. Normal startup validation rules run if the maps have not frozen yet.

## Request types and families

The generic overload uses the exact type argument. The `Type` overload supports a request's runtime type:

```csharp
var pipeline = provider.InspectRequestFlow(request.GetType());
```

| Request contract | `Family` | `DeclaredHandler.ResponseType` |
| --- | --- | --- |
| `IRequest<TResponse>` | `Task` | `typeof(TResponse)` |
| `IRequest` | `Task` | `null` |
| `IValueRequest<TResponse>` | `ValueTask` | `typeof(TResponse)` |
| `IValueRequest` | `ValueTask` | `null` |
| `IStreamRequest<TItem>` | `Stream` | `typeof(TItem)` |

- `DeclaredHandler.IsVoid` identifies a handler without a response value.
- CQRS commands, queries, and stream queries use the same inspection API through their core request contracts.
- `HandlerServiceType` is the closed core handler contract dispatch resolves from DI.

## Stage order and exclusions

- `Stages` is ordered outermost first, including registrations accumulated across `AddRequestFlow` calls.
- `DeclaredType` identifies the registration, such as `LoggingStage<,>`. `ClosedType` identifies the selected service, such as `LoggingStage<PlaceOrder, Guid>`.
- `HandlerFilter` records the optional `WhereHandlerImplements<TContract>()` filter.
- `ExcludedStages` contains registered stages that did not match this request, in declaration order. It includes stages from other request families.
- A stage absent from registration has no entry.

| `StageExclusionReason` | Meaning | Example |
| --- | --- | --- |
| `DifferentFamily` | The stage's registration family differs from the handler's Task, ValueTask, or stream family. | A stage registered with `AddValueStage` is inspected for a Task request. |
| `HandlerFilterNotMatched` | The declared handler type is not assignable to the configured handler filter. | A stage requires `WhereHandlerImplements<IOrdersCommandHandler>()`, but the query handler does not implement `IOrdersCommandHandler`. |
| `GenericConstraintsNotSatisfied` | The type arguments used to close the open stage do not satisfy its generic constraints. | A stage requires `TRequest : IValidatedRequest`, but the request does not implement `IValidatedRequest`. |
| `ContractNotCompatible` | The closed stage does not implement a compatible stage contract for this request. | A stage declared only for `CreateOrderCommand` is inspected for the unrelated `GetOrderQuery`. |

- Reasons record the first failed check in the table's order. They do not list every possible mismatch.
- A stage from another family reports `DifferentFamily` even when its handler filter would also fail.
- Use `ReasonCode` for programmatic checks. `DeclaredType` and `HandlerFilter`, together with the pipeline's `RequestType` and `DeclaredHandler`, identify the registrations involved.
- Exclusion can be intentional. A validation rule or `DisallowUnusedStages()` can reject unwanted configuration.

Inspection describes the configured pipeline. A stage can short-circuit, call its continuation more than once, or choose its behavior from request data. The description does not record which stages ran, timing, cache hits, retries, or stream items.

## Declared registration and DI resolution

`DeclaredHandler` describes the handler registered through RequestFlow. Its `Lifetime` and each included stage's `DeclaredLifetime` describe RequestFlow's declarations.

Application registrations can replace the handler or stage service, change its lifetime, or supply a factory. Inspection does not determine the object DI will return.

For example, an application can register `PlaceOrderHandler` through RequestFlow, then replace its handler service:

```csharp
services.AddTransient<
    IRequestHandler<PlaceOrder, Guid>,
    AuditedPlaceOrderHandler>();
```

- Register the replacement before building the provider.
- With the default container, dispatch resolves the later registration for that handler contract.
- Inspection continues to report `PlaceOrderHandler` as the declared handler and `IRequestHandler<PlaceOrder, Guid>` as the handler service.
- `WhereHandlerImplements<TContract>()` selects stages using `PlaceOrderHandler`. Adding or removing the filter interface on the replacement does not change the frozen stage chain.
- A DI replacement alone does not supply a missing RequestFlow handler declaration or resolve duplicate-handler validation errors.

See [Registration](registration.md), [Stages](stages.md), and [Validation rules](validation-rules.md).
