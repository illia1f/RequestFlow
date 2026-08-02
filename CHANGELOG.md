# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Pre-1.0: the public API can still change between previews.

Releases are cut from this file. The `release` workflow reads the section matching the pushed tag and uses it as the GitHub Release body, so a tag with no matching section fails the build before anything reaches nuget.org. Before tagging, rename `[Unreleased]` to the version you are shipping and give it a date.

## [Unreleased]

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

[Unreleased]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.4...HEAD
[1.0.0-preview.4]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.3...v1.0.0-preview.4
[1.0.0-preview.3]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.2...v1.0.0-preview.3
[1.0.0-preview.2]: https://github.com/illia1f/RequestFlow/compare/v1.0.0-preview.1...v1.0.0-preview.2
[1.0.0-preview.1]: https://github.com/illia1f/RequestFlow/releases/tag/v1.0.0-preview.1
