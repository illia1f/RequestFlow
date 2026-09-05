using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports invalid, conflicting, ambiguous, unused, and lifetime-incompatible event strategies.
/// </summary>
internal sealed class EventStrategyRule(EventStrategyResolution resolution)
    : IRequestFlowValidationRule
{
    private readonly EventStrategyResolution _resolution = resolution
        ?? throw new ArgumentNullException(nameof(resolution));

    public IEnumerable<RequestFlowValidationProblem> Validate(
        RequestFlowValidationContext context)
    {
        IReadOnlyList<EventStrategyModel> strategies = context.Model.EventStrategies;

        foreach (RequestFlowValidationProblem problem in FindShapeProblems(strategies))
            yield return problem;

        foreach (RequestFlowValidationProblem problem in FindTargetConflicts(strategies))
            yield return problem;

        foreach (EventStrategyAmbiguity ambiguity in _resolution.Ambiguities)
            yield return CreateAmbiguityProblem(ambiguity, strategies);

        foreach (RequestFlowValidationProblem problem in FindLifetimeConflicts(strategies))
            yield return problem;

        if (context.UnusedEventHandlersDisallowed)
        {
            for (int i = 0; i < strategies.Count; i++)
            {
                EventStrategyModel strategy = strategies[i];
                if (strategy.DeclaredEventType is null || _resolution.ApplicableDeclarations[i])
                    continue;

                yield return new RequestFlowValidationProblem(
                    ProblemCodes.UnusedEventStrategy,
                    $"Event strategy '{strategy.StrategyType.FullName}' targets " +
                    $"'{strategy.DeclaredEventType.FullName}', but that declaration applies to no " +
                    "known event. Scan the event assembly, correct the target type, or remove the " +
                    "declaration.",
                    strategy.DeclaredEventType);
            }
        }

        foreach (RequestFlowValidationProblem problem in FindRoleLifetimeConflicts(
            context.Model, strategies))
        {
            yield return problem;
        }
    }

    private static IEnumerable<RequestFlowValidationProblem> FindShapeProblems(
        IReadOnlyList<EventStrategyModel> strategies)
    {
        var reportedInterfaces = new HashSet<Type>();
        var reportedAbstractTypes = new HashSet<Type>();
        foreach (EventStrategyModel strategy in strategies)
        {
            Type strategyType = strategy.StrategyType;
            if (strategyType.IsInterface && reportedInterfaces.Add(strategyType))
            {
                yield return new RequestFlowValidationProblem(
                    ProblemCodes.EventStrategyIsInterface,
                    $"Event publish strategy '{strategyType.FullName}' is an interface, so the " +
                    "container cannot construct it. Name a concrete strategy class.",
                    strategyType);
            }
            else if (strategyType.IsAbstract && reportedAbstractTypes.Add(strategyType))
            {
                yield return new RequestFlowValidationProblem(
                    ProblemCodes.EventStrategyAbstract,
                    $"Event publish strategy '{strategyType.FullName}' is abstract, so the " +
                    "container cannot construct it. Name a concrete strategy class.",
                    strategyType);
            }
        }
    }

    private static IEnumerable<RequestFlowValidationProblem> FindTargetConflicts(
        IReadOnlyList<EventStrategyModel> strategies)
    {
        EventStrategyModel? global = null;
        var targets = new Dictionary<Type, EventStrategyModel>();
        var reportedTargets = new HashSet<Type>();
        bool globalReported = false;

        foreach (EventStrategyModel strategy in strategies)
        {
            if (strategy.DeclaredEventType is null)
            {
                if (global is null)
                {
                    global = strategy;
                }
                else if (global.StrategyType != strategy.StrategyType && !globalReported)
                {
                    globalReported = true;
                    yield return new RequestFlowValidationProblem(
                        ProblemCodes.ConflictingEventStrategies,
                        $"The global event strategy is declared as both " +
                        $"'{global.StrategyType.FullName}' and '{strategy.StrategyType.FullName}'. " +
                        "Keep one global strategy declaration.");
                }

                continue;
            }

            Type target = strategy.DeclaredEventType;
            if (!targets.TryGetValue(target, out EventStrategyModel? first))
            {
                targets.Add(target, strategy);
            }
            else if (first.StrategyType != strategy.StrategyType && reportedTargets.Add(target))
            {
                yield return new RequestFlowValidationProblem(
                    ProblemCodes.ConflictingEventStrategies,
                    $"Event target '{target.FullName}' is declared with both " +
                    $"'{first.StrategyType.FullName}' and '{strategy.StrategyType.FullName}'. " +
                    "Keep one strategy for that target.",
                    target);
            }
        }
    }

    private static RequestFlowValidationProblem CreateAmbiguityProblem(
        EventStrategyAmbiguity ambiguity,
        IReadOnlyList<EventStrategyModel> strategies)
    {
        var declarations = new string[ambiguity.DeclarationIndexes.Length];
        for (int i = 0; i < declarations.Length; i++)
        {
            EventStrategyModel strategy = strategies[ambiguity.DeclarationIndexes[i]];
            declarations[i] =
                $"'{strategy.DeclaredEventType!.FullName}' with '{strategy.StrategyType.FullName}'";
        }

        return new RequestFlowValidationProblem(
            ProblemCodes.AmbiguousEventStrategy,
            $"Event '{ambiguity.EventType.FullName}' has equally specific strategy declarations: " +
            string.Join(", ", declarations) +
            ". Declare the strategy on the exact event type to select one.",
            ambiguity.EventType);
    }

    private static IEnumerable<RequestFlowValidationProblem> FindLifetimeConflicts(
        IReadOnlyList<EventStrategyModel> strategies)
    {
        var firstLifetimes = new Dictionary<Type, RequestFlowLifetime>();
        var reported = new HashSet<Type>();
        foreach (EventStrategyModel strategy in strategies)
        {
            if (!firstLifetimes.TryGetValue(
                strategy.StrategyType, out RequestFlowLifetime firstLifetime))
            {
                firstLifetimes.Add(strategy.StrategyType, strategy.Lifetime);
            }
            else if (firstLifetime != strategy.Lifetime && reported.Add(strategy.StrategyType))
            {
                yield return new RequestFlowValidationProblem(
                    ProblemCodes.EventStrategyLifetime,
                    $"Event publish strategy '{strategy.StrategyType.FullName}' is declared with " +
                    $"both {firstLifetime} and {strategy.Lifetime} lifetimes. Use one lifetime for " +
                    "each strategy type.",
                    strategy.StrategyType);
            }
        }
    }

    private static IEnumerable<RequestFlowValidationProblem> FindRoleLifetimeConflicts(
        RequestFlowModel model,
        IReadOnlyList<EventStrategyModel> strategies)
    {
        Dictionary<Type, List<RequestFlowLifetime>> roleLifetimes = BuildRoleLifetimes(model);
        var reported = new HashSet<Type>();
        foreach (EventStrategyModel strategy in strategies)
        {
            if (!roleLifetimes.TryGetValue(
                strategy.StrategyType, out List<RequestFlowLifetime>? lifetimes))
            {
                continue;
            }

            RequestFlowLifetime? conflictingLifetime = null;
            foreach (RequestFlowLifetime lifetime in lifetimes)
            {
                if (lifetime != strategy.Lifetime)
                {
                    conflictingLifetime = lifetime;
                    break;
                }
            }

            if (!conflictingLifetime.HasValue || !reported.Add(strategy.StrategyType))
                continue;

            yield return new RequestFlowValidationProblem(
                ProblemCodes.EventStrategyRoleLifetime,
                $"Class '{strategy.StrategyType.FullName}' is registered as a " +
                $"{strategy.Lifetime} event publish strategy and as a " +
                $"{conflictingLifetime.Value} event handler or " +
                "stage. Use the same lifetime or split the roles into separate classes.",
                strategy.StrategyType);
        }
    }

    private static Dictionary<Type, List<RequestFlowLifetime>> BuildRoleLifetimes(
        RequestFlowModel model)
    {
        var lifetimes = new Dictionary<Type, List<RequestFlowLifetime>>();

        // Only event handlers and reached stages share the strategy's concrete service key.
        // Task, ValueTask, and stream handlers use handler-interface keys, so their lifetimes
        // cannot collide with the strategy's.
        foreach (EventSubscriptionModel subscription in model.EventSubscriptions)
            AddLifetime(lifetimes, subscription.HandlerType, subscription.Lifetime);

        foreach (RequestModel request in model.Requests)
        {
            foreach (ClosedStageModel stage in request.Stages)
            {
                foreach (StageDeclarationModel declaration in model.StageDeclarations)
                {
                    if (declaration.StageType == stage.DeclaredType)
                    {
                        AddLifetime(
                            lifetimes,
                            stage.ClosedType,
                            declaration.Lifetime);
                    }
                }
            }
        }

        return lifetimes;
    }

    private static void AddLifetime(
        Dictionary<Type, List<RequestFlowLifetime>> lifetimes,
        Type type,
        RequestFlowLifetime lifetime)
    {
        if (!lifetimes.TryGetValue(type, out List<RequestFlowLifetime>? values))
        {
            values = [];
            lifetimes.Add(type, values);
        }

        if (!values.Contains(lifetime))
            values.Add(lifetime);
    }
}
