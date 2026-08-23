using System;

namespace RequestFlow;

/// <summary>
/// Thrown by <see cref="IRequestDispatcher.SendAsync{TResponse}"/> and by
/// <c>IStreamDispatcher.Stream</c> when the dispatched request type has no entry in the dispatch map.
/// </summary>
/// <exception cref="ArgumentNullException"/>
public sealed class HandlerNotFoundException(Type requestType) : InvalidOperationException(BuildMessage(requestType))
{
    /// <summary>
    /// The request type that had no registered handler.
    /// </summary>
    public Type RequestType { get; } = requestType;

    private static string BuildMessage(Type requestType)
    {
        if (requestType is null)
            throw new ArgumentNullException(nameof(requestType));

        return $"No handler is registered for request type '{requestType.FullName}'. " +
            "Make sure its assembly is included in RegisterHandlersFromAssembly* during AddRequestFlow.";
    }
}
