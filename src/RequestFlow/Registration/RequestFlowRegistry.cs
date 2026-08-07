using System;
using System.Collections.Generic;
using System.Reflection;

namespace RequestFlow;

/// <summary>
/// Accumulates handler registrations across every <c>AddRequestFlow</c> call on one
/// service collection and builds the validated dispatch map for each provider.
/// </summary>
internal sealed class RequestFlowRegistry
{
    private readonly List<HandlerRegistration> _handlers = [];
    private readonly List<Type> _requestTypes = [];

    // _problems keeps the report in first-seen order; _seenProblems makes the dedup O(1).
    private readonly List<string> _problems = [];
    private readonly HashSet<string> _seenProblems = [];
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
    /// True once any <c>AddRequestFlow</c> call asked for a stage that applies to nothing to be
    /// fatal.
    /// </summary>
    public bool UnusedStagesDisallowed { get; private set; }

    /// <summary>
    /// Makes a stage that applies to nothing a validation problem; sticky across calls.
    /// </summary>
    public void DisallowUnusedStages()
        => UnusedStagesDisallowed = true;

    /// <summary>
    /// Every stage declaration accumulated so far, in registration order, which is execution
    /// order.
    /// </summary>
    public IReadOnlyList<StageDeclaration> StageDeclarations => _stageDeclarations;

    /// <summary>
    /// The cached stage closings shared by registration, the freeze, and validation.
    /// </summary>
    public StageClosingCache ClosingCache { get; } = new();

    /// <summary>
    /// Every handler accumulated so far.
    /// </summary>
    public IReadOnlyList<HandlerRegistration> Handlers => _handlers;

    /// <summary>
    /// Appends one call's shape-valid stage declarations. A stage declared twice would run
    /// twice, so duplicates are reported at freeze rather than skipped here.
    /// </summary>
    public void AddStageDeclarations(IReadOnlyList<StageDeclaration> declarations)
        => _stageDeclarations.AddRange(declarations);

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
    {
        List<Assembly> added = [];
        foreach (var assembly in assemblies)
        {
            if (_assemblies.Add(assembly))
                added.Add(assembly);
        }

        return added;
    }

    /// <summary>
    /// Adds the closings not declared by an earlier call and returns the newly added ones.
    /// </summary>
    public IReadOnlyList<GenericHandlerClosing> AddNewClosings(IReadOnlyList<GenericHandlerClosing> closings)
    {
        List<GenericHandlerClosing> added = [];
        foreach (var closing in closings)
        {
            if (_closings.Add(closing))
                added.Add(closing);
        }

        return added;
    }

    /// <summary>
    /// Records one call's scan and closing results.
    /// </summary>
    public void Add(
        IReadOnlyList<HandlerRegistration> handlers,
        IReadOnlyList<Type> requestTypes,
        IReadOnlyList<string> problems)
    {
        _handlers.AddRange(handlers);
        _requestTypes.AddRange(requestTypes);

        // The validator is the only producer of problem strings, so string identity is a
        // safe dedup key for the same declaration repeated across calls.
        foreach (var problem in problems)
        {
            if (_seenProblems.Add(problem))
                _problems.Add(problem);
        }
    }

    /// <summary>
    /// Validates everything accumulated and builds the dispatch map for the resolving
    /// provider.
    /// </summary>
    /// <exception cref="RequestFlowValidationException"/>
    public DispatchMap BuildDispatchMap()
    {
        List<string> problems =
        [
            .. _problems,
            .. RegistrationValidator.ValidateDuplicateHandlers(_handlers),
            .. RegistrationValidator.ValidateDuplicateStages(_stageDeclarations),
            .. RegistrationValidator.ValidateAliasedStages(_stageDeclarations, _handlers, ClosingCache),
        ];
        if (!UnhandledRequestsAllowed)
            problems.AddRange(RegistrationValidator.ValidateUnhandledRequests(_handlers, _requestTypes));

        // Built before the throw, because the strict check needs to know which stages applied.
        StagePlanSet stagePlans = BuildStagePlans();
        if (UnusedStagesDisallowed)
        {
            problems.AddRange(
                RegistrationValidator.ValidateUnusedStages(_stageDeclarations, stagePlans.AppliedStageTypes));
        }

        if (problems.Count > 0)
            throw new RequestFlowValidationException(problems);

        // Duplicate handlers were reported above, so one plan lands per handler here.
        Dictionary<Type, RequestPlanBase> plans = [];
        foreach (var handler in _handlers)
        {
            plans[handler.RequestType] = CreatePlan(handler, stagePlans.ChainsByRequest[handler.RequestType]);
        }

        return new DispatchMap(plans);
    }

    // Ordering and chain shape are decided at freeze, never per AddRequestFlow call.
    private StagePlanSet BuildStagePlans()
    {
        Dictionary<Type, StageChain> chainsByRequest = [];
        HashSet<Type> appliedStageTypes = [];
        List<Type> ordered = [];

        foreach (var handler in _handlers)
        {
            ordered.Clear();
            foreach (var declaration in _stageDeclarations)
            {
                if (!ClosingCache.TryClose(declaration, handler, out Type closedStageType))
                    continue;

                ordered.Add(closedStageType);
                appliedStageTypes.Add(declaration.StageType);
            }

            Type[] stageTypes = ordered.ToArray();

            chainsByRequest[handler.RequestType] =
                new StageChain(stageTypes, TypedShapesFor(handler, stageTypes));
        }

        return new StagePlanSet(chainsByRequest, appliedStageTypes);
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

    // The staged plans build their own levels, keeping the reflection at this one call.
    private static RequestPlanBase CreatePlan(HandlerRegistration handler, StageChain chain)
    {
        if (chain.StageTypes.Length == 0)
        {
            Type planType = handler.IsVoid
                ? typeof(VoidRequestPlan<>).MakeGenericType(handler.RequestType)
                : typeof(RequestPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType);

            return (RequestPlanBase)Activator.CreateInstance(planType)!;
        }

        Type stagedPlanType = handler.IsVoid
            ? typeof(StagedVoidRequestPlan<>).MakeGenericType(handler.RequestType)
            : typeof(StagedRequestPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType);

        return (RequestPlanBase)Activator.CreateInstance(stagedPlanType, [chain])!;
    }
}

/// <summary>
/// The stage chain for each request type, plus the stage types that reached at least one
/// request.
/// </summary>
internal sealed class StagePlanSet(Dictionary<Type, StageChain> chainsByRequest, HashSet<Type> appliedStageTypes)
{
    public Dictionary<Type, StageChain> ChainsByRequest { get; } = chainsByRequest;

    public ISet<Type> AppliedStageTypes { get; } = appliedStageTypes;
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
