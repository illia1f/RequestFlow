# RequestFlow roadmap

Anything past v1 is direction, not commitment.

Release: [1.0.0-preview.9](CHANGELOG.md#100-preview9---2026-09-08).

## v1.0 (in progress)

- [x] Core abstractions: `IRequest`, `IRequestHandler<,>`, `NoResult`
- [x] `IRequestDispatcher` and the dispatcher over a frozen dispatch map
- [x] `IRequestStage` with open, constrained, and closed generic registration
- [x] CQRS contracts in `RequestFlow.Cqrs.Abstractions`; typed dispatchers and `AddCqrs` in `RequestFlow.Cqrs`
- [x] `AddRequestFlow` registration with assembly scanning and generic handler closings
- [x] Manual request and event handler registration, scan exclusions, and explicit event registration
- [x] Missing-handler exemptions by message type or declaring assembly
- [x] Exceptions and startup validation (`ValidateRequestFlow`)
- [x] Validation as pluggable rules: `IRequestFlowValidationRule`, `AddValidationRule<T>()`, and stable problem codes on `RequestFlowValidationException`
- [x] Pipeline inspection through `InspectRequestFlow`, including declared handlers, ordered stages, and exclusion reasons
- [x] NuGet publishing for all four packages
- [x] `RequestFlow.*` package ID prefix reservation
- [x] Events: `IEvent`, `IEventHandler`, and `IEventPublisher`, with built-in and custom publish strategies
- [x] Streaming requests via `IAsyncEnumerable<T>`
- [x] Opt-in ValueTask request family: `IValueRequest`, ValueTask stages, and CQRS command/query twins
- [x] MediatR migration and target compatibility guides
- [ ] BenchmarkDotNet suite against pinned MediatR, martinothamar/Mediator, LiteBus, and DispatchR packages, with raw results

Targets: `netstandard2.0;net462;net8.0;net10.0`. The `net462` target has shipped since `1.0.0-preview.2`.

## After v1.0
