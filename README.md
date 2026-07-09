# RequestFlow

A small, fast request/handler and pipeline library for .NET. You define a request and its handler, then move cross-cutting concerns like validation, logging, authorization, and performance monitoring into composable stages instead of scattering them through the handler. A request flows through its stages, then into the handler, and the response flows back out.

The core library stays unopinionated about how you name your requests. If you want a type-level split between commands and queries for CQRS- and DDD-style apps, add the `RequestFlow.Cqrs` package.

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Status](https://img.shields.io/badge/status-in%20design-orange)
![Targets](https://img.shields.io/badge/targets-netstandard2.0%20%7C%20net8.0%20%7C%20net10.0-512BD4)

> **Status:** in development, nothing on NuGet yet.

## Why

[MediatR](https://github.com/LuckyPennySoftware/MediatR) went commercial in 2025, and it had been the default for this kind of work for years. The free options that remain fall into two camps. Some are general-purpose mediators that treat every message the same, with no distinction between a command and a query. Others are older and narrower, like Microsoft's [CQRS.Mediatr.Lite](https://github.com/microsoft/CQRS.Mediatr.Lite), which last shipped in 2021 and solved one team's problem before going quiet.

None of them pair low-allocation dispatch with a command/query split the type system enforces. RequestFlow aims at that gap:

1. No reflection on the hot path; handler lookup is cached.
2. Stage pipeline composed once, no LINQ in dispatch.
3. CQRS as an opt-in package, not a convention.

## Planned packages

| Package            | What it gives you                                                               |
| ------------------ | ------------------------------------------------------------------------------- |
| `RequestFlow`      | `IRequest`, `IRequestHandler`, `IRequestStage`, `IRequestDispatcher`, DI registration |
| `RequestFlow.Cqrs` | `ICommand`, `IQuery`, CQRS handler contracts, typed dispatchers                 |

## Contributing

Design feedback is the most useful contribution right now.

## License

MIT. See [LICENSE](LICENSE).
