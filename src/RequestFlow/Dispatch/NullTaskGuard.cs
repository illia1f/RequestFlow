using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RequestFlow;

internal static class NullTaskGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResponse> FromHandler<TResponse>(Task<TResponse> task, Type requestType)
        => task ?? throw new HandlerNullTaskException(requestType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResponse> FromStage<TResponse>(Task<TResponse> task, Type stageType)
        => task ?? throw new StageNullTaskException(stageType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task FromEventHandler(Task task, Type eventType, Type handlerType)
        => task ?? throw new EventHandlerNullTaskException(eventType, handlerType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task FromStrategy(Task task, Type strategyType)
        => task ?? throw new EventStrategyNullTaskException(strategyType);
}
