using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Benchmarks.Core;

/// <summary>
/// One library in a paired scope check: its label, provider build, and the registrations its report prints.
/// </summary>
/// <param name="ReportedServices">Service types the report prints besides the dispatcher.</param>
/// <param name="ReportedContracts">Open generic interfaces whose implementations the report prints.</param>
public sealed record ComparedLibrary(
    string Name,
    Func<IServiceCollection, ServiceProvider> Build,
    IReadOnlyCollection<Type>? ReportedServices = null,
    IReadOnlyCollection<Type>? ReportedContracts = null);
