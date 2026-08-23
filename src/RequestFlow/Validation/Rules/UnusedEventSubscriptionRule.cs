using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports every event subscription that reaches no known event.
/// </summary>
internal sealed class UnusedEventSubscriptionRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(
        RequestFlowValidationContext context)
    {
        foreach (var subscription in context.Model.EventSubscriptions)
        {
            if (subscription.ReachedEvents.Count == 0)
            {
                yield return new RequestFlowValidationProblem(
                    ProblemCodes.UnusedEventSubscription,
                    $"Event subscription '{subscription.HandlerType.FullName}' declared for " +
                    $"'{subscription.DeclaredEventType.FullName}' reaches no known event; scan the " +
                    "assembly containing the events it targets or drop DisallowUnusedEventHandlers.",
                    subscription.HandlerType);
            }
        }
    }
}
