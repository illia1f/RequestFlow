# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Pre-1.0: the public API can still change between previews.

The `release` workflow uses the section matching the pushed tag as the GitHub Release body. A missing section fails the build before publishing. Before tagging, rename `[Unreleased]` to the release version and add its date.

## [Unreleased]

### Added

- `RegisterHandlersFromCallingAssembly()` scans the assembly containing the `AddRequestFlow` configuration delegate, so no marker type is needed. Use `RegisterHandlersFromAssemblyContaining<T>()` or `RegisterHandlersFromAssembly(assembly)` when the target assembly must be explicit.
- `AddHandler<THandler>()` registers a Task, ValueTask, or stream handler without scanning its assembly. `ExcludeHandler<THandler>()` excludes it from the same call's scan. Scanned and manually added handlers register once. Invalid handler types produce `RF0018` to `RF0020`.
- ValueTask requests, handlers, and `IValueRequestDispatcher`, with `IValueRequestStage`, `ValueContinuation`, and `AddValueStage` for stages. Task and ValueTask chains stay separate; existing Task contracts are unchanged. See [ValueTask requests](docs/value-tasks.md) for usage and consumption rules.
- ValueTask commands and queries: `IValueCommand<TResponse>`, `IValueCommand`, `IValueQuery<TResponse>`, and their handlers. `AddCqrs` registers `IValueCommandDispatcher` and `IValueQueryDispatcher`.
- ValueTask validation codes: `RF0125` rejects more than one `IValueRequest<TResponse>` contract; `RF0126` to `RF0128` reject ValueTask contracts combined with Task, stream, or event contracts; and `RF0129` and `RF0130` reject handler or stage response mismatches.
- `RF0124`: a handler declared against an interface or abstract request type now fails the freeze, from any registration source, when the target cannot expose the declared type as the request's exact runtime type. The `net462` asset keeps interface and abstract `MarshalByRefObject` handlers valid for `RealProxy` transparent proxies.

### Fixed

- `AddEventHandler<THandler>()` and `RegisterGenericHandler(...)` with a type whose interface list cannot load, because an interface's assembly is not deployed, no longer throw a raw loader exception out of `AddRequestFlow`; the freeze reports `RF0017` or `RF0004` beside the other problems.

## [1.0.0-preview.8] - 2026-08-24

### Added

- Events: `IEvent`, `IEventHandler<TEvent>`, and `IEventPublisher.PublishAsync`. The same assembly scan that finds request handlers finds event handlers, and `AddRequestFlow` registers the publisher beside the dispatchers, so there is no separate call to make. [events.md](docs/events.md) covers the feature.
- Delivery is polymorphic and frozen at startup: a handler declared for a base class or interface receives every known assignable event, in one fixed order per event. Handlers register under their concrete type, not `IEventHandler<TEvent>`, so your own interface descriptor neither replaces nor joins publication.
- A publish strategy decides how the handlers run: `SequentialPublishStrategy` by default, `ParallelPublishStrategy`, and `FailFastPublishStrategy` to stop at the first failure. `PublishEventsInParallel()`, `PublishEventsSequentially()`, and `PublishEventsFailFast()` select a built-in globally without naming the class, each with a `<TEvent>` overload for one assignable event target. Sequential and parallel give every applicable handler a turn unless a cancellation check stops the walk, and collect what failed into `EventPublishException.Failures`.
- Write your own strategy against `IEventPublishStrategy` and the testable `EventDelivery`. Select one globally with `PublishAllEventsWith<TStrategy>()` or for one assignable event type with `PublishEventsWith<TEvent, TStrategy>()`, and set its lifetime through `EventStrategyOptions`; a custom strategy resolves from DI as a singleton unless you say otherwise.
- Event exceptions: `EventNotRegisteredException` (a published type the event map does not know), `EventPublishException` (the collected handler failures), `EventPublishCanceledException` (a publisher-acknowledged stop), `EventHandlerNullTaskException` (a handler that returns a null task), and `EventStrategyNullTaskException` (a strategy that returns a null task). `EventHandlerFailure` names the handler, its declared contract, and the cause.
- Event validation codes: `RF0114` (a known event with no applicable handler, allowed by `AllowUnhandledEvents()`), `RF0115` (a subscription that reaches no known event, reported only under `DisallowUnusedEventHandlers()`), `RF0116` (a type that is both a request and an event), `RF0117` (a type that is both a stream request and an event), and `RF0118` (a stage class that also handles events under a different lifetime, where the descriptor registered last decides how both roles resolve).
- Event strategy validation codes: `RF0013`, `RF0014`, and `RF0119` through `RF0123` cover invalid strategy shapes, conflicting or ambiguous selection, lifetime conflicts, unused declarations, and shared-role lifetime collisions.
- The validation model carries events: `RequestFlowModel.Events`, `EventSubscriptions`, and `EventStrategies`, the `EventModel`, `EventHandlerModel`, `EventSubscriptionModel`, and `EventStrategyModel` types, and the `UnhandledEventsAllowed` and `UnusedEventHandlersDisallowed` flags on the context. `RequestFlowModelBuilder` gains `AddEvent`, `AddEventHandler`, `PublishEventsWith`, and a four-flag `BuildContext` overload beside the unchanged two-flag one; `AddEvent` takes a concrete closed event type and rejects anything the model could not hold.
- `IEventPublisher` follows the dispatchers' lifetime: scoped by default, transient under `WithTransientDispatcher`.
- `AddEventHandler<THandler>()` registers one event handler without scanning its assembly, and `ExcludeEventHandler<THandler>()` keeps one handler out of the same call's scan, so MediatR's conditional `AddTransient<INotificationHandler<X>, H>()` and scan-filter patterns port directly. A handler both scanned and added manually delivers once, and a type that is not a concrete event handler class is a startup problem (`RF0015` to `RF0017`).

