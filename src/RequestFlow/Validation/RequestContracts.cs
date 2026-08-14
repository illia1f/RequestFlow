using System;

namespace RequestFlow;

/// <summary>
/// Reads the response type a request type declares, for the rules that hold a handler or a stage to
/// it.
/// </summary>
internal static class RequestContracts
{
    /// <summary>
    /// The response type a sole, unambiguous <c>IRequest&lt;TResponse&gt;</c> contract names, or null
    /// when another rule owns the shape: two response contracts are <c>RF0106</c>, and a stream
    /// contract alongside one is <c>RF0109</c>.
    /// </summary>
    public static Type? SoleDeclaredResponse(Type requestType)
    {
        Type? declared = null;

        foreach (var iface in requestType.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();

            if (definition == typeof(IStreamRequest<>))
                return null;

            if (definition != typeof(IRequest<>))
                continue;

            if (declared is not null)
                return null;

            declared = iface.GetGenericArguments()[0];
        }

        return declared;
    }
}
