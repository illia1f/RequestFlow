# Samples

`Orders.Api` is a minimal API over an in-memory store, wired with `AddRequestFlow`, `AddCqrs`, two
stages, and two validation rules of its own. `Orders.Api.Violations` holds the types those rules
reject, in an assembly the application scans only when you ask it to.

## Run it

```bash
dotnet run --project samples/Orders.Api
```

Swagger is at <http://localhost:5113/swagger> and the document behind it at `/openapi/v1.json`,
both in Development only.

```bash
curl -X POST http://localhost:5113/orders -H "Content-Type: application/json" -d '{"customer":"ada","total":42.5}'
curl http://localhost:5113/orders/{id}
curl -X POST http://localhost:5113/orders/{id}/cancel
```

An empty customer or a total of zero returns 400, thrown by `ValidationStage` before the handler
runs. Cancelling an id the store does not hold returns 404.

## Watch the rules fire

```bash
dotnet run --project samples/Orders.Api -- --break-rules
```

The flag scans `Orders.Api.Violations`, and startup fails with every problem at once:

```
RequestFlow.RequestFlowValidationException: RequestFlow registration is invalid:
RF0102: Request 'Orders.Api.Violations.RefundOrderCommand' has no handler.
CQRS0001: Request 'Orders.Api.Violations.RefundOrderCommand' is classified as both a command and a query; pick one side of the split.
ORDERS0001: Request 'Orders.Api.Violations.FetchOrderDetails' is handled by 'Orders.Api.Violations.FetchOrderDetailsHandler', so its name has to end in 'Query'.
ORDERS0002: Command 'Orders.Api.Violations.ArchiveOrderCommand' has no validation stage in its chain; its handler 'Orders.Api.Violations.ArchiveOrderCommandHandler' has to implement IOrdersCommandHandler.
```

Four problems from three sources: the core reports `RF0102`, `AddCqrs` contributes `CQRS0001`, and
the application's own rules report the other two.

| Code | Rule | Reads |
| --- | --- | --- |
| `ORDERS0001` | `Rules/CommandNamingRule.cs` | `HandlerModel.ContractType`, to tell a command from a query |
| `ORDERS0002` | `Rules/CommandValidatedRule.cs` | `RequestModel.Stages`, the chain as the freeze closed it |

`ORDERS0002` catches a real mistake. `ValidationStage` is filtered to handlers implementing
`IOrdersCommandHandler`, so a command handler that leaves the marker off still compiles and still
dispatches, with no validation in its chain.

[docs/validation-rules.md](../docs/validation-rules.md) covers writing and registering a rule.
