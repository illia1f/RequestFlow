# Modular monolith sample

The sample splits a minimal Orders API into two application modules. Each module owns its handlers and calls `AddRequestFlow` for its assembly. The host validates both modules before it starts.

| Project | Owns |
| --- | --- |
| `Orders.Api` | composition root, exception mapping, OpenAPI, host startup |
| `Orders.Modules.Orders` | order commands, order queries, endpoints, stages, event contracts, and Orders architecture rules |
| `Orders.Modules.Audit` | order-event subscribers, activity state, activity query, and activity endpoint |
| `Orders.Api.Violations` | request and event types loaded only by the `--break-rules` run |

Audit references the Orders event contracts. Orders does not reference Audit.

## Module registration

The host registers both modules:

```csharp
builder.Services.AddOrdersModule().AddCqrs();
builder.Services.AddAuditModule();
```

`AddOrdersModule()` registers:

- the Orders assembly scan
- `OrderStore`
- `LoggingStage<,>` for every request
- `ValidationStage<,>` for handlers implementing `IOrdersCommandHandler`
- parallel publication for `OrderPlaced`
- fail-fast publication for `OrderCancelled`
- `CommandNamingRule` and `CommandValidationStageRule`

`AddAuditModule()` registers the Audit assembly scan, `OrderMetrics`, and `OrderAuditTrail`.

Both `AddRequestFlow` calls contribute to one registry. `ValidateRequestFlow()` validates the handlers, stages, events, and rules from both modules, then freezes their plans together.

## Cross-module flow

1. `POST /orders` sends `CreateOrderCommand`.
2. The Orders handler saves the order and publishes `OrderPlaced`.
3. Audit handles the event through `IEventHandler<OrderEvent>` and records an entry.
4. `GET /orders/activity` sends `GetOrderActivityQuery` to the Audit module.

## Run the API

```bash
dotnet run --project samples/Orders.Api
```

Swagger is at <http://localhost:5113/swagger> and the OpenAPI document is at `/openapi/v1.json`, both in Development only.

```bash
curl -X POST http://localhost:5113/orders -H "Content-Type: application/json" -d '{"customer":"ada","total":42.5}'
curl http://localhost:5113/orders/{id}
curl -X POST http://localhost:5113/orders/{id}/cancel
curl http://localhost:5113/orders/activity
```

An empty customer or a total of zero returns 400 because `ValidationStage` stops the chain before the handler. Cancelling an unknown id returns 404.

## Run the validation failures

```bash
dotnet run --project samples/Orders.Api -- --break-rules
```

The flag adds `Orders.Api.Violations` through another `AddRequestFlow` call. Startup reports every problem and exits:

```text
RequestFlow.RequestFlowValidationException: RequestFlow registration is invalid:
RF0102: Request 'Orders.Api.Violations.RefundOrderCommand' has no handler.
RF0114: Event 'Orders.Api.Violations.OrderRefunded' has no handler.
ORDERS0001: Request 'Orders.Api.Violations.FetchOrderDetails' is handled by 'Orders.Api.Violations.FetchOrderDetailsHandler', so its name has to end in 'Query'.
ORDERS0002: Command 'Orders.Api.Violations.ArchiveOrderCommand' has no validation stage in its chain; its handler 'Orders.Api.Violations.ArchiveOrderCommandHandler' has to implement IOrdersCommandHandler.
CQRS0001: Request 'Orders.Api.Violations.RefundOrderCommand' is classified as both a command and a query; pick one side of the split.
```

The codes come from three sources:

| Codes | Source |
| --- | --- |
| `RF0102`, `RF0114` | RequestFlow core |
| `CQRS0001` | `AddCqrs()` |
| `ORDERS0001`, `ORDERS0002` | Orders module rules |

`ORDERS0002` reads `RequestModel.Stages`, the frozen stage chain. If a command handler omits `IOrdersCommandHandler`, the stage filter no longer selects it and the custom rule rejects the application.

See [Validation rules](../docs/validation-rules.md) for the full model and registration API.
