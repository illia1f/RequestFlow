namespace RequestFlow;

internal static class DeploymentWarnings
{
    internal const string Trimming =
        "RequestFlow uses reflection to discover registrations and build dispatch plans. Trimming is not supported, including with manual handler registration.";

    internal const string NativeAot =
        "RequestFlow constructs closed generic dispatch plans at runtime. Native AOT is not supported, including with manual handler registration.";
}
