using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Rejects a null task returned by a handler with a <see cref="HandlerNullTaskException"/>
/// that names the request type.
/// </summary>
internal static class NullTaskGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResponse> ThrowIfNull<TResponse>(Task<TResponse> task, Type requestType)
    {
        if (task is null)
            throw new HandlerNullTaskException(requestType);

        return task;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task ThrowIfNull(Task task, Type requestType)
    {
        if (task is null)
            throw new HandlerNullTaskException(requestType);

        return task;
    }
}
