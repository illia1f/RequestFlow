# RequestFlow roadmap

Anything past v1 is direction, not commitment.

## v1.0 (in progress)

Deliberately minimal: request/response dispatch, the stage pipeline, and the CQRS layer.

- [x] Core abstractions: `IRequest`, `IRequestHandler<,>`, `NoResult`
- [x] `IRequestDispatcher` and the dispatcher over a frozen dispatch map
- [x] `IRequestStage` with open, constrained, and closed generic registration
- [x] CQRS layer: `ICommand`/`IQuery` and handler contracts in `RequestFlow.Cqrs.Abstractions`, typed dispatchers and `AddCqrs` registration in `RequestFlow.Cqrs`
- [x] `AddRequestFlow` registration with assembly scanning and generic handler closings
- [x] Exceptions and startup validation (`ValidateRequestFlow`)
- [x] NuGet publish: all four packages are up at `1.0.0-preview.1`
- [ ] `RequestFlow.*` package ID prefix reservation
- [ ] Benchmark suite: BenchmarkDotNet against MediatR, martinothamar/Mediator, and LiteBus as pinned package references, raw artifacts committed

Targets: `netstandard2.0;net462;net8.0;net10.0`. The published `1.0.0-preview.1` predates the net462 target, so that one first ships in the next preview.

## v1.x

- Events: in-process publish/subscribe (`IEvent`, `IEventHandler`, `IEventPublisher`), RequestFlow's answer to MediatR notifications. Delivery, ordering, concurrency, failure, and cancellation semantics get written down before any code.
- Streaming requests via `IAsyncEnumerable<T>`.
