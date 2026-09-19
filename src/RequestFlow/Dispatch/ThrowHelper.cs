using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RequestFlow;

internal static class ThrowHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull(
        [NotNull] object? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
            ArgumentNull(paramName);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ArgumentNull(string? paramName) => throw new ArgumentNullException(paramName);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TResult HandlerNotFound<TResult>(Type requestType)
        => throw new HandlerNotFoundException(requestType);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TResult ResponseTypeMismatch<TResult>(Type requestType, Type expected, Type actual)
        => throw new ResponseTypeMismatchException(requestType, expected, actual);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TResult EventNotRegistered<TResult>(Type eventType)
        => throw new EventNotRegisteredException(eventType);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TResult HandlerNullTask<TResult>(Type requestType)
        => throw new HandlerNullTaskException(requestType);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TResult StageNullTask<TResult>(Type stageType)
        => throw new StageNullTaskException(stageType);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TResult EventHandlerNullTask<TResult>(Type eventType, Type handlerType)
        => throw new EventHandlerNullTaskException(eventType, handlerType);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TResult EventStrategyNullTask<TResult>(Type strategyType)
        => throw new EventStrategyNullTaskException(strategyType);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TResult HandlerNullStream<TResult>(Type requestType)
        => throw new HandlerNullStreamException(requestType);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TResult StageNullStream<TResult>(Type stageType)
        => throw new StageNullStreamException(stageType);
}
