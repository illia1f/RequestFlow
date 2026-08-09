using System;

namespace RequestFlow;

/// <summary>
/// Checks the contract type a model entry was given against the family it belongs to.
/// </summary>
internal static class OpenContract
{
    /// <exception cref="ArgumentException"/>
    public static Type OrDefault(Type? contractType, Type fallback, Type alternate)
    {
        if (contractType is null)
            return fallback;

        if (contractType.IsInterface && contractType.IsGenericTypeDefinition &&
            (BuiltOn(contractType, fallback) || BuiltOn(contractType, alternate)))
        {
            return contractType;
        }

        throw new ArgumentException(
            $"'{contractType}' is neither {Describe(fallback)} nor {Describe(alternate)}, nor built on " +
            $"either. Pass {Describe(fallback)}, or an open generic interface deriving from it.",
            nameof(contractType));
    }

    // An open definition lists the contract closed over its own parameters, so compare definitions.
    private static bool BuiltOn(Type definition, Type contract)
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

    private static string Describe(Type definition)
    {
        string name = definition.Name;
        int tick = name.IndexOf('`');
        if (tick >= 0)
            name = name.Substring(0, tick);

        Type[] parameters = definition.GetGenericArguments();
        string[] names = new string[parameters.Length];
        for (int i = 0; i < names.Length; i++)
            names[i] = parameters[i].Name;

        return name + "<" + string.Join(", ", names) + ">";
    }
}
