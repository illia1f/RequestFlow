# RequestFlow

A small, fast request/handler and pipeline library for .NET. You define a request and its handler, then move cross-cutting concerns like validation, logging, authorization, and performance monitoring into composable stages instead of scattering them through the handler. A request flows through its stages, then into the handler, and the response flows back out.

The core library stays unopinionated about how you name your requests. If you want a type-level split between commands and queries for CQRS- and DDD-style apps, add the `RequestFlow.Cqrs` package.

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Status](https://img.shields.io/badge/status-in%20development-orange)
![Targets](https://img.shields.io/badge/targets-netstandard2.0%20%7C%20net8.0%20%7C%20net10.0-512BD4)

> **Status:** in development, nothing on NuGet yet.

## Why

[MediatR](https://github.com/LuckyPennySoftware/MediatR) went commercial in 2025, and it had been the default for this kind of work for years. The free options that remain fall into two camps. Some are general-purpose mediators that treat every message the same, with no distinction between a command and a query. Others are older and narrower, like Microsoft's [CQRS.Mediatr.Lite](https://github.com/microsoft/CQRS.Mediatr.Lite), which last shipped in 2021 and solved one team's problem before going quiet.

None of them pair low-allocation dispatch with a command/query split the type system enforces. RequestFlow aims at that gap:

1. No reflection on the hot path; handler lookup is cached.
2. Stage pipeline composed once, no LINQ in dispatch.
3. CQRS as an opt-in package, not a convention.

## Planned packages

- **`RequestFlow.Abstractions`** holds the contracts: `IRequest`, `IRequestHandler`, `IRequestStage`, `IRequestDispatcher`, `NoResult`. Depends on nothing.
- **`RequestFlow`** is the runtime: dispatcher, stage pipeline, `AddRequestFlow` with assembly scanning. Depends on Abstractions and `Microsoft.Extensions.DependencyInjection.Abstractions`.
- **`RequestFlow.Cqrs`** adds `ICommand`, `IQuery`, CQRS handler contracts, typed dispatchers. Same dependencies as the runtime.

Contracts live in their own package so your domain layer, and any future add-on package, can reference the interfaces without taking a dependency on the runtime. Everything shares the `RequestFlow` namespace.

## Contributing

Design feedback is the most useful contribution right now.

## License

MIT. See [LICENSE](LICENSE).
