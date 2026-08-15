using System;

namespace RequestFlow;

/// <summary>
/// Checks the shape of the contract type a model entry was given.
/// </summary>
internal static class OpenContract
{
    /// <exception cref="ArgumentException"/>
    public static Type OrDefault(Type? contractType, Type fallback, Type alternate)
    {
        if (contractType is null)
            return fallback;

        if (contractType.IsInterface && contractType.IsGenericTypeDefinition)
            return contractType;

        throw new ArgumentException(
            $"'{contractType}' is not an open generic interface. Pass {Describe(fallback)}, " +
            $"{Describe(alternate)}, or another open generic contract interface.",
            nameof(contractType));
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
