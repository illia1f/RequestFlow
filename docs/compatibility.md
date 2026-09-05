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

- `RequestFlow.Abstractions` has no package dependencies on `net8.0` or `net10.0`. On `netstandard2.0` and `net462`, it depends on `Microsoft.Bcl.AsyncInterfaces` 8.0.0 for streaming contracts, which brings `System.Threading.Tasks.Extensions` 4.5.4 for `ValueTask` transitively.
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

`AddHandler<THandler>()` and `AddEventHandler<THandler>()` avoid assembly scanning, but freeze still uses dynamic construction. Manual registration does not enable trimming or NativeAOT.

## Runtime execution

Reflection runs only during registration and startup. Dispatch and event publication use frozen maps with no reflection, LINQ, or locking.
