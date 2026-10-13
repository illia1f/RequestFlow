namespace RequestFlow.Benchmarks.Harness;

internal static class BenchmarkArgumentPolicy
{
    private const string PublishedRuntime = "net10.0";

    // Options that select benchmarks, place artifacts or pick the published runtime without changing measurement.
    private static readonly string[] ValueOptions =
        ["--filter", "-f", "--artifacts", "-a", "--runtimes", "-r", "--anyCategories", "--allCategories"];

    private static readonly string[] FlagOptions = ["--join"];

    /// <summary>
    /// Returns whether any argument falls outside the options that leave measurement unchanged.
    /// </summary>
    internal static bool HasMeasurementOverrides(string[] arguments)
    {
        string? option = null;

        foreach (string argument in arguments)
        {
            string? value = argument;

            if (argument.StartsWith('-'))
            {
                int equals = argument.IndexOf('=');

                option = equals < 0 ? argument : argument[..equals];
                value = equals < 0 ? null : argument[(equals + 1)..];

                if (!Contains(ValueOptions, option) && !Contains(FlagOptions, option))
                    return true;
            }

            // A value belongs to the last option and continues until the next token that starts with '-'.
            if (value is not null && !IsAllowedValue(option, value))
                return true;
        }

        return false;
    }

    private static bool IsAllowedValue(string? option, string value)
        => option is not null && Contains(ValueOptions, option) &&
            (!Contains(["--runtimes", "-r"], option) || value.Equals(PublishedRuntime, StringComparison.OrdinalIgnoreCase));

    private static bool Contains(string[] options, string option) => options.Contains(option, StringComparer.OrdinalIgnoreCase);
}
