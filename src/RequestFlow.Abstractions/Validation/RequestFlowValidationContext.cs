using System;

namespace RequestFlow;

/// <summary>
/// What a validation rule reads: the frozen registration, plus the registration choices that shape a finding.
/// </summary>
/// <remarks>
/// Built once per freeze and handed to every rule, so a rule added with <c>AddValidationRule</c>
/// sees the facts a built-in one sees.
/// <para>
/// A rule test builds one with <see cref="RequestFlowModelBuilder.BuildContext(bool, bool)"/>
/// instead of standing up a container.
/// </para>
/// </remarks>
public sealed class RequestFlowValidationContext
{
    /// <exception cref="ArgumentNullException"/>
    internal RequestFlowValidationContext(
        RequestFlowModel model, bool unhandledRequestsAllowed, bool unusedStagesDisallowed)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        UnhandledRequestsAllowed = unhandledRequestsAllowed;
        UnusedStagesDisallowed = unusedStagesDisallowed;
    }

    /// <summary>
    /// The frozen registration: every request, its handlers, and the stages that reached it.
    /// </summary>
    public RequestFlowModel Model { get; }

    /// <summary>
    /// True when <c>AllowUnhandledRequests</c> was called, so a request with no handler is permitted.
    /// </summary>
    public bool UnhandledRequestsAllowed { get; }

    /// <summary>
    /// True when <c>DisallowUnusedStages</c> was called, so a stage that reached no request is a problem.
    /// </summary>
    public bool UnusedStagesDisallowed { get; }
}
