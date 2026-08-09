namespace RequestFlow;

/// <summary>
/// How long a registered handler or stage instance lives.
/// </summary>
/// <remarks>
/// A stage takes any of the three, since <c>AddStage</c> names the lifetime per call. A handler is
/// transient or scoped, chosen by the <c>AddRequestFlow</c> call that found it.
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
