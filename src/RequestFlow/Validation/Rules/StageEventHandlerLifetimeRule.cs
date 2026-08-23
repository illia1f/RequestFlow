using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports a class registered as a stage that also handles events under a different lifetime.
/// </summary>
internal sealed class StageEventHandlerLifetimeRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        Dictionary<Type, RequestFlowLifetime> handlerLifetimes = [];
        foreach (var subscription in context.Model.EventSubscriptions)
        {
            if (!handlerLifetimes.ContainsKey(subscription.HandlerType))
                handlerLifetimes.Add(subscription.HandlerType, subscription.Lifetime);
        }

        if (handlerLifetimes.Count == 0)
            yield break;

        // One class declared twice is RF0103's finding; the collision itself is reported once.
        HashSet<Type> reported = [];

        foreach (var declaration in context.Model.StageDeclarations)
        {
            // A declaration that reached nothing registers no descriptor, so nothing collides.
            if (declaration.ReachedRequests.Count == 0)
                continue;

            if (!handlerLifetimes.TryGetValue(declaration.StageType, out RequestFlowLifetime handlerLifetime))
                continue;

            if (handlerLifetime == declaration.Lifetime || !reported.Add(declaration.StageType))
                continue;

            yield return new RequestFlowValidationProblem(
                ProblemCodes.StageEventHandlerLifetime,
                $"Class '{declaration.StageType.FullName}' is registered as a {declaration.Lifetime} " +
                $"stage and as a {handlerLifetime} event handler. Both descriptors are keyed on the " +
                $"concrete class, so the one registered last decides the lifetime for both roles. " +
                $"Split the two roles into two classes.",
                declaration.StageType);
        }
    }
}
