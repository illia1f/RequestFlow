namespace RequestFlow;

/// <summary>
/// The handler and stage family of a request pipeline.
/// </summary>
public enum RequestPipelineFamily
{
    /// <summary>
    /// Task requests and stages, including void requests.
    /// </summary>
    Task,

    /// <summary>
    /// ValueTask requests and stages, including void requests.
    /// </summary>
    ValueTask,

    /// <summary>
    /// Stream requests and stages returning asynchronous sequences.
    /// </summary>
    Stream
}
