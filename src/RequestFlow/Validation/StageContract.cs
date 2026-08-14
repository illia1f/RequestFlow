using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports which stage contract a stage type implements.
/// </summary>
/// <remarks>
/// The typed contract wins when a type implements both, matching the order
/// <see cref="StageClosing"/> tests them in, so the model names the contract dispatch would use.
/// A package builds its own contract on top of a core one, so the most derived contract wins;
/// when two apply and neither derives from the other, the core contract is recorded instead of
/// picking one. Declarations that implement neither never reach here: registration drops them
/// before the registry records them.
/// <para>
/// One snapshot asks about the same stage type once per handler per declaration, so the caller
/// passes a memo it owns for that snapshot. <see cref="Of"/> takes one per family, since a type
/// implementing a contract from each would otherwise get one family's answer for both.
/// <see cref="OfClosing"/> keys its memo by the closed core contract as well, since one stage type
/// can satisfy a different contract per request. A shared static memo would need a lock and would outlive the freeze.
/// </para>
/// </remarks>
internal static class StageContract
{
    public static Type Of(Type stageType, StageFamily family, Dictionary<Type, Type> memo)
    {
        if (memo.TryGetValue(stageType, out Type? cached))
            return cached;

        Type contract = Resolve(stageType, family);
        memo[stageType] = contract;

        return contract;
    }

    /// <summary>
    /// The contract one closing satisfies for its request and response pair, ignoring contracts
    /// the stage implements only for other requests.
    /// </summary>
    // A nested dictionary rather than a tuple key, since net462 has no ValueTuple without a
    // package the project must not take.
    public static Type OfClosing(
        Type closedStageType,
        StageFamily family,
        Type requestType,
        Type responseType,
        bool isVoid,
        Dictionary<Type, Dictionary<Type, Type>> memo)
    {
        Type typedCore = family.TypedContract.MakeGenericType(requestType, responseType);

        if (!memo.TryGetValue(closedStageType, out Dictionary<Type, Type>? perCore))
        {
            perCore = [];
            memo[closedStageType] = perCore;
        }

        if (perCore.TryGetValue(typedCore, out Type? cached))
            return cached;

        Type? voidCore = isVoid && family.VoidContract is not null
            ? family.VoidContract.MakeGenericType(requestType)
            : null;

        Type contract = ResolveClosing(closedStageType, family, typedCore, voidCore);
        perCore[typedCore] = contract;

        return contract;
    }

    // The closed cores honor the in TRequest variance, so a stage written against a base request
    // still reports the contract that admits this one.
    private static Type ResolveClosing(Type closedStageType, StageFamily family, Type typedCore, Type? voidCore)
    {
        List<Type> typed = [];
        List<Type> untyped = [];
        foreach (var iface in closedStageType.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            if (typedCore.IsAssignableFrom(iface))
                typed.Add(iface.GetGenericTypeDefinition());
            else if (voidCore is not null && voidCore.IsAssignableFrom(iface))
                untyped.Add(iface.GetGenericTypeDefinition());
        }

        if (typed.Count > 0)
            return MostDerived(typed, family.TypedContract);

        return untyped.Count > 0 && family.VoidContract is not null
            ? MostDerived(untyped, family.VoidContract)
            : family.TypedContract;
    }

    private static Type Resolve(Type stageType, StageFamily family)
    {
        List<Type> typed = [];
        List<Type> untyped = [];
        foreach (var iface in stageType.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();
            if (Implements(definition, family.TypedContract))
                typed.Add(definition);
            else if (family.VoidContract is not null && Implements(definition, family.VoidContract))
                untyped.Add(definition);
        }

        if (typed.Count > 0)
            return MostDerived(typed, family.TypedContract);

        return untyped.Count > 0 && family.VoidContract is not null
            ? MostDerived(untyped, family.VoidContract)
            : family.TypedContract;
    }

    internal static bool Implements(Type definition, Type contract)
    {
        if (definition == contract)
            return true;

        foreach (var iface in definition.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == contract)
                return true;
        }

        return false;
    }

    private static Type MostDerived(List<Type> definitions, Type coreContract)
    {
        foreach (var definition in definitions)
        {
            if (definition != coreContract && DerivesFromAll(definition, definitions))
                return definition;
        }

        return coreContract;
    }

    private static bool DerivesFromAll(Type definition, List<Type> others)
    {
        foreach (var other in others)
        {
            if (other != definition && !Implements(definition, other))
                return false;
        }

        return true;
    }
}
