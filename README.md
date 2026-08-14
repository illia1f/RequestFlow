# ![R](https://raw.githubusercontent.com/illia1f/RequestFlow/main/assets/RequestFlowIcon-89x52.png)equestFlow

A small, fast request/handler library for .NET. You define a request and its handler, register them with one call, and dispatch through a single interface. All the wiring happens at runtime, once at startup, with no compiler plugin and no build-time code generation: if a project can reference a NuGet package, it can run RequestFlow.

The core library stays unopinionated about how you name your requests. If you want a type-level split between commands and queries for CQRS- and DDD-style apps, install `RequestFlow.Cqrs` instead; it already contains the core package.

[![NuGet](https://img.shields.io/nuget/vpre/RequestFlow?label=nuget)](https://www.nuget.org/packages/RequestFlow)
[![Downloads](https://img.shields.io/nuget/dt/RequestFlow?label=downloads)](https://www.nuget.org/packages/RequestFlow)
[![CI](https://github.com/illia1f/RequestFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/illia1f/RequestFlow/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/illia1f/RequestFlow/blob/main/LICENSE)
![Status](https://img.shields.io/badge/status-preview-orange)
![Targets](https://img.shields.io/badge/targets-netstandard2.0%20%7C%20net462%20%7C%20net8.0%20%7C%20net10.0-512BD4)

> **Status:** [preview on NuGet](https://www.nuget.org/packages/RequestFlow). Install with the `--prerelease` flag:
>
> ```
> dotnet add package RequestFlow --prerelease
> ```

## Why

[MediatR](https://github.com/LuckyPennySoftware/MediatR) went commercial in 2025, and the search for a replacement now turns up a crowded field of free mediators. Many of the fastest are built on source generators: compiler plugins that write the dispatch code during your build. That buys speed and compile-time checks. It also ties the library to your toolchain: a recent compiler, `PackageReference`, analyzers left on, and generated code in every build.

RequestFlow trades those requirements away and keeps everything at runtime.

- Errors surface at startup, not in production. Discovery, validation, and the dispatch plan all finish before the first request, and a broken configuration fails the boot with one exception listing every problem. After that, dispatch is one dictionary lookup with no reflection, LINQ, or locking.
- No build step. Nothing runs inside your compiler, and there is no generated code to step through when something misbehaves. One package behaves the same from .NET 10 down to .NET Framework 4.6.2.
- MIT, permanently. This library exists because a license changed underneath its users once. It takes no dependency whose license could do the same.
- Migration is mostly renames. Requests and handlers keep their shape coming from MediatR; the mapping table below covers a typical codebase.

Fast is a claim to prove, not to assert. A BenchmarkDotNet suite against the other mediators, raw artifacts included, is on the [roadmap](ROADMAP.md) before v1. Until it lands, this README quotes no numbers.

## Coming from MediatR

| MediatR                                              | RequestFlow                                           |
| ---------------------------------------------------- | ----------------------------------------------------- |
| `IRequest<TResponse>`, `IRequest`                    | same names, `RequestFlow` namespace                   |
| `IRequestHandler<TRequest, TResponse>` with `Handle` | same interface, method is `HandleAsync`               |
| `IMediator.Send(...)`                                | `IRequestDispatcher.SendAsync(...)`                   |
| void requests through `Unit`                         | void handlers return plain `Task`, no `Unit` anywhere |
| `IPipelineBehavior<,>`                               | `IRequestStage<,>`                                    |
| `IStreamRequest<TResponse>`                          | `IStreamRequest<TItem>`, `RequestFlow` namespace      |
| `IStreamRequestHandler<,>` with `Handle`             | same interface and same method name                   |
| `IMediator.CreateStream(...)`                        | `IStreamDispatcher.Stream(...)`                       |
| `services.AddMediatR(...)`                           | `services.AddRequestFlow(...)`                        |

What doesn't move yet: notifications (`INotification` / `Publish`). They are on the [roadmap](ROADMAP.md) for v1.0 and return as events, an in-process publish/subscribe (`IEvent`, `IEventHandler`, `IEventPublisher`). Nothing is built there today, so if your codebase leans on notifications, hold the migration until they land.

## Packages

- **[`RequestFlow.Abstractions`](https://www.nuget.org/packages/RequestFlow.Abstractions)** holds the contracts: `IRequest`, `IRequestHandler`, `IRequestDispatcher`, `IRequestStage`, `NoResult`, and the streaming set: `IStreamRequest`, `IStreamDispatcher`, `IStreamRequestHandler`, `IStreamRequestStage`. Depends on nothing on `net8.0` and `net10.0`; on `netstandard2.0` and `net462` it carries one Microsoft package, `Microsoft.Bcl.AsyncInterfaces`, which supplies `IAsyncEnumerable<T>` there.
- **[`RequestFlow`](https://www.nuget.org/packages/RequestFlow)** is the runtime: both dispatchers, `AddRequestFlow` with assembly scanning, startup validation. Depends on `RequestFlow.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions`.
- **[`RequestFlow.Cqrs.Abstractions`](https://www.nuget.org/packages/RequestFlow.Cqrs.Abstractions)** holds the CQRS contracts: `ICommand`, `IQuery`, their handler interfaces, `ICommandDispatcher`, `IQueryDispatcher`. Depends on `RequestFlow.Abstractions` only.
- **[`RequestFlow.Cqrs`](https://www.nuget.org/packages/RequestFlow.Cqrs)** is the CQRS runtime: typed dispatcher implementations, registered with `AddRequestFlow(...).AddCqrs()`. Depends on the contracts package and the core runtime.

Contracts live in their own packages so your domain layer, and any future add-on package, can reference the interfaces without taking a dependency on a runtime. Install a runtime package at the composition root and the matching contracts arrive transitively. Core types share the `RequestFlow` namespace; the CQRS types live in `RequestFlow.Cqrs`.

## Documentation

- [Getting started](https://github.com/illia1f/RequestFlow/blob/main/docs/getting-started.md): install, first request and handler, dispatching
- [Registration](https://github.com/illia1f/RequestFlow/blob/main/docs/registration.md): every `AddRequestFlow` option, scanning, generic handlers, startup validation
- [Stages](https://github.com/illia1f/RequestFlow/blob/main/docs/stages.md): wrapping handlers, execution order, which requests a stage reaches, filters
- [Streaming](https://github.com/illia1f/RequestFlow/blob/main/docs/streaming.md): stream requests over `IAsyncEnumerable`, stream stages, cancellation, and which package a stream handler needs
- [Service lifetimes](https://github.com/illia1f/RequestFlow/blob/main/docs/lifetimes.md): what RequestFlow registers, with which lifetime, and what you can change
- [Exceptions](https://github.com/illia1f/RequestFlow/blob/main/docs/exceptions.md): every exception RequestFlow throws, when it surfaces, and how to fix it
- [Validation rules](https://github.com/illia1f/RequestFlow/blob/main/docs/validation-rules.md): contributing custom checks to startup validation, the model rules see, built-in problem codes
- [Sample](https://github.com/illia1f/RequestFlow/blob/main/samples/README.md): a minimal API using commands, queries, stages, and two validation rules of its own

## Contributing

Design feedback is the most useful contribution right now.

## License

MIT. See [LICENSE](https://github.com/illia1f/RequestFlow/blob/main/LICENSE).
