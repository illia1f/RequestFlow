using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reads the response type a request type declares, for the rules that hold a handler or a stage to it.
/// </summary>
internal static class RequestContracts
{
    /// <summary>
    /// The sole Task response type, or null for multiple response contracts or mixed request families.
    /// </summary>
    public static Type? GetSoleDeclaredResponse(Type requestType)
    {
        Type? declared = null;

        foreach (var iface in requestType.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();

            if (definition == typeof(IStreamRequest<>)
                || definition == typeof(IValueRequest<>))
                return null;

            if (definition != typeof(IRequest<>))
                continue;

            if (declared is not null)
                return null;

            declared = iface.GetGenericArguments()[0];
        }

        return declared;
    }

    /// <summary>
    /// Caches <see cref="GetSoleDeclaredResponse(Type)"/> per request type for one validation pass, including null results.
    /// </summary>
    public static Type? GetSoleDeclaredResponse(Type requestType, Dictionary<Type, Type?> memo)
    {
        if (memo.TryGetValue(requestType, out Type? cached))
            return cached;

        Type? declared = GetSoleDeclaredResponse(requestType);
        memo[requestType] = declared;

        return declared;
    }
}
