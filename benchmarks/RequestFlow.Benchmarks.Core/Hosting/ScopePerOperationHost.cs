using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Benchmarks.Core;

/// <summary>
/// A provider whose benchmark methods create and dispose a scope per operation.
/// </summary>
public sealed class ScopePerOperationHost(ServiceProvider provider) : IDisposable
{
    public readonly ServiceProvider Provider = provider;

    public void Dispose() => Provider.Dispose();
}
