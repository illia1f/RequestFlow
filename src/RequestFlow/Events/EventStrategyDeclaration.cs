using System;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

internal sealed class EventStrategyDeclaration(
    Type? declaredEventType,
    Type strategyType,
    ServiceLifetime lifetime)
{
    public Type? DeclaredEventType { get; } = declaredEventType;

    public Type StrategyType { get; } = strategyType
        ?? throw new ArgumentNullException(nameof(strategyType));

    public ServiceLifetime Lifetime { get; } = lifetime;
}

internal static class EventStrategyTypes
{
    public static bool IsBuiltIn(Type strategyType)
        => strategyType == typeof(SequentialPublishStrategy)
            || strategyType == typeof(ParallelPublishStrategy)
            || strategyType == typeof(FailFastPublishStrategy);
}
