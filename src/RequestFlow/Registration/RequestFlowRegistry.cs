using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
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

    private readonly HashSet<HandlerContractKey> _seenHandlerContracts = [];
    private readonly HashSet<EventSubscriptionKey> _seenEventSubscriptions = [];
    private readonly List<Type> _eventTypes = [];
    private readonly List<EventStrategyDeclaration> _eventStrategyDeclarations = [];

    // _problems keeps the report in first-seen order; _seenProblems makes the dedup O(1).
    private readonly List<RequestFlowValidationProblem> _problems = [];
    private readonly HashSet<RequestFlowValidationProblem> _seenProblems = [];
    private readonly HashSet<Assembly> _assemblies = [];
    private readonly HashSet<GenericHandlerClosing> _closings = [];
    private readonly List<StageDeclaration> _stageDeclarations = [];
    private readonly HashSet<Type> _registeredClosedStageTypes = [];
    private readonly AsyncLocal<FreezeEntry?> _activeFreeze = new();
    private readonly UnhandledMessageExemptions _exemptions = new();

    public void AddExemptions(UnhandledMessageExemptions exemptions)
        => _exemptions.Add(exemptions);

    public bool AllUnhandledRequestsAllowed { get; private set; }

    public void AllowAllUnhandledRequests()
        => AllUnhandledRequestsAllowed = true;

    public bool UnusedStagesDisallowed { get; private set; }

    public void DisallowUnusedStages()
        => UnusedStagesDisallowed = true;

    public bool AllUnhandledEventsAllowed { get; private set; }

    public void AllowAllUnhandledEvents()
        => AllUnhandledEventsAllowed = true;

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
    /// Returns true when this closed stage type is recorded for the first time.
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

    /// <summary>
    /// Adds the subscriptions whose handler and declared event pair is not already present and returns the newly added ones.
    /// </summary>
    public IReadOnlyList<EventHandlerRegistration> AddNewEventHandlers(
        IReadOnlyList<EventHandlerRegistration> handlers)
    {
        List<EventHandlerRegistration> added = [];
        foreach (var handler in handlers)
        {
            if (_seenEventSubscriptions.Add(new EventSubscriptionKey(handler.HandlerType, handler.DeclaredEventType)))
            {
                _eventHandlers.Add(handler);
                added.Add(handler);
            }
        }

        return added;
    }

    /// <summary>
    /// Adds the handlers whose implementation and contract pair is not already present and returns the newly added ones.
    /// </summary>
    public IReadOnlyList<HandlerRegistration> AddNewHandlers(
        IReadOnlyList<HandlerRegistration> handlers)
    {
        List<HandlerRegistration> added = [];
        foreach (var handler in handlers)
        {
            if (_seenHandlerContracts.Add(new HandlerContractKey(handler.ImplementationType, handler.Contract)))
            {
                _handlers.Add(handler);
                added.Add(handler);
            }
        }

        return added;
    }

    public void Add(
        IReadOnlyList<Type> requestTypes,
        IReadOnlyList<RequestFlowValidationProblem> problems)
        => Add(requestTypes, [], problems);

    public void Add(
        IReadOnlyList<Type> requestTypes,
        IReadOnlyList<Type> eventTypes,
        IReadOnlyList<RequestFlowValidationProblem> problems)
    {
        _requestTypes.AddRange(requestTypes);
        _eventTypes.AddRange(eventTypes);

        foreach (var problem in problems)
        {
            if (_seenProblems.Add(problem))
                _problems.Add(problem);
        }
    }

    /// <summary>
    /// Runs built-in and registered validation rules, then builds this provider's request and event maps.
    /// </summary>
    /// <exception cref="RequestFlowValidationException"/>
    /// <exception cref="InvalidOperationException"/>
    public FrozenPlans Freeze(IServiceProvider provider)
    {
        ThrowIfFreezing(provider);
        FreezeEntry? previous = _activeFreeze.Value;
        var current = new FreezeEntry(provider.GetRequiredService<IServiceScopeFactory>(), previous);
        _activeFreeze.Value = current;
        try
        {
            return FreezeCore(provider);
        }
        finally
        {
            Volatile.Write(ref current.ScopeFactory, null);
            _activeFreeze.Value = previous;
        }
    }

    public void ThrowIfFreezing(IServiceProvider provider)
    {
        FreezeEntry? entry = _activeFreeze.Value;
        if (entry is null)
            return;

        // The scope factory identifies the provider across its root and child scopes.
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        for (; entry is not null; entry = entry.Previous)
        {
            if (ReferenceEquals(Volatile.Read(ref entry.ScopeFactory), scopeFactory))
            {
                throw new InvalidOperationException(
                    "RequestFlow validation cannot be re-entered through a dispatcher, IEventPublisher, " +
                    "ValidateRequestFlow, or InspectRequestFlow while validation is running. " +
                    "Remove that dependency from the validation rule and its dependencies; use context.Model instead.");
            }
        }
    }

    private FrozenPlans FreezeCore(IServiceProvider provider)
    {
        EventClosureResult eventClosure = EventClosure.Build(
            _eventTypes, BuildEventSubscriptions(), BuildEventStrategies());
        RegistrationSnapshot snapshot = RegistrationSnapshot.Capture(
            _handlers,
            _requestTypes,
            _stageDeclarations,
            ClosingCache,
            eventClosure);
        RequestFlowModel model = snapshot.Model;

        RequestFlowValidationContext context = new(
            model,
            AllUnhandledRequestsAllowed,
            UnusedStagesDisallowed,
            AllUnhandledEventsAllowed,
            UnusedEventHandlersDisallowed,
            _exemptions);

        ValidationRuleRunner.Validate(
            context,
            BuiltInRules.For(context, snapshot.StageFacts, eventClosure.StrategyResolution),
            provider.GetServices<IRequestFlowValidationRule>(),
            _problems);

        Dictionary<Type, RequestPipeline> pipelines = PipelineInspection.Capture(
            model, _handlers, _stageDeclarations, ClosingCache);

        Dictionary<Type, RequestPlanBase> plans = [];
        foreach (var handler in _handlers)
        {
            StageChain chain = BuildStageChain(handler, pipelines[handler.RequestType].Stages);
            plans[handler.RequestType] = CreatePlan(handler, chain);
        }

        var dispatch = new DispatchMap(plans);
        EventMap events = EventPlanFactory.Build(
            eventClosure.Events,
            eventClosure.StrategyResolution);

        return new FrozenPlans(dispatch, events, pipelines);
    }

    private EventStrategyInput[] BuildEventStrategies()
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

    private EventSubscriptionInput[] BuildEventSubscriptions()
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

    private static StageChain BuildStageChain(
        HandlerRegistration handler, IReadOnlyList<RequestPipelineStage> stages)
    {
        Type[] stageTypes = new Type[stages.Count];
        for (int i = 0; i < stages.Count; i++)
            stageTypes[i] = stages[i].ClosedType;

        return new StageChain(stageTypes, BuildTypedShapes(handler, stageTypes));
    }

    // Only a void request can take stages of either contract shape, so only its chain records
    // which shape each level runs under. A stage implementing both runs as the two-parameter form.
    private static bool[] BuildTypedShapes(HandlerRegistration handler, Type[] stageTypes)
    {
        if (!handler.IsVoid || stageTypes.Length == 0)
            return [];

        Type typedStageDefinition = handler.ContractDefinition == typeof(IValueRequestHandler<>)
            ? typeof(IValueRequestStage<,>)
            : typeof(IRequestStage<,>);
        Type typedContract = typedStageDefinition.MakeGenericType(
            handler.RequestType,
            typeof(NoResult));
        bool[] typedShapes = new bool[stageTypes.Length];
        for (int i = 0; i < stageTypes.Length; i++)
            typedShapes[i] = typedContract.IsAssignableFrom(stageTypes[i]);

        return typedShapes;
    }

    private static RequestPlanBase CreatePlan(HandlerRegistration handler, StageChain chain)
    {
        bool staged = chain.StageTypes.Length > 0;
        Type planType = GetPlanType(handler, staged);

        return staged
            ? (RequestPlanBase)Activator.CreateInstance(planType, [chain])!
            : (RequestPlanBase)Activator.CreateInstance(planType)!;
    }

    private static Type GetPlanType(HandlerRegistration handler, bool staged)
    {
        if (handler.ContractDefinition == typeof(IStreamRequestHandler<,>))
        {
            return staged
                ? typeof(StagedStreamPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType)
                : typeof(StreamPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType);
        }

        if (handler.ContractDefinition == typeof(IValueRequestHandler<,>))
        {
            return staged
                ? typeof(StagedValueRequestPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType)
                : typeof(ValueRequestPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType);
        }

        if (handler.ContractDefinition == typeof(IValueRequestHandler<>))
        {
            return staged
                ? typeof(StagedValueVoidRequestPlan<>).MakeGenericType(handler.RequestType)
                : typeof(ValueVoidRequestPlan<>).MakeGenericType(handler.RequestType);
        }

        if (handler.IsVoid)
            return staged ? typeof(StagedVoidRequestPlan<>).MakeGenericType(handler.RequestType)
                : typeof(VoidRequestPlan<>).MakeGenericType(handler.RequestType);

        return staged
            ? typeof(StagedRequestPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType)
            : typeof(RequestPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType);
    }

    private readonly record struct HandlerContractKey(Type ImplementationType, Type Contract);

    private readonly record struct EventSubscriptionKey(Type HandlerType, Type DeclaredEventType);

    private sealed class FreezeEntry(IServiceScopeFactory scopeFactory, FreezeEntry? previous)
    {
        public IServiceScopeFactory? ScopeFactory = scopeFactory;

        public FreezeEntry? Previous { get; } = previous;
    }
}

/// <summary>
/// One request's stages in execution order. <see cref="TypedShapes"/> records, per position,
/// whether a void stage runs under its family's two-parameter contract; it is empty for a
/// request whose stages can only take one shape.
/// </summary>
internal sealed class StageChain(Type[] stageTypes, bool[] typedShapes)
{
    public Type[] StageTypes { get; } = stageTypes;

    public bool[] TypedShapes { get; } = typedShapes;
}
