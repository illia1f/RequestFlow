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

        Dictionary<Type, RequestPlanBase> plans = [];
        foreach (var handler in _handlers)
        {
            plans[handler.RequestType] = CreatePlan(handler, stagePlans.StageTypesByRequest[handler.RequestType]);
        }

        return new DispatchMap(plans);
    }

    // Ordering and chain shape are decided at freeze, never per AddRequestFlow call.
    private StagePlanSet BuildStagePlans()
    {
        Dictionary<Type, Type[]> stageTypesByRequest = [];
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

            stageTypesByRequest[handler.RequestType] = ordered.ToArray();
        }

        return new StagePlanSet(stageTypesByRequest, appliedStageTypes);
    }

    private static RequestPlanBase CreatePlan(HandlerRegistration handler, Type[] stageTypes)
    {
        if (stageTypes.Length == 0)
        {
            Type planType = handler.IsVoid
                ? typeof(VoidRequestPlan<>).MakeGenericType(handler.RequestType)
                : typeof(RequestPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType);
            return (RequestPlanBase)Activator.CreateInstance(planType)!;
        }

        Type stagedPlanType = handler.IsVoid
            ? typeof(StagedVoidRequestPlan<>).MakeGenericType(handler.RequestType)
            : typeof(StagedRequestPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType);

        // Wrapped in an object array on purpose: Type[] converts to object[], so handing
        // stageTypes straight through would be read as one constructor argument per stage type.
        return (RequestPlanBase)Activator.CreateInstance(stagedPlanType, [(object)stageTypes])!;
    }
}

/// <summary>
/// The ordered stage types for each request type, plus the stage types that reached at least
/// one request.
/// </summary>
internal sealed class StagePlanSet(Dictionary<Type, Type[]> stageTypesByRequest, HashSet<Type> appliedStageTypes)
{
    public Dictionary<Type, Type[]> StageTypesByRequest { get; } = stageTypesByRequest;

    public ISet<Type> AppliedStageTypes { get; } = appliedStageTypes;
}
