# RequestFlow roadmap

Anything past v1 is direction, not commitment.

## v1.0 (in progress)

Request/response dispatch, the stage pipeline, the CQRS layer, events, and streaming. Nothing past what a mediator needs.

- [x] Core abstractions: `IRequest`, `IRequestHandler<,>`, `NoResult`
- [x] `IRequestDispatcher` and the dispatcher over a frozen dispatch map
- [x] `IRequestStage` with open, constrained, and closed generic registration
- [x] CQRS layer: `ICommand`/`IQuery` and handler contracts in `RequestFlow.Cqrs.Abstractions`, typed dispatchers and `AddCqrs` registration in `RequestFlow.Cqrs`
- [x] `AddRequestFlow` registration with assembly scanning and generic handler closings
- [x] Exceptions and startup validation (`ValidateRequestFlow`)
- [x] Validation as pluggable rules: `IRequestFlowValidationRule`, `AddValidationRule<T>()`, and stable problem codes on `RequestFlowValidationException`
- [x] NuGet publish: all four packages, latest preview `1.0.0-preview.5`
- [x] `RequestFlow.*` package ID prefix reservation
- [ ] Events: in-process publish/subscribe (`IEvent`, `IEventHandler`, `IEventPublisher`), RequestFlow's answer to MediatR notifications. Delivery, ordering, concurrency, failure, and cancellation semantics are settled and get written down before any code.
- [ ] Streaming requests via `IAsyncEnumerable<T>`. The stream methods carry no `Async` suffix, because what they return is not awaitable.
- [ ] Benchmark suite in the repository: BenchmarkDotNet against MediatR, martinothamar/Mediator, LiteBus, and DispatchR as pinned package references, with the raw artifacts, so the README can quote numbers instead of pointing here

Targets: `netstandard2.0;net462;net8.0;net10.0`. The `net462` target has shipped since `1.0.0-preview.2`.

## After v1.0

...
