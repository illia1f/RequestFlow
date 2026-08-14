using System;

namespace RequestFlow;

/// <summary>
/// Base class for the exceptions thrown when a handler or a stage returns a null sequence from
/// Handle.
/// </summary>
public abstract class NullStreamException : InvalidOperationException
{
    private protected NullStreamException(string message)
        : base(message)
    { }
}
