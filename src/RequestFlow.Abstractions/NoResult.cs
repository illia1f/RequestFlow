namespace RequestFlow;

/// <summary>
/// The response type of a request that returns nothing.
/// </summary>
public readonly struct NoResult
{
    /// <summary>
    /// The single, shared <see cref="NoResult"/> value.
    /// </summary>
    public static readonly NoResult Value = default;

    /// <summary>
    /// A completed task carrying <see cref="Value"/>. Code that must return a
    /// <c>Task&lt;NoResult&gt;</c> synchronously can return this instead of allocating one.
    /// </summary>
    public static readonly System.Threading.Tasks.Task<NoResult> Task =
        System.Threading.Tasks.Task.FromResult(Value);
}
