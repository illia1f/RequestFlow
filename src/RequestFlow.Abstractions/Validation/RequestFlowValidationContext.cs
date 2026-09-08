using System;

namespace RequestFlow;

/// <summary>
/// What a validation rule reads: the frozen registration, plus the registration choices that shape a finding.
/// </summary>
/// <remarks>
/// Built once per freeze and handed to every rule, so a rule added with <c>AddValidationRule</c>
/// sees the facts a built-in one sees.
/// <para>
/// A rule test builds one with <see cref="RequestFlowModelBuilder.BuildContext(bool, bool, bool, bool)"/>
/// instead of standing up a container.
/// </para>
/// </remarks>
public sealed class RequestFlowValidationContext
{
    /// <exception cref="ArgumentNullException"/>
    internal RequestFlowValidationContext(
        RequestFlowModel model,
        bool allUnhandledRequestsAllowed,
        bool unusedStagesDisallowed,
        bool allUnhandledEventsAllowed,
        bool unusedEventHandlersDisallowed)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        AllUnhandledRequestsAllowed = allUnhandledRequestsAllowed;
        UnusedStagesDisallowed = unusedStagesDisallowed;
        AllUnhandledEventsAllowed = allUnhandledEventsAllowed;
        UnusedEventHandlersDisallowed = unusedEventHandlersDisallowed;
    }

    /// <summary>
    /// The frozen request, stage, and event registration.
    /// </summary>
    public RequestFlowModel Model { get; }

    /// <summary>
    /// True when <c>AllowUnhandledRequests</c> was called, so a request with no handler is permitted.
    /// </summary>
    public bool AllUnhandledRequestsAllowed { get; }

    /// <summary>
    /// True when <c>DisallowUnusedStages</c> was called, so a stage that reached no request is a problem.
    /// </summary>
    public bool UnusedStagesDisallowed { get; }

    /// <summary>
    /// True when an event with no applicable handler is permitted.
    /// </summary>
    public bool AllUnhandledEventsAllowed { get; }

    /// <summary>
    /// True when an event subscription that reaches no known event is a problem.
    /// </summary>
    public bool UnusedEventHandlersDisallowed { get; }
}
