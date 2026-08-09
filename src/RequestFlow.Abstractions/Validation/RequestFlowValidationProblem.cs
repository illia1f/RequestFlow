using System;

namespace RequestFlow;

/// <summary>
/// One startup validation finding: a stable code, a human-readable message, and the type at
/// fault when one exists. Two problems with the same code, message, and subject are equal.
/// </summary>
/// <remarks>
/// A custom rule picks its own code, staying off the <c>RF</c> prefix the built-in ones use:
/// <code>
/// new RequestFlowValidationProblem(
///     "APP0001",
///     $"Handler '{handlerType.FullName}' must be named ...Handler.",
///     handlerType);
/// </code>
/// </remarks>
public sealed record RequestFlowValidationProblem
{
    /// <exception cref="ArgumentNullException"/>
    public RequestFlowValidationProblem(string code, string message, Type? subject = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Subject = subject;
    }

    /// <summary>
    /// Stable identifier for the kind of problem; built-in codes are documented and never change.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// What is wrong and how to fix it.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// The offending type, when the problem points at one.
    /// </summary>
    public Type? Subject { get; }

    public override string ToString()
        => Code + ": " + Message;
}
