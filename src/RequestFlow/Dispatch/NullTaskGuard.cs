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
}