### Changed

- The request and event maps freeze together as one `FrozenPlans` singleton, on the first dispatcher or publisher resolution. `ValidateRequestFlow` forces that single freeze, and one `RequestFlowValidationException` still lists every problem from both halves.
- `RF0109` now comes from the shared contract-conflict rule instead of the stream contract rule. On a type that also carries two stream contracts, other problems can now fall between `RF0108` and `RF0109` in `Problems`, and `RF0110` precedes `RF0109` instead of following it.

## [1.0.0-preview.7] - 2026-08-15

### Added

- Streaming requests: `IStreamRequest<TItem>`, `IStreamRequestHandler<TRequest, TItem>`, and `IStreamDispatcher.Stream`, which returns an `IAsyncEnumerable<TItem>`. `AddRequestFlow` registers the stream dispatcher beside the request one, so there is no separate call to make. [streaming.md](docs/streaming.md) covers the feature, including why `Stream` and `Handle` carry no `Async` suffix.
- Stream stages: `IStreamRequestStage<TRequest, TItem>`, registered with `AddStreamStage` under the same rules as `AddStage`. A stage receives a `StreamContinuation<TItem>` and can filter, project, inject items, or stop the walk early. Stream stages and task stages never wrap each other.
- Every streaming contract ships in `RequestFlow.Abstractions`. On `netstandard2.0` and `net462` the package now carries `Microsoft.Bcl.AsyncInterfaces`; `net8.0` and `net10.0` keep zero dependencies.
- Stream queries for the CQRS split: `IStreamQuery<TItem>`, `IStreamQueryHandler<TQuery, TItem>`, and `IStreamQueryDispatcher`, all in `RequestFlow.Cqrs.Abstractions`. `AddCqrs` registers the stream query dispatcher beside the command and query ones; the contracts derive from the core streaming set, so the assembly scan discovers the handlers with no extra registration. The split rule counts a stream query as the query side, so a command that is also a stream query now fails the freeze with `CQRS0001`; core validation reports the same type as `RF0109`, with or without `AddCqrs`.
- `HandlerNullStreamException` and `StageNullStreamException` for a null sequence out of `Handle`, sharing a `NullStreamException` base. Both surface from enumeration, not from the `Stream` call.
- Streaming validation codes: `RF0108` (a request with more than one `IStreamRequest<TItem>` contract), `RF0109` (a request that is both `IRequest<TResponse>` and `IStreamRequest<TItem>`), and `RF0110`/`RF0111` (a stream handler or stage whose item type is wider than its request declares).
- Task-path validation codes: `RF0112` (a handler whose response type is not the one its request declares) and `RF0113` (a stage with the same mismatch). Both compile through covariance and used to fail only at dispatch.
- Cancellation on a stream reads the token passed to `Stream` and the one passed to `WithCancellation`; either cancels the chain. A stage passing its own token to `Invoke` replaces both for the levels below it.
- A stream stage written as an async iterator allocates its state machine and enumerator once per enumeration, per level; one that returns `next.Invoke(...)` directly allocates nothing. Neither cost grows with the number of items.

### Changed

- `RequestFlowModelBuilder` and `RequestModelBuilder` accept any open generic interface as a `ContractType`, not only ones built on `IRequestHandler` or `IRequestStage`. A class or a closed interface is still rejected.
- `RF0004`'s message now reads `does not implement IRequestHandler or IStreamRequestHandler`; anything matching on the old text breaks.
- `ResponseTypeMismatchException`'s message now says `but the call site used response type` instead of naming `SendAsync`, because `IStreamDispatcher.Stream` throws it too. Anything matching on the old text breaks.

