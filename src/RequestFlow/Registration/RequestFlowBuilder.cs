using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

    /// <summary>
    /// Adds a validation rule to the startup pass.
    /// </summary>
    /// <remarks>
    /// Registered once per rule type as a singleton resolved from the root provider.
    /// Register your own descriptor for a transient rule.
    /// Do not inject dispatchers or <see cref="IEventPublisher"/> into a rule: their resolution waits for validation and hangs startup.
    /// Scope validation rejects scoped dependencies first.
    /// A rule retains any injected handler or stage for the provider's lifetime.
    /// </remarks>
    public RequestFlowBuilder AddValidationRule<TRule>()
        where TRule : class, IRequestFlowValidationRule
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRequestFlowValidationRule, TRule>());
        return this;
    }
}
