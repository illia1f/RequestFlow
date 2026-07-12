# RequestFlow

A small, fast request/handler library for .NET. You define a request and its handler, register them with one call, and dispatch through a single interface. Handler lookup is validated at startup and served from a frozen map, so nothing on the dispatch path uses reflection.

Composable stages for cross-cutting concerns (validation, logging, authorization) are the next planned piece: a request will flow through its stages, then into the handler, and the response back out. They are not in the current preview.

The core library stays unopinionated about how you name your requests. If you want a type-level split between commands and queries for CQRS- and DDD-style apps, install `RequestFlow.Cqrs` instead; it already contains the core package.

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/illia1f/RequestFlow/blob/main/LICENSE)
![Status](https://img.shields.io/badge/status-preview-orange)
![Targets](https://img.shields.io/badge/targets-netstandard2.0%20%7C%20net8.0%20%7C%20net10.0-512BD4)

> **Status:** preview on NuGet. The request/handler core, startup validation, and the CQRS package are live; stages are still in development. Install with the `--prerelease` flag:
>
> ```
> dotnet add package RequestFlow --prerelease
> ```

## Why

[MediatR](https://github.com/LuckyPennySoftware/MediatR) went commercial in 2025, and it had been the default for this kind of work for years. The free options that remain fall into two camps. Some are general-purpose mediators that treat every message the same, with no distinction between a command and a query. Others are older and narrower, like Microsoft's [CQRS.Mediatr.Lite](https://github.com/microsoft/CQRS.Mediatr.Lite), which last shipped in 2021 and solved one team's problem before going quiet.

None of them pair low-allocation dispatch with a command/query split the type system enforces. RequestFlow aims at that gap:

1. No reflection on the hot path; handler lookup is cached.
2. CQRS as an opt-in package, not a convention.
3. Stage pipeline composed once, no LINQ in dispatch (planned, not in the current preview).

## Packages

- **`RequestFlow.Abstractions`** holds the contracts: `IRequest`, `IRequestHandler`, `IRequestDispatcher`, `NoResult`. Depends on nothing. `IRequestStage` joins this package when stages ship.
- **`RequestFlow`** is the runtime: dispatcher, `AddRequestFlow` with assembly scanning, startup validation. Depends on Abstractions and `Microsoft.Extensions.DependencyInjection.Abstractions`.
- **`RequestFlow.Cqrs.Abstractions`** holds the CQRS contracts: `ICommand`, `IQuery`, their handler interfaces, `ICommandDispatcher`, `IQueryDispatcher`. Depends on `RequestFlow.Abstractions` only.
- **`RequestFlow.Cqrs`** is the CQRS runtime: typed dispatcher implementations, registered with `AddRequestFlow(...).AddCqrs()`. Depends on the contracts package and the core runtime.

Contracts live in their own packages so your domain layer, and any future add-on package, can reference the interfaces without taking a dependency on a runtime. Install a runtime package at the composition root and the matching contracts arrive transitively. Core types share the `RequestFlow` namespace; the CQRS types live in `RequestFlow.Cqrs`.

## Documentation

- [Getting started](https://github.com/illia1f/RequestFlow/blob/main/docs/getting-started.md): install, first request and handler, dispatching
- [Registration](https://github.com/illia1f/RequestFlow/blob/main/docs/registration.md): every `AddRequestFlow` option, scanning, generic handlers, startup validation
- [Service lifetimes](https://github.com/illia1f/RequestFlow/blob/main/docs/lifetimes.md): what RequestFlow registers, with which lifetime, and what you can change
- [Exceptions](https://github.com/illia1f/RequestFlow/blob/main/docs/exceptions.md): every exception RequestFlow throws, when it surfaces, and how to fix it

## Contributing

Design feedback is the most useful contribution right now.

## License

MIT. See [LICENSE](https://github.com/illia1f/RequestFlow/blob/main/LICENSE).
