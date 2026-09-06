using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Thrown when the dispatch map is built and the accumulated registrations are invalid;
/// aggregates every registration problem into one failure.
/// </summary>
public sealed class RequestFlowValidationException : InvalidOperationException
{
    /// <exception cref="ArgumentNullException"/>
    public RequestFlowValidationException(IReadOnlyList<RequestFlowValidationProblem> problems)
        : this(problems, null)
    { }

    /// <exception cref="ArgumentNullException"/>
    public RequestFlowValidationException(
        IReadOnlyList<RequestFlowValidationProblem> problems,
        Exception? innerException)
        : base(BuildMessage(problems), innerException)
    {
        Problems = problems;
    }

    /// <summary>
    /// Every registration problem found during startup validation.
    /// </summary>
    public IReadOnlyList<RequestFlowValidationProblem> Problems { get; }

    private static string BuildMessage(IReadOnlyList<RequestFlowValidationProblem> problems)
    {
        if (problems is null)
            throw new ArgumentNullException(nameof(problems));

        return "RequestFlow registration is invalid:" + Environment.NewLine +
            string.Join(Environment.NewLine, problems);
    }
}
