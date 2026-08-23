using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Accumulates handler registrations across every <c>AddRequestFlow</c> call on one
/// service collection and builds the validated request and event maps for each provider.
/// </summary>
internal sealed class RequestFlowRegistry
{
    private readonly List<HandlerRegistration> _handlers = [];
    private readonly List<Type> _requestTypes = [];
    private readonly List<EventHandlerRegistration> _eventHandlers = [];
    private readonly List<Type> _eventTypes = [];
    private readonly List<EventStrategyDeclaration> _eventStrategyDeclarations = [];

    // _problems keeps the report in first-seen order; _seenProblems makes the dedup O(1).
    private readonly List<RequestFlowValidationProblem> _problems = [];
    private readonly HashSet<RequestFlowValidationProblem> _seenProblems = [];
    private readonly HashSet<Assembly> _assemblies = [];
    private readonly HashSet<GenericHandlerClosing> _closings = [];
    private readonly List<StageDeclaration> _stageDeclarations = [];
    private readonly HashSet<Type> _registeredClosedStageTypes = [];

    /// <summary>
    /// True once any <c>AddRequestFlow</c> call opted out of the missing-handler check.
    /// </summary>
    public bool UnhandledRequestsAllowed { get; private set; }

    /// <summary>
    /// Opts the whole registry out of the missing-handler check; sticky across calls.
    /// </summary>
    public void AllowUnhandledRequests()
        => UnhandledRequestsAllowed = true;

    /// <summary>
    /// True once any <c>AddRequestFlow</c> call asked for a stage that applies to nothing to be fatal.
    /// </summary>
    public bool UnusedStagesDisallowed { get; private set; }

    /// <summary>
    /// Makes a stage that applies to nothing a validation problem; sticky across calls.
    /// </summary>
    public void DisallowUnusedStages()
        => UnusedStagesDisallowed = true;

    public bool UnhandledEventsAllowed { get; private set; }

    public void AllowUnhandledEvents()
        => UnhandledEventsAllowed = true;

    public bool UnusedEventHandlersDisallowed { get; private set; }

    public void DisallowUnusedEventHandlers()
        => UnusedEventHandlersDisallowed = true;

    /// <summary>
    /// Every stage declaration accumulated so far, in registration order, which is execution order.
    /// </summary>
    public IReadOnlyList<StageDeclaration> StageDeclarations => _stageDeclarations;

    /// <summary>
    /// The cached stage closings shared by registration, the freeze, and validation.
    /// </summary>
    public StageClosingCache ClosingCache { get; } = new();

    public IReadOnlyList<HandlerRegistration> Handlers => _handlers;

    public IReadOnlyList<EventHandlerRegistration> EventHandlers => _eventHandlers;

    public IReadOnlyList<EventStrategyDeclaration> EventStrategyDeclarations
        => _eventStrategyDeclarations;

    /// <summary>
    /// Appends one call's shape-valid stage declarations. A stage belongs to a chain once, so
    /// duplicates are reported at freeze rather than skipped here.
    /// </summary>
    public void AddStageDeclarations(IReadOnlyList<StageDeclaration> declarations)
        => _stageDeclarations.AddRange(declarations);

