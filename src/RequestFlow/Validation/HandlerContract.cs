using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports which handler contract a handler implements for one request.
/// </summary>
/// <remarks>
/// A package builds its own contract on top of the core one, as <c>ICommandHandler</c> does, so
/// the most derived contract over the request wins. When two contracts apply and neither derives
/// from the other, the core contract is recorded instead of picking one.
/// </remarks>
internal static class HandlerContract
{
    public static Type Of(Type handlerType, Type closedCoreContract)
    {
        List<Type> candidates = [];
        foreach (var iface in handlerType.GetInterfaces())
        {
            if (iface.IsGenericType && iface != closedCoreContract && closedCoreContract.IsAssignableFrom(iface))
                candidates.Add(iface);
        }

        foreach (var candidate in candidates)
        {
            if (DerivesFromAll(candidate, candidates))
                return candidate.GetGenericTypeDefinition();
        }

        return closedCoreContract.GetGenericTypeDefinition();
    }

    private static bool DerivesFromAll(Type candidate, List<Type> others)
    {
        foreach (var other in others)
        {
            if (other != candidate && !other.IsAssignableFrom(candidate))
                return false;
        }

        return true;
    }
}
