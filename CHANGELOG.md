# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Pre-1.0: the public API can still change between previews.

Releases are cut from this file. The `release` workflow reads the section matching the pushed tag and uses it as the GitHub Release body, so a tag with no matching section fails the build before anything reaches nuget.org. Before tagging, rename `[Unreleased]` to the version you are shipping and give it a date.

## [Unreleased]

### Added

- Dedicated exceptions for the three broken-contract failures that used to throw a plain `InvalidOperationException`: `HandlerNullTaskException` and `StageNullTaskException` for a null task returned from `HandleAsync`, and `OverlappingNextCallException` for a stage that calls `next` while its earlier call is still running. Each carries the type at fault in a property (`RequestType` or `StageType`) instead of only naming it in the message. The two null-task types share an abstract `NullTaskException` base, so one catch clause covers both, and all three still derive from `InvalidOperationException`.

### Changed

- A stage receives `next` as `IContinuation<TResponse>` (or `IContinuation` on the void form) instead of the `StageDelegate` delegate types, which are gone. Stage bodies call `await next.InvokeAsync()` where they called `await next()`. Nothing else about a stage changes. An interface also leaves room to add arguments to a future `InvokeAsync` overload, which a delegate signature cannot take without breaking every stage.
- A stage now resolves from the container when its level first runs rather than up front, and so does the handler. A stage that short-circuits builds neither the stages below it nor the handler behind them, which is the point for a cache stage sitting in front of a repository. A repeated `next` call still walks the instances the dispatch already resolved, the handler included.
- For a request with stages, a container failure building a stage or the handler now surfaces from inside the chain, out of the `next.InvokeAsync()` call that reached that level, where the stages wrapped around it can catch it. A retry stage with a broad `catch` will retry a missing registration. Turn on `ServiceProviderOptions.ValidateOnBuild` to keep registration mistakes at startup.

### Performance

- A dispatch through a chain of N stages allocates N objects instead of 2N+1: the per-level delegate and the per-dispatch stage array are both gone, and a stage that invokes `next` more than once no longer allocates on the repeat. Measured on net10.0 with a synchronous handler, a three-stage chain costs 192 bytes per dispatch against 416 before, and each further stage adds 56 bytes rather than 112.

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

[Unreleased]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.3...HEAD
[1.0.0-preview.3]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.2...v1.0.0-preview.3
[1.0.0-preview.2]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.1...v1.0.0-preview.2
[1.0.0-preview.1]: https://github.com/illia1f/RequestFlow/releases/tag/v1.0.0-preview.1
