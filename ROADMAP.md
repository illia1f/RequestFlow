# RequestFlow roadmap

Anything past v1 is direction, not commitment.

## v1.0 (in progress)

Deliberately minimal: request/response dispatch, the stage pipeline, and the CQRS layer.

- [x] Core abstractions: `IRequest`, `IRequestHandler<,>`, `NoResult`
- [x] `IRequestDispatcher` and the dispatcher over a frozen dispatch map
- [ ] `IRequestStage` with open, constrained, and closed generic registration
- [ ] `RequestFlow.Cqrs`: `ICommand`/`IQuery`, handler contracts, typed dispatchers
- [x] `AddRequestFlow` registration with assembly scanning and generic handler closings
- [x] Exceptions and startup validation (`ValidateRequestFlow`)
- [ ] NuGet publish and package ID prefix reservation

Targets: `netstandard2.0;net8.0;net10.0`.

## v1.x

- Streaming requests via `IAsyncEnumerable<T>`.

## Under consideration

- Events: in-process publish/subscribe (`IEvent`, `IEventHandler`, `IEventPublisher`). Not planned for now; would be additive if it ever happens.
- Native adapter packages for specific DI containers, if anyone asks.
