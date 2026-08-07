using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Tests.Unit;

/// <summary>
/// A provider that records every service type asked of it and forwards the ask to a real one, so a
/// test can assert what a dispatch resolved and in what order.
/// </summary>
internal sealed class CountingProvider(IServiceProvider inner) : IServiceProvider, ISupportRequiredService
{
    internal readonly List<Type> Requested = [];

    public object? GetService(Type serviceType)
    {
        Requested.Add(serviceType);

        return inner.GetService(serviceType);
    }

    public object GetRequiredService(Type serviceType)
    {
        Requested.Add(serviceType);

        return inner.GetRequiredService(serviceType);
    }
}
