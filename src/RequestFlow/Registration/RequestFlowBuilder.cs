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
    /// Registered once per rule type however many times it is called, as a singleton, and
    /// resolved from the container, so constructor dependencies work. A scoped dependency throws
    /// on a provider that validates scopes, since the rule is resolved from the root provider.
    /// Register the descriptor yourself for a transient rule.
    /// <para>
    /// A rule must not take <see cref="IRequestDispatcher"/> or a typed dispatcher. Resolving one
    /// needs the dispatch map the freeze has not finished building, and the container blocks on
    /// itself, so startup hangs with no exception. A provider that validates scopes throws first,
    /// since the dispatcher is scoped and a rule is a singleton. Handlers and stages resolve
    /// without touching the map, but a singleton rule holding one keeps that instance for the
    /// provider's lifetime.
    /// </para>
    /// </remarks>
    public RequestFlowBuilder AddValidationRule<TRule>()
        where TRule : class, IRequestFlowValidationRule
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRequestFlowValidationRule, TRule>());
        return this;
    }
}
