using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reads the response type a ValueTask request declares for handler and stage validation.
/// </summary>
internal static class ValueRequestContracts
{
    /// <summary>
    /// Returns the response from one unambiguous <c>IValueRequest&lt;TResponse&gt;</c> contract.
    /// Another request family or more than one ValueTask contract returns null.
    /// </summary>
    public static Type? GetSoleDeclaredResponse(Type requestType)
    {
        Type? declared = null;

        if (requestType.IsGenericType)
        {
            Type definition = requestType.GetGenericTypeDefinition();
            if (definition == typeof(IRequest<>) || definition == typeof(IStreamRequest<>))
                return null;

            if (definition == typeof(IValueRequest<>))
                declared = requestType.GetGenericArguments()[0];
        }

        foreach (Type iface in requestType.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(IRequest<>) || definition == typeof(IStreamRequest<>))
                return null;

            if (definition != typeof(IValueRequest<>))
                continue;

            if (declared is not null)
                return null;

            declared = iface.GetGenericArguments()[0];
        }

        return declared;
    }

    /// <summary>
    /// Returns <see cref="GetSoleDeclaredResponse(Type)"/> with one pass's answer cached per request.
    /// </summary>
    public static Type? GetSoleDeclaredResponse(
        Type requestType,
        Dictionary<Type, Type?> memo)
    {
        if (memo.TryGetValue(requestType, out Type? cached))
            return cached;

        Type? declared = GetSoleDeclaredResponse(requestType);
        memo[requestType] = declared;

        return declared;
    }
}
