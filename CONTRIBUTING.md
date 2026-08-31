# Contributing to RequestFlow

Pre-1.0: the public API can still change between previews.

## Setup

- .NET 10 SDK (`global.json` pins the major version)
- .NET 8 runtime (tests also run on `net8.0`)
- On Windows, tests also run on .NET Framework (the `net462` target), so the library's oldest build gets tested too. Nothing to install: the 4.8 runtime that ships with Windows runs them.

```
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~RequestDispatcherTests"
```

## Before you push

Builds treat warnings as errors in `src/`. Test projects turn warnings-as-errors off.

CI builds, tests, and packs on Windows. The release workflow publishes from Ubuntu. Tests target net462 only on Windows, so the build job needs a Windows runner.

CI sets `CI=true`, which turns on `ContinuousIntegrationBuild`: debug symbols then record repository paths instead of local machine paths. Locally you only need it to reproduce a packaging problem:

```
dotnet build -c Release -p:CI=true
```

## Rules

- No code from MediatR (commercially licensed since 2025). Shared vocabulary is fine, shared code is not. Use MIT or Apache licensed references.
- No reflection, LINQ, or locking on the dispatch path. Reflection is fine at registration and startup validation.
- Target frameworks and dependencies are fixed. All four `src/` projects target `netstandard2.0;net462;net8.0;net10.0`.

| Project                         | Depends on                                                                                      |
| ------------------------------- | ----------------------------------------------------------------------------------------------- |
| `RequestFlow.Abstractions`      | nothing on `net8.0`/`net10.0`; `Microsoft.Bcl.AsyncInterfaces` on `netstandard2.0` and `net462` |
| `RequestFlow.Cqrs.Abstractions` | `RequestFlow.Abstractions`                                                                      |
| `RequestFlow`                   | `RequestFlow.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`             |
| `RequestFlow.Cqrs`              | `RequestFlow.Cqrs.Abstractions`, `RequestFlow`                                                  |

## Tests

xUnit, NSubstitute, Shouldly (not FluentAssertions, whose license changed). Names follow `Given_When_Then`:

```csharp
[Fact]
public async Task Given_Null_Request_When_Sending_Request_Then_Throws_Argument_Null_Exception()
```

Fixtures go in a region at the bottom of the file. When a test checks DI registration itself, use a real `ServiceCollection` instead of mocks.

## Commits

Conventional Commits:

```
feat(cqrs): add CQRS contracts, dispatchers, AddCqrs
test: expand request dispatcher coverage
```

## Where to read next

- [docs/getting-started.md](docs/getting-started.md)
- [docs/registration.md](docs/registration.md)
- [docs/lifetimes.md](docs/lifetimes.md)
- [docs/exceptions.md](docs/exceptions.md)
- [ROADMAP.md](ROADMAP.md) for what is planned and what is out of scope

Open an issue before starting anything large.
