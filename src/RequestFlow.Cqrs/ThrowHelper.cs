using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RequestFlow.Cqrs;

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
}
