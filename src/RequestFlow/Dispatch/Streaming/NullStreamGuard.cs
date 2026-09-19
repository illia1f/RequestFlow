using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RequestFlow;

internal static class NullStreamGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<TItem> FromHandler<TItem>(IAsyncEnumerable<TItem> stream, Type requestType)
        => stream ?? ThrowHelper.HandlerNullStream<IAsyncEnumerable<TItem>>(requestType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<TItem> FromStage<TItem>(IAsyncEnumerable<TItem> stream, Type stageType)
        => stream ?? ThrowHelper.StageNullStream<IAsyncEnumerable<TItem>>(stageType);
}
