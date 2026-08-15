using System;
using System.Collections.Generic;
using System.Text;

namespace RequestFlow;

/// <summary>
/// Renders contract lists for validation problem messages.
/// </summary>
internal static class ContractFormatting
{
    /// <summary>
    /// Renders each closed contract as <c>{contractName}&lt;{argument}&gt;</c>, comma-separated.
    /// </summary>
    public static string Format(string contractName, IReadOnlyList<Type> contracts)
    {
        var names = new StringBuilder();
        for (int i = 0; i < contracts.Count; i++)
        {
            if (i > 0)
                names.Append(", ");

            names.Append(contractName).Append('<')
                .Append(contracts[i].GetGenericArguments()[0].FullName).Append('>');
        }

        return names.ToString();
    }
}
