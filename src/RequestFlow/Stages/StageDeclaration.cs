using System;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// One registered stage: the stage type, the optional handler contract that narrows which
/// requests it reaches, and its lifetime. Position in the registry's list is execution order.
/// </summary>
internal sealed class StageDeclaration(
    Type stageType, Type? handlerFilter, ServiceLifetime lifetime = ServiceLifetime.Transient)
{
    public Type StageType { get; } = stageType;

    public Type? HandlerFilter { get; } = handlerFilter;

    public ServiceLifetime Lifetime { get; } = lifetime;
}
