using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Thrown when the dispatch map is built and the accumulated registrations are invalid;
/// aggregates every registration problem into one failure.
/// </summary>
public sealed class RequestFlowValidationException(IReadOnlyList<string> problems)
    : InvalidOperationException(
        "RequestFlow registration is invalid:" + Environment.NewLine +
        string.Join(Environment.NewLine, problems))
{
    /// <summary>
    /// Every registration problem found during startup validation.
    /// </summary>
    public IReadOnlyList<string> Problems { get; } = problems;
}