    public void AddEventStrategyDeclarations(
        IReadOnlyList<EventStrategyDeclaration> declarations)
    {
        foreach (EventStrategyDeclaration declaration in declarations)
        {
            bool duplicate = false;
            foreach (EventStrategyDeclaration existing in _eventStrategyDeclarations)
            {
                if (existing.DeclaredEventType == declaration.DeclaredEventType
                    && existing.StrategyType == declaration.StrategyType
                    && existing.Lifetime == declaration.Lifetime)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
                _eventStrategyDeclarations.Add(declaration);
        }
    }

    /// <summary>
    /// Records <paramref name="closedStageType"/> unless an earlier call already did, and returns
    /// whether this call recorded it, so each closed stage gets one descriptor.
    /// </summary>
    public bool TryAddClosedStageType(Type closedStageType)
        => _registeredClosedStageTypes.Add(closedStageType);

    /// <summary>
    /// Adds the assemblies not registered by an earlier call and returns the newly added ones.
    /// </summary>
    public IReadOnlyList<Assembly> AddNewAssemblies(IReadOnlyList<Assembly> assemblies)
        => AddUnseen(assemblies, _assemblies);

    /// <summary>
    /// Adds the closings not declared by an earlier call and returns the newly added ones.
    /// </summary>
    public IReadOnlyList<GenericHandlerClosing> AddNewClosings(IEnumerable<GenericHandlerClosing> closings)
        => AddUnseen(closings, _closings);

    private static IReadOnlyList<T> AddUnseen<T>(IEnumerable<T> items, HashSet<T> seen)
    {
        List<T> added = [];
        foreach (var item in items)
        {
            if (seen.Add(item))
                added.Add(item);
        }

        return added;
    }

    public void Add(
        IReadOnlyList<HandlerRegistration> handlers,
        IReadOnlyList<Type> requestTypes,
        IReadOnlyList<RequestFlowValidationProblem> problems)
        => Add(handlers, requestTypes, [], [], problems);

    public void Add(
        IReadOnlyList<HandlerRegistration> handlers,
        IReadOnlyList<Type> requestTypes,
        IReadOnlyList<EventHandlerRegistration> eventHandlers,
        IReadOnlyList<Type> eventTypes,
        IReadOnlyList<RequestFlowValidationProblem> problems)
    {
        _handlers.AddRange(handlers);
        _requestTypes.AddRange(requestTypes);
        _eventHandlers.AddRange(eventHandlers);
        _eventTypes.AddRange(eventTypes);

        // A problem compares by value and is deterministic per declaration, so the same
        // declaration repeated across calls dedups to one entry.
        foreach (var problem in problems)
        {
            if (_seenProblems.Add(problem))
                _problems.Add(problem);
        }
    }

    /// <summary>
    /// Validates everything accumulated, including any rule registered with
    /// <c>AddValidationRule</c> and resolved from <paramref name="provider"/>, and builds the
    /// request and event maps for the resolving provider.
    /// </summary>
    /// <exception cref="RequestFlowValidationException"/>
    /// <exception cref="InvalidOperationException"/>
    public FrozenPlans Freeze(IServiceProvider provider)
    {
        EventClosureResult eventClosure = EventClosure.Build(
            _eventTypes, EventSubscriptions(), EventStrategies());
        RequestFlowModel model = RegistrationSnapshot.Capture(
            _handlers, _requestTypes, _stageDeclarations, ClosingCache, eventClosure);

        // One context for the whole pass, so a built-in rule and a registered one read the same facts.
        RequestFlowValidationContext context = new(
            model,
            UnhandledRequestsAllowed,
            UnusedStagesDisallowed,
            UnhandledEventsAllowed,
            UnusedEventHandlersDisallowed);

        List<RequestFlowValidationProblem> problems = ValidationRuleRunner.Run(
            context,
            BuiltInRules.For(context, _stageDeclarations, eventClosure.StrategyResolution),
            provider.GetServices<IRequestFlowValidationRule>(),
            _problems);

        if (problems.Count > 0)
            throw new RequestFlowValidationException(problems);

        Dictionary<Type, StageChain> chainsByRequest = BuildStagePlans();

        // Duplicate handlers were reported above, so one plan lands per handler here.
        Dictionary<Type, RequestPlanBase> plans = [];
        foreach (var handler in _handlers)
        {
            plans[handler.RequestType] = CreatePlan(handler, chainsByRequest[handler.RequestType]);
        }

        var dispatch = new DispatchMap(plans);
        EventMap events = EventPlanFactory.Build(
            eventClosure.Events,
            eventClosure.StrategyResolution);

        return new FrozenPlans(dispatch, events);
    }

    private EventStrategyInput[] EventStrategies()
    {
        var strategies = new EventStrategyInput[_eventStrategyDeclarations.Count];
        for (int i = 0; i < strategies.Length; i++)
        {
            EventStrategyDeclaration declaration = _eventStrategyDeclarations[i];
            strategies[i] = new EventStrategyInput(
                declaration.DeclaredEventType,
                declaration.StrategyType,
                ModelLifetime.Of(declaration.Lifetime));
        }

        return strategies;
    }

    private EventSubscriptionInput[] EventSubscriptions()
    {
        var subscriptions = new EventSubscriptionInput[_eventHandlers.Count];
        for (int i = 0; i < subscriptions.Length; i++)
        {
            EventHandlerRegistration handler = _eventHandlers[i];
            subscriptions[i] = new EventSubscriptionInput(
                handler.HandlerType,
                handler.DeclaredEventType,
                ModelLifetime.Of(handler.Lifetime));
        }

        return subscriptions;
    }

    private Dictionary<Type, StageChain> BuildStagePlans()
    {
        Dictionary<Type, StageChain> chainsByRequest = [];
        List<Type> ordered = [];

        foreach (var handler in _handlers)
        {
            ordered.Clear();
            foreach (var declaration in _stageDeclarations)
            {
                if (!ClosingCache.TryClose(declaration, handler, out Type closedStageType))
                    continue;

                ordered.Add(closedStageType);
            }

            Type[] stageTypes = ordered.ToArray();

            chainsByRequest[handler.RequestType] =
                new StageChain(stageTypes, TypedShapesFor(handler, stageTypes));
        }

        return chainsByRequest;
    }

    // Only a void request can take stages of either contract shape, so only its chain records
    // which shape each level runs under. A stage implementing both runs as the two-parameter form.
    private static bool[] TypedShapesFor(HandlerRegistration handler, Type[] stageTypes)
    {
        if (!handler.IsVoid || stageTypes.Length == 0)
            return [];

        Type typedContract = typeof(IRequestStage<,>).MakeGenericType(handler.RequestType, typeof(NoResult));
        bool[] typedShapes = new bool[stageTypes.Length];
        for (int i = 0; i < stageTypes.Length; i++)
            typedShapes[i] = typedContract.IsAssignableFrom(stageTypes[i]);

        return typedShapes;
    }

    private static RequestPlanBase CreatePlan(HandlerRegistration handler, StageChain chain)
    {
        bool staged = chain.StageTypes.Length > 0;
        Type planType = PlanTypeFor(handler, staged);

        return staged
            ? (RequestPlanBase)Activator.CreateInstance(planType, [chain])!
            : (RequestPlanBase)Activator.CreateInstance(planType)!;
    }

    private static Type PlanTypeFor(HandlerRegistration handler, bool staged)
    {
        if (handler.ContractDefinition == typeof(IStreamRequestHandler<,>))
        {
            return staged
                ? typeof(StagedStreamPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType)
                : typeof(StreamPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType);
        }

        if (handler.IsVoid)
            return staged ? typeof(StagedVoidRequestPlan<>).MakeGenericType(handler.RequestType)
                : typeof(VoidRequestPlan<>).MakeGenericType(handler.RequestType);

        return staged
            ? typeof(StagedRequestPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType)
            : typeof(RequestPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType);
    }
}

/// <summary>
/// One request's stages in execution order. <see cref="TypedShapes"/> records, per position,
/// whether the stage runs as <see cref="IRequestStage{TRequest, TResponse}"/>; it is empty for
/// a request whose stages can only take that one shape.
/// </summary>
internal sealed class StageChain(Type[] stageTypes, bool[] typedShapes)
{
    public Type[] StageTypes { get; } = stageTypes;

    public bool[] TypedShapes { get; } = typedShapes;
}