## [1.0.0-preview.6] - 2026-08-09

### Added

- `IRequestFlowValidationRule`: any package or application can add its own checks to the startup validation pass with `AddValidationRule<T>()` and report into the same exception as the built-in checks. A rule reads the whole registration picture off `RequestFlowValidationContext`, including the `AllowUnhandledRequests` and `DisallowUnusedStages` opt-ins. A rule that throws is reported as `RF0107` and the rules after it still run; [validation-rules.md](docs/validation-rules.md) covers writing, registering, and testing one.
- `RequestFlowModelBuilder` builds a `RequestFlowModel` by hand and `BuildContext` wraps one in the context a rule receives, which is how a rule is unit tested without a container.
- Startup validation rejects a request type that implements more than one `IRequest<TResponse>` contract (`RF0106`). The extra contract used to pass the freeze and fail every dispatch under it with `ResponseTypeMismatchException`.
- `AddCqrs` rejects a request classified as both a command and a query (`CQRS0001`).
- `ProblemCodes` and `CqrsProblemCodes` are public and ship in the abstractions packages, so an assembly referencing only those can match `ProblemCodes.UnhandledRequest` instead of a literal string.
- The model reports lifetimes as `RequestFlowLifetime` rather than the container's `ServiceLifetime`, since `RequestFlow.Abstractions` takes no dependency on the DI package. Handlers and stages also report the contract they implement as `ContractType`, so a kind contributed on top of a core contract, such as `ICommandHandler`, comes through under its own. Every list on the model is read-only.

### Changed

- `RequestFlowValidationException.Problems` holds `RequestFlowValidationProblem` values (stable code, message, offending type) instead of strings, and the public constructor takes the same list, so a call site passing strings no longer compiles. Message lines now read `RF0101: ...`, so anything matching on the old text breaks. Repeated registrations collapse into one problem each, where a request with three handlers used to report two identical lines.
- Stage declarations that alias one stage class report a single `RF0104` naming every declaration in the collision, instead of a line per colliding pair. `RF0104` also no longer fires for two closings of one stage class on a request with more than one handler: that request already fails on `RF0101`, and the collision surfaces once the duplicate handler is gone.

## [1.0.0-preview.5] - 2026-08-07

### Added

- `Continuation<TResponse>.Over(rest)` and `Continuation.Over(rest)` put a delegate in place of the rest of the chain, so a stage unit-tests with no container and no dispatcher. [stages.md](docs/stages.md) has a retry stage tested that way. The delegate receives the token the stage passed to `InvokeAsync`, or `Over`'s optional second argument when the call named none; a default `Continuation` has no chain behind it and throws `InvalidOperationException`.

### Changed

- `IContinuation<TResponse>` and `IContinuation` are gone. A stage now takes `Continuation<TResponse>` or `Continuation`, two `readonly struct`s that wrap a chain built once when the dispatch map freezes, and every call carries its own provider and cancellation token through it. The calls on `next` are unchanged, so migrating a stage means editing one parameter type; migrating its tests takes more, because a struct cannot be substituted, so build a real one with `Over`.
- Stages and the handler resolve on every entry into their level instead of once per dispatch. So the lifetime you registered decides what a second `next` call gets: a transient stage is built again, a scoped one comes back as the same instance. [lifetimes.md](docs/lifetimes.md) covers who disposes the extra instances when the dispatcher comes from the root provider.
- A new logo, on the package icon and in the README. The icon crops to the R and the arrow so it fills its canvas horizontally, which is the axis nuget.org's icon slot scales by. The brighter palette also holds up against the dark package page, where the old purple nearly disappeared.

### Removed

- `OverlappingNextCallException`. A stage can now call `next` again while an earlier call is still running, which is what hedging and shadow comparison need. The catch: a scoped or singleton stage below an overlapping one is one instance running in two walks at once, so it has to be thread safe inside one dispatch and not only across dispatches. A transient stage stays clear of that, and [stages.md](docs/stages.md) shows how to keep a failure on one call from leaving the other walk unawaited.

### Performance

- Allocation per dispatch no longer grows with the chain: levels are built once when the dispatch map freezes, so five stages cost what no stages cost, and a repeated `next` call allocates nothing of RequestFlow's. Stage instances are still the container's to allocate, on the lifetime you registered.
- No atomic operations left on the `next` path.
- Void requests still cross a `Task` to `Task<NoResult>` bridge at every level. It costs nothing for a level that already finished, or for a stage that hands back the task its own `next` call returned. A stage marked `async` pays one task per level of that shape.

## [1.0.0-preview.4] - 2026-08-03

