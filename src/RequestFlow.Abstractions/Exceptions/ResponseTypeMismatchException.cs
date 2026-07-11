using System;

namespace RequestFlow;

/// <summary>
/// Thrown by <see cref="IRequestDispatcher.SendAsync{TResponse}"/> when the dispatched
/// request type has a registered handler, but the call site's response type argument
/// differs from the one the handler was registered with.
/// </summary>
public sealed class ResponseTypeMismatchException(Type requestType, Type expected, Type actual)
    : InvalidOperationException(
        $"Request type '{requestType.FullName}' is registered with response type '{expected.FullName}', " +
        $"but SendAsync was called with response type '{actual.FullName}'. Check the call site's type argument.")
{
    /// <summary>
    /// The dispatched request type.
    /// </summary>
    public Type RequestType { get; } = requestType;

    /// <summary>
    /// The response type the handler was registered with.
    /// </summary>
    public Type ExpectedResponseType { get; } = expected;

    /// <summary>
    /// The response type the call site asked for.
    /// </summary>
    public Type ActualResponseType { get; } = actual;
}
