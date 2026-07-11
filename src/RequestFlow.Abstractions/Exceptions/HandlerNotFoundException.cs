using System;

namespace RequestFlow;

/// <summary>
/// Thrown by <see cref="IRequestDispatcher.SendAsync{TResponse}"/> when the dispatched
/// request type has no entry in the dispatch map.
/// </summary>
public sealed class HandlerNotFoundException(Type requestType)
    : InvalidOperationException(
        $"No handler is registered for request type '{requestType.FullName}'. " +
        "Make sure its assembly is included in RegisterHandlersFromAssembly* during AddRequestFlow.")
{
    /// <summary>
    /// The request type that had no registered handler.
    /// </summary>
    public Type RequestType { get; } = requestType;
}
