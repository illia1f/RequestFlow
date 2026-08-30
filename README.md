# ![R](https://raw.githubusercontent.com/illia1f/RequestFlow/main/assets/RequestFlowIcon-89x52.png)equestFlow

This is a request/handler mediator for .NET. Built-in validation system checks mediator and application-defined rules at startup. Frozen maps route requests through prebuilt execution plans for extremely fast dispatch with near-zero memory allocations.

Supports handlers, stages, streams, events. Additionally,`RequestFlow.Cqrs` adds a type-enforced command/query split. Registration runs at startup without a source generator, analyzer, or other build step.

[![NuGet](https://img.shields.io/nuget/vpre/RequestFlow?label=nuget)](https://www.nuget.org/packages/RequestFlow)
[![Downloads](https://img.shields.io/nuget/dt/RequestFlow?label=downloads)](https://www.nuget.org/packages/RequestFlow)
[![CI](https://github.com/illia1f/RequestFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/illia1f/RequestFlow/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/illia1f/RequestFlow/blob/main/LICENSE)
![Status](https://img.shields.io/badge/status-preview-orange)
![Targets](https://img.shields.io/badge/targets-net10.0%20%7C%20net8.0%20%7C%20netstandard2.0%20%7C%20net462-512BD4)

> **Status:** [preview on NuGet](https://www.nuget.org/packages/RequestFlow). Install with the `--prerelease` flag:
>
> ```bash
> dotnet add package RequestFlow --prerelease
> ```

## Startup validation

`AddRequestFlow` collects registrations. `ValidateRequestFlow()` closes the stage chains and runs every built-in and application-defined rule. If all validations pass, it freezes the valid model. Otherwise, it reports all problems in a single `RequestFlowValidationException`.

```csharp
builder.Services
    .AddRequestFlow(options =>
    {
        options.RegisterHandlersFromCallingAssembly();
        options.AddStage(
            typeof(ValidationStage<,>),
            stage => stage.WhereHandlerImplements<IOrdersCommandHandler>());
    })
    .AddValidationRule<CommandValidationStageRule>();

WebApplication app = builder.Build();
app.Services.ValidateRequestFlow();
```

In the [modular sample](https://github.com/illia1f/RequestFlow/blob/main/samples/README.md), [`CommandValidationStageRule`](https://github.com/illia1f/RequestFlow/blob/main/samples/Orders.Modules.Orders/Rules/CommandValidationStageRule.cs) reads the frozen stage chain and rejects commands without `ValidationStage<,>`. The convention is checked at startup instead of during code review.

A custom rule can inspect request and response contracts, selected handlers, closed stages, stream shapes, events, and event strategies. It reports into the same exception as the built-in checks. See [Validation rules](https://github.com/illia1f/RequestFlow/blob/main/docs/validation-rules.md).

## Why RequestFlow

- Startup validation reports missing and duplicate handlers, invalid stage closures, event problems, CQRS conflicts, and application-defined rule failures in one exception.
- Request, stream, and event plans freeze once. Dispatch starts with a map lookup and uses no reflection, LINQ, or locking.
- Repeated `AddRequestFlow` calls are additive, so each module can register its assembly into the same application model.
- Requests, streams, and events use separate dispatch surfaces. The optional CQRS package adds command, query, and stream-query dispatchers. Void handlers return plain `Task`.

## Modular monoliths

The sample keeps module registration beside module code:

```csharp
builder.Services.AddOrdersModule().AddCqrs();
builder.Services.AddAuditModule();
```

`AddOrdersModule()` and `AddAuditModule()` each call `AddRequestFlow` for their own assembly and add to the same registry. An `OrderPlaced` event from Orders can reach an Audit handler, and startup validation covers both modules.

See the [sample walkthrough](https://github.com/illia1f/RequestFlow/blob/main/samples/README.md).

## Modern .NET first

RequestFlow targets .NET 10 and .NET 8 directly. It also ships `netstandard2.0` and `net462` assets for applications that still run on older targets.

Runtime registration uses assembly discovery and dynamic generic construction during freeze, so RequestFlow does not support trimming or NativeAOT. See [Compatibility](https://github.com/illia1f/RequestFlow/blob/main/docs/compatibility.md).

## Coming from MediatR

Most request and handler changes are mechanical. Event semantics and some extension points differ.

| MediatR                                       | RequestFlow                                        |
| --------------------------------------------- | -------------------------------------------------- |
| `IRequest<TResponse>`, `IRequest`             | same names in the `RequestFlow` namespace          |
| `IRequestHandler<TRequest, TResponse>.Handle` | `IRequestHandler<TRequest, TResponse>.HandleAsync` |
| `ISender.Send` or `IMediator.Send`            | `IRequestDispatcher.SendAsync`                     |
| `IPipelineBehavior<,>`                        | `IRequestStage<,>`                                 |
| `IStreamRequest<T>` and `CreateStream`        | `IStreamRequest<T>` and `IStreamDispatcher.Stream` |
| `INotification` and `Publish`                 | `IEvent` and `IEventPublisher.PublishAsync`        |
| `services.AddMediatR(...)`                    | `services.AddRequestFlow(...)`                     |

Void handlers return plain `Task`; `Unit` does not appear in user code. Existing `Task` and `Task<T>` handlers keep those return types.

The [MediatR migration guide](https://github.com/illia1f/RequestFlow/blob/main/docs/migrating-from-mediatr.md) covers the file-by-file sequence, event differences, conditional registration, and unsupported extension points.

## Packages

- **[`RequestFlow.Abstractions`](https://www.nuget.org/packages/RequestFlow.Abstractions)** holds requests, handlers, dispatchers, stages, streaming, events, publish strategies, validation models, and exceptions. It has no package dependency on `net8.0` or `net10.0`.
- **[`RequestFlow`](https://www.nuget.org/packages/RequestFlow)** adds dispatch, event publication, assembly and manual registration, startup validation, and frozen execution plans.
- **[`RequestFlow.Cqrs.Abstractions`](https://www.nuget.org/packages/RequestFlow.Cqrs.Abstractions)** holds command, query, stream-query, handler, and typed-dispatcher contracts.
- **[`RequestFlow.Cqrs`](https://www.nuget.org/packages/RequestFlow.Cqrs)** adds the typed dispatchers and `AddCqrs()` validation rule on top of the core runtime.

Install a runtime package at the composition root. Reference an abstractions package directly from a domain or application layer that should not depend on runtime registration.

## Documentation

- [Getting started](https://github.com/illia1f/RequestFlow/blob/main/docs/getting-started.md): install, first request and handler, dispatching
- [Registration](https://github.com/illia1f/RequestFlow/blob/main/docs/registration.md): scanning, manual registration, generic handlers, additive calls, startup validation
- [Stages](https://github.com/illia1f/RequestFlow/blob/main/docs/stages.md): wrapping handlers, execution order, filters, and request selection
- [Streaming](https://github.com/illia1f/RequestFlow/blob/main/docs/streaming.md): stream requests, stream stages, cancellation, and enumeration timing
- [Events](https://github.com/illia1f/RequestFlow/blob/main/docs/events.md): polymorphic delivery, strategies, ordering, failures, and cancellation
- [Validation rules](https://github.com/illia1f/RequestFlow/blob/main/docs/validation-rules.md): application-defined checks over the frozen registration model
- [Compatibility](https://github.com/illia1f/RequestFlow/blob/main/docs/compatibility.md): modern targets, downlevel targets, trimming, and NativeAOT
- [Migrating from MediatR](https://github.com/illia1f/RequestFlow/blob/main/docs/migrating-from-mediatr.md): concept mapping and semantic differences
- [Service lifetimes](https://github.com/illia1f/RequestFlow/blob/main/docs/lifetimes.md): handler, stage, dispatcher, and publisher lifetimes
- [Exceptions](https://github.com/illia1f/RequestFlow/blob/main/docs/exceptions.md): exceptions, timing, and fixes
- [Modular sample](https://github.com/illia1f/RequestFlow/blob/main/samples/README.md): an API host with independently registered Orders and Audit modules

## Contributing

Design feedback is the most useful contribution while the packages are in preview.

## License

MIT. See [LICENSE](https://github.com/illia1f/RequestFlow/blob/main/LICENSE).
