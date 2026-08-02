using System;

namespace RequestFlow;

/// <summary>
/// Base class for the exceptions thrown when a handler or a stage returns a null task
/// from HandleAsync.
/// </summary>
public abstract class NullTaskException : InvalidOperationException
{
    private protected NullTaskException(string message)
        : base(message)
    { }
}
