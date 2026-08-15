using System;

namespace RequestFlow;

/// <summary>
/// Thrown when a stream handler returns a null sequence from Handle, at the point the chain
/// enters the handler's level.
/// </summary>
public sealed class HandlerNullStreamException(Type requestType)
    : NullStreamException(
        $"The handler for '{requestType.FullName}' returned a null sequence from Handle; " +
        "return an empty sequence to yield nothing.")
{
    /// <summary>
    /// The request type whose handler returned the null sequence.
    /// </summary>
    public Type RequestType { get; } = requestType;
}
