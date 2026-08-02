using System;

namespace RequestFlow;

/// <summary>
/// Thrown by <see cref="IRequestDispatcher.SendAsync{TResponse}"/> when the handler for the
/// dispatched request returns a null task from HandleAsync.
/// </summary>
public sealed class HandlerNullTaskException(Type requestType)
    : NullTaskException($"The handler for '{requestType.FullName}' returned a null task from HandleAsync.")
{
    /// <summary>
    /// The request type whose handler returned the null task.
    /// </summary>
    public Type RequestType { get; } = requestType;
}
