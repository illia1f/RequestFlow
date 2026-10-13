namespace RequestFlow.Benchmarks.Core;

public enum ScopeLifetime
{
    /// <summary>
    /// Every call runs in one scope created before timing, which stays undisposed.
    /// </summary>
    RetainedScope,

    /// <summary>
    /// Every call creates its own scope and disposes it before completing.
    /// </summary>
    ScopePerOperation,

    /// <summary>
    /// Calls invoke a handler instance directly, so no scope is checked.
    /// </summary>
    DirectHandler,
}
