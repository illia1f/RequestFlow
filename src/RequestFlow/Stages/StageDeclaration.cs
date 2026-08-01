using System;

namespace RequestFlow;

/// <summary>
/// One registered stage: the stage type, plus the optional handler contract that narrows
/// which requests it reaches. Position in the registry's list is execution order.
/// </summary>
internal sealed class StageDeclaration(Type stageType, Type? handlerFilter)
{
    public Type StageType { get; } = stageType;

    public Type? HandlerFilter { get; } = handlerFilter;
}
