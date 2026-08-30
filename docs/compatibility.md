# Compatibility

## Target frameworks

All four RequestFlow packages target `netstandard2.0;net462;net8.0;net10.0`.

| Target | Use |
| --- | --- |
| `net10.0` | Applications targeting .NET 10 |
| `net8.0` | Applications targeting .NET 8 LTS |
| `netstandard2.0` | Libraries and applications that consume .NET Standard 2.0 |
| `net462` | Applications that require an explicit .NET Framework 4.6.2 NuGet asset |

## Package dependencies

- `RequestFlow.Abstractions` has no package dependency on `net8.0` or `net10.0`. Its two downlevel assets depend on `Microsoft.Bcl.AsyncInterfaces` 8.0.0 for `IAsyncEnumerable<T>`.
- `RequestFlow` depends on `RequestFlow.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions`.
- `RequestFlow.Cqrs.Abstractions` depends on `RequestFlow.Abstractions`.
- `RequestFlow.Cqrs` depends on `RequestFlow.Cqrs.Abstractions` and `RequestFlow`.

ASP.NET Core is not required by the packages. The runtime integrates through `IServiceCollection` and `IServiceProvider`.

## Compiler and build integration

RequestFlow performs registration and validation in the application process during startup. It ships no analyzer package, compiler plugin, or generated dispatch source.

## Trimming and NativeAOT

Trimming and NativeAOT are not supported.

The registration and freeze paths need runtime type metadata and dynamic generic construction:

- `RegisterHandlersFromCallingAssembly()` and the `RegisterHandlersFromAssembly*()` methods scan assemblies.
- `Type.MakeGenericType` closes handlers, stages, and plans.
- `Activator.CreateInstance` builds frozen plans.

`AddHandler<THandler>()` and `AddEventHandler<THandler>()` can avoid scanning an assembly for those handlers. They do not remove the dynamic construction performed during freeze, so they do not make an application trimming-safe or NativeAOT-compatible.

Do not enable trimmed or NativeAOT publishing for an application that depends on RequestFlow until the library ships an explicitly supported registration and plan-construction path.

## Runtime execution

Reflection is confined to registration and startup plan construction. Request, stream, and event hot paths use frozen maps and closed plans with no reflection, LINQ, or locking.