### Added

- `HandlerNullTaskException` and `StageNullTaskException` for a null task out of `HandleAsync`, and `OverlappingNextCallException` for a stage that calls `next` while its earlier call is still running. Each carries the type at fault (`RequestType` or `StageType`). The two null-task types share a `NullTaskException` base, and all three derive from `InvalidOperationException`, which these cases used to throw plain.

### Changed

- A stage receives `next` as `IContinuation<TResponse>`, or `IContinuation` on the void form. The `StageDelegate` types are gone: call `await next.InvokeAsync()` where you called `await next()`.
- `next.InvokeAsync` takes an optional `CancellationToken`. Omit it and the call continues under the token the stage received, which is the previous behaviour. Pass one and it replaces the token for every level below the stage, the handler included, which is what a timeout stage needs to stop the work rather than only stop waiting for it. [stages.md](docs/stages.md) has the linked-token timeout stage, and a retry stage around it composes.
- A stage and the handler resolve from the container when their level first runs, not up front. A stage that short-circuits builds nothing below it, and a repeated `next` call reuses what the dispatch already resolved. A container failure now surfaces out of the `next.InvokeAsync()` call at that level, where the stages around it can catch it. Turn on `ServiceProviderOptions.ValidateOnBuild` to keep registration mistakes at startup.
- Handlers register transient or scoped, nothing else. `WithHandlerLifetime(ServiceLifetime)` is gone, `WithScopedHandlers()` replaces it, and `HandlerLifetime` and `DispatcherLifetime` on `RequestFlowOptions` are internal now. Registering a singleton handler by hand still works, in the order [lifetimes.md](docs/lifetimes.md) shows.
- Each stage declares its own lifetime through `AsSingleton()` and `AsScoped()` on the `AddStage` delegate, transient when neither is called. Naming two different lifetimes throws. The delegate parameter is now `StageOptions` instead of `StageApplicability`; `WhereHandlerImplements` is unchanged.
- `AddStage` appends its own descriptor even when the service collection already holds the closed stage type, so the declared lifetime always applies. One ordering rule now covers stages and handlers alike: register your own after the last `AddRequestFlow` call, where the container takes the last descriptor for a service type. [stages.md](docs/stages.md) covers the `Replace` and `ValidateOnBuild` corners.
- The embedded package icon is a near-square crop of the logo. nuget.org draws it into a fixed 32x32 `object-fit: contain` box, where the old wide image left most of the height empty.

### Performance

- A dispatch through N stages allocates N objects instead of 2N+1, and a repeated `next` call allocates nothing.

## [1.0.0-preview.3] - 2026-08-02

### Added

- An embedded package icon on all four packages. nuget.org and the Visual Studio package manager show the RequestFlow logo instead of the default placeholder.

## [1.0.0-preview.2] - 2026-08-01

### Added

- `IRequestStage`, a stage that wraps handler execution. Stages run in registration order around the handler, so cross-cutting work like logging or validation lives outside the handler.
- An explicit `net462` target on all four packages. .NET Framework consumers already worked through `netstandard2.0`; the difference is that .NET Framework now shows up as a supported framework on the NuGet page.

## [1.0.0-preview.1] - 2026-07-12

First public preview.

### Added

- `RequestFlow.Abstractions`: `IRequest<TResponse>`, `IRequest`, `IRequestHandler<TRequest, TResponse>`, `IRequestHandler<TRequest>`, `IRequestDispatcher`, and the dispatch exceptions.
- `RequestFlow`: `AddRequestFlow` assembly scanning, startup validation that reports every registration problem in one `RequestFlowValidationException`, and a dispatcher that resolves handlers through a frozen per-request-type map with no reflection, LINQ, or locking on the dispatch path.
- `RequestFlow.Cqrs.Abstractions` and `RequestFlow.Cqrs`: command and query contracts with typed dispatchers, registered through `AddCqrs`, for codebases that want the split enforced by the compiler.
- `provider.ValidateRequestFlow()` to force validation at startup instead of at the first dispatch.

[Unreleased]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.8...HEAD
[1.0.0-preview.8]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.7...v1.0.0-preview.8
[1.0.0-preview.7]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.6...v1.0.0-preview.7
[1.0.0-preview.6]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.5...v1.0.0-preview.6
[1.0.0-preview.5]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.4...v1.0.0-preview.5
[1.0.0-preview.4]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.3...v1.0.0-preview.4
[1.0.0-preview.3]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.2...v1.0.0-preview.3
[1.0.0-preview.2]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.1...v1.0.0-preview.2
[1.0.0-preview.1]: https://github.com/illia1f/RequestFlow/releases/tag/v1.0.0-preview.1
