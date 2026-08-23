using System;
using RequestFlow;

// Same namespace convention as AddRequestFlow: visible without an extra using.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// RequestFlow startup validation entry point.
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// Builds and validates this provider's request and event maps now instead of at first dispatch.
    /// Returns the provider for chaining.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="RequestFlowValidationException"/>
    public static IServiceProvider ValidateRequestFlow(this IServiceProvider provider)
    {
        if (provider is null)
            throw new ArgumentNullException(nameof(provider));

        using (IServiceScope scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<FrozenPlans>();
        }

        return provider;
    }
}
