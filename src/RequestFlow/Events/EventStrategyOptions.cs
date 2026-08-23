using System;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Configures the lifetime of one custom event publish strategy.
/// </summary>
public sealed class EventStrategyOptions
{
    private readonly Type _strategyType;
    private ServiceLifetime? _declaredLifetime;

    internal EventStrategyOptions(Type strategyType)
        => _strategyType = strategyType ?? throw new ArgumentNullException(nameof(strategyType));

    internal ServiceLifetime Lifetime => _declaredLifetime ?? ServiceLifetime.Singleton;

    /// <summary>
    /// Registers the strategy as a singleton, which is the default.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public EventStrategyOptions AsSingleton()
        => WithLifetime(ServiceLifetime.Singleton);

    /// <summary>
    /// Registers the strategy once per service scope.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public EventStrategyOptions AsScoped()
        => WithLifetime(ServiceLifetime.Scoped);

    /// <summary>
    /// Registers a new strategy instance for every publish.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public EventStrategyOptions AsTransient()
        => WithLifetime(ServiceLifetime.Transient);

    private EventStrategyOptions WithLifetime(ServiceLifetime lifetime)
    {
        if (EventStrategyTypes.IsBuiltIn(_strategyType))
        {
            throw new InvalidOperationException(
                $"Built-in event publish strategy '{_strategyType.FullName}' is selected directly " +
                "and is not resolved from the container, so a built-in strategy cannot have a lifetime.");
        }

        if (_declaredLifetime is { } declared && declared != lifetime)
        {
            throw new InvalidOperationException(
                $"This event publish strategy is already declared {declared}. A strategy takes one " +
                $"lifetime; remove either the {declared} or {lifetime} call.");
        }

        _declaredLifetime = lifetime;
        return this;
    }
}
