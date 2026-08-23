using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports every known event with no applicable handler.
/// </summary>
internal sealed class UnhandledEventRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(
        RequestFlowValidationContext context)
    {
        foreach (var @event in context.Model.Events)
        {
            if (@event.Handlers.Count == 0)
            {
                yield return new RequestFlowValidationProblem(
                    ProblemCodes.UnhandledEvent,
                    $"Event '{@event.EventType.FullName}' has no handler.",
                    @event.EventType);
            }
        }
    }
}
