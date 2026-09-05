using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RequestFlow;

/// <summary>
/// The request, stage, and event registration handed to every validation rule.
/// </summary>
/// <remarks>
/// Lists are read-only and include unhandled requests and duplicate stages.
/// Declarations rejected by registration shape checks are excluded.
/// Unmatched stages remain in <see cref="StageDeclarations"/>.
/// Registration flags are on <see cref="RequestFlowValidationContext"/>.
/// Use <see cref="RequestFlowModelBuilder"/> to build a model for rule tests.
/// </remarks>
public sealed class RequestFlowModel
{
    internal RequestFlowModel(RequestModel[] requests, StageDeclarationModel[] stageDeclarations)
        : this(requests, stageDeclarations, [], [], [])
    { }

    internal RequestFlowModel(
        RequestModel[] requests,
        StageDeclarationModel[] stageDeclarations,
        EventModel[] events,
        EventSubscriptionModel[] eventSubscriptions)
        : this(requests, stageDeclarations, events, eventSubscriptions, [])
    { }

    internal RequestFlowModel(
        RequestModel[] requests,
        StageDeclarationModel[] stageDeclarations,
        EventModel[] events,
        EventSubscriptionModel[] eventSubscriptions,
        EventStrategyModel[] eventStrategies)
    {
        Requests = new ReadOnlyCollection<RequestModel>(
            requests ?? throw new ArgumentNullException(nameof(requests)));
        StageDeclarations = new ReadOnlyCollection<StageDeclarationModel>(
            stageDeclarations ?? throw new ArgumentNullException(nameof(stageDeclarations)));
        Events = new ReadOnlyCollection<EventModel>(
            events ?? throw new ArgumentNullException(nameof(events)));
        EventSubscriptions = new ReadOnlyCollection<EventSubscriptionModel>(
            eventSubscriptions ?? throw new ArgumentNullException(nameof(eventSubscriptions)));
        EventStrategies = new ReadOnlyCollection<EventStrategyModel>(
            eventStrategies ?? throw new ArgumentNullException(nameof(eventStrategies)));
    }

    /// <summary>
    /// Every request type registration knows about: the scanned ones in scan order, then any type a
    /// handler brought in on its own.
    /// </summary>
    public IReadOnlyList<RequestModel> Requests { get; }

    /// <summary>
    /// One entry per <c>AddStage</c>, <c>AddValueStage</c>, or <c>AddStreamStage</c> call, in call order, including duplicates.
    /// </summary>
    public IReadOnlyList<StageDeclarationModel> StageDeclarations { get; }

    /// <summary>
    /// Every known concrete event and its handlers in frozen delivery order.
    /// </summary>
    public IReadOnlyList<EventModel> Events { get; }

    /// <summary>
    /// Every event handler subscription and the known events it reaches.
    /// </summary>
    public IReadOnlyList<EventSubscriptionModel> EventSubscriptions { get; }

    /// <summary>
    /// Every event publish strategy declaration and the known events it selects.
    /// </summary>
    public IReadOnlyList<EventStrategyModel> EventStrategies { get; }
}
