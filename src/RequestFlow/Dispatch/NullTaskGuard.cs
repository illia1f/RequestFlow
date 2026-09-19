using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RequestFlow;

internal static class NullTaskGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResponse> FromHandler<TResponse>(Task<TResponse> task, Type requestType)
        => task ?? ThrowHelper.HandlerNullTask<Task<TResponse>>(requestType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResponse> FromStage<TResponse>(Task<TResponse> task, Type stageType)
        => task ?? ThrowHelper.StageNullTask<Task<TResponse>>(stageType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task FromEventHandler(Task task, Type eventType, Type handlerType)
        => task ?? ThrowHelper.EventHandlerNullTask<Task>(eventType, handlerType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task FromStrategy(Task task, Type strategyType)
        => task ?? ThrowHelper.EventStrategyNullTask<Task>(strategyType);
}
