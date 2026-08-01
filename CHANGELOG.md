# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Pre-1.0: the public API can still change between previews.

Releases are cut from this file. The `release` workflow reads the section matching the pushed tag and uses it as the GitHub Release body, so a tag with no matching section fails the build before anything reaches nuget.org. Before tagging, rename `[Unreleased]` to the version you are shipping and give it a date.

## [Unreleased]

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
