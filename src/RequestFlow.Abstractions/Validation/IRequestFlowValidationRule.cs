using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// A startup validation check. Runs once when the dispatch map freezes; every reported
/// problem lands in the single <see cref="RequestFlowValidationException"/>.
/// </summary>
/// <remarks>
/// Register a rule with <c>AddValidationRule&lt;HandlerNamingRule&gt;()</c>.
/// </remarks>
public interface IRequestFlowValidationRule
{
    /// <summary>
    /// Inspects the context and returns every problem found; empty when all is well.
    /// </summary>
    IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context);
}
