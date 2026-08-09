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
/// One snapshot asks about the same closed stage type once per handler per declaration, so the
/// caller passes a memo it owns for that snapshot. A shared static one would need a lock and would
/// outlive the freeze.
/// </para>
/// </remarks>
internal static class StageContract
{
    public static Type Of(Type stageType, Dictionary<Type, Type> memo)
    {
        if (memo.TryGetValue(stageType, out Type? cached))
            return cached;

        Type contract = Resolve(stageType);
        memo[stageType] = contract;

        return contract;
    }

    private static Type Resolve(Type stageType)
    {
        List<Type> typed = [];
        List<Type> untyped = [];
        foreach (var iface in stageType.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();
            if (Implements(definition, typeof(IRequestStage<,>)))
                typed.Add(definition);
            else if (Implements(definition, typeof(IRequestStage<>)))
                untyped.Add(definition);
        }

        if (typed.Count > 0)
            return MostDerived(typed, typeof(IRequestStage<,>));

        return untyped.Count > 0 ? MostDerived(untyped, typeof(IRequestStage<>)) : typeof(IRequestStage<,>);
    }

    private static bool Implements(Type definition, Type contract)
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
