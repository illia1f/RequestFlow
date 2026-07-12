using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Chains optional RequestFlow feature registrations after <c>AddRequestFlow</c>.
/// </summary>
public sealed class RequestFlowBuilder
{
    internal RequestFlowBuilder(IServiceCollection services)
        => Services = services;

    /// <summary>
    /// The service collection RequestFlow is registered on.
    /// </summary>
    public IServiceCollection Services { get; }
}
