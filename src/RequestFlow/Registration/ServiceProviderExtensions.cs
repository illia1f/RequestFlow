using System;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using RequestFlow;

// Same namespace convention as AddRequestFlow: visible without an extra using.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// RequestFlow startup validation and pipeline inspection.
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// Validates registration and returns the frozen pipeline for the exact request type.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="RequestFlowValidationException"/>
    /// <exception cref="HandlerNotFoundException"/>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(DeploymentWarnings.Trimming)]
    [RequiresDynamicCode(DeploymentWarnings.NativeAot)]
#endif
    public static RequestPipeline InspectRequestFlow<TRequest>(this IServiceProvider provider)
        => InspectRequestFlow(provider, typeof(TRequest));

    /// <summary>
    /// Validates registration and returns the frozen pipeline for the exact request type.
    /// </summary>
    /// <remarks>
    /// Does not resolve handlers or stages. Unknown requests and requests without handlers throw
    /// <see cref="HandlerNotFoundException"/>, including requests exempted from handler coverage.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="RequestFlowValidationException"/>
    /// <exception cref="HandlerNotFoundException"/>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(DeploymentWarnings.Trimming)]
    [RequiresDynamicCode(DeploymentWarnings.NativeAot)]
#endif
    public static RequestPipeline InspectRequestFlow(this IServiceProvider provider, Type requestType)
    {
        if (provider is null)
            throw new ArgumentNullException(nameof(provider));
        if (requestType is null)
            throw new ArgumentNullException(nameof(requestType));

        using (IServiceScope scope = provider.CreateScope())
        {
            return GetFrozenPlans(scope.ServiceProvider).GetPipeline(requestType);
        }
    }

    /// <summary>
    /// Builds and validates this provider's request and event maps now instead of at first dispatch.
    /// Returns the provider for chaining.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="RequestFlowValidationException"/>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(DeploymentWarnings.Trimming)]
    [RequiresDynamicCode(DeploymentWarnings.NativeAot)]
#endif
    public static IServiceProvider ValidateRequestFlow(this IServiceProvider provider)
    {
        if (provider is null)
            throw new ArgumentNullException(nameof(provider));

        using (IServiceScope scope = provider.CreateScope())
        {
            GetFrozenPlans(scope.ServiceProvider);
        }

        return provider;
    }

    private static FrozenPlans GetFrozenPlans(IServiceProvider provider)
    {
        // Check before resolving the singleton, whose DI lock can block a validation worker.
        provider.GetService<RequestFlowRegistry>()?.ThrowIfFreezing(provider);
        return provider.GetRequiredService<FrozenPlans>();
    }
}
