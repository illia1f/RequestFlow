using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Benchmarks.Core;

/// <summary>
/// A provider with one retained scope and the service resolved from it.
/// </summary>
public sealed class RetainedScopeHost<TService> : IDisposable
    where TService : notnull
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    /// <summary>
    /// The service resolved from the retained scope.
    /// </summary>
    public readonly TService Service;

    public RetainedScopeHost(ServiceProvider provider)
    {
        _provider = provider;
        _scope = provider.CreateScope();
        Service = _scope.ServiceProvider.GetRequiredService<TService>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
