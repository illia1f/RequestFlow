namespace RequestFlow;

/// <summary>
/// How long a registered handler or stage instance lives.
/// </summary>
/// <remarks>
/// Stages support all three lifetimes. Handlers are transient or scoped,
/// selected by the <c>AddRequestFlow</c> call that discovers them.
/// </remarks>
public enum RequestFlowLifetime
{
    /// <summary>
    /// A new instance for every resolution.
    /// </summary>
    Transient,

    /// <summary>
    /// One instance per scope.
    /// </summary>
    Scoped,

    /// <summary>
    /// One instance for the life of the container.
    /// </summary>
    Singleton
}
