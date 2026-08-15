using System;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// One stage's settings: which requests it reaches, and the lifetime its closed types are registered with.
/// </summary>
public sealed class StageOptions
{
    private ServiceLifetime? _declaredLifetime;

    internal Type? HandlerFilter { get; private set; }

    internal ServiceLifetime Lifetime => _declaredLifetime ?? ServiceLifetime.Transient;

    /// <summary>
    /// Limits the stage to requests whose handler implements <typeparamref name="TContract"/>.
    /// One filter per stage: a second call throws rather than replace the first.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public StageOptions WhereHandlerImplements<TContract>()
    {
        if (HandlerFilter is not null)
        {
            throw new InvalidOperationException(
                $"This stage already filters on '{HandlerFilter.FullName}'. A stage takes one handler filter; " +
                "to reach handlers of several contracts, give them one shared contract to implement.");
        }

        HandlerFilter = typeof(TContract);
        return this;
    }

    /// <summary>
    /// Registers this stage as a singleton instead of the default transient. One lifetime per
    /// stage: a call naming a different one throws rather than replace it.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public StageOptions AsSingleton()
        => WithLifetime(ServiceLifetime.Singleton);

    /// <summary>
    /// Registers this stage as scoped instead of the default transient. One lifetime per
    /// stage: a call naming a different one throws rather than replace it.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public StageOptions AsScoped()
        => WithLifetime(ServiceLifetime.Scoped);

    // Repeating a lifetime says nothing new, so only a call that disagrees is a mistake.
    private StageOptions WithLifetime(ServiceLifetime lifetime)
    {
        if (_declaredLifetime is { } declared && declared != lifetime)
        {
            throw new InvalidOperationException(
                $"This stage is already declared {declared}. A stage takes one lifetime; remove one of the two calls.");
        }

        _declaredLifetime = lifetime;
        return this;
    }
}
