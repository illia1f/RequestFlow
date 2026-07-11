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
        List<string> problems = [.. _problems, .. RegistrationValidator.ValidateDuplicateHandlers(_handlers)];
        if (!UnhandledRequestsAllowed)
            problems.AddRange(RegistrationValidator.ValidateUnhandledRequests(_handlers, _requestTypes));

        if (problems.Count > 0)
            throw new RequestFlowValidationException(problems);

        Dictionary<Type, RequestPlanBase> plans = [];
        foreach (var handler in _handlers)
        {
            Type planType = handler.IsVoid
                ? typeof(VoidRequestPlan<>).MakeGenericType(handler.RequestType)
                : typeof(RequestPlan<,>).MakeGenericType(handler.RequestType, handler.ResponseType);
            plans[handler.RequestType] = (RequestPlanBase)Activator.CreateInstance(planType)!;
        }

        return new DispatchMap(plans);
    }
}
