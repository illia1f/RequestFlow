using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RequestFlow;

/// <summary>
/// The request, stage, and event registration handed to every validation rule.
/// </summary>
/// <remarks>
/// One <c>AddStage</c> or <c>AddStreamStage</c> call is counted once in
/// <see cref="StageDeclarations"/> and again in every request it applies to. A stage that fits no
/// request is only in <see cref="StageDeclarations"/>.
/// <para>
/// A declaration the shape checks rejected is not here, so <c>AddStage</c> given a type that is
/// not a stage is reported without reaching a rule. Past that nothing is filtered. A request with
/// no handler is in the list, with an empty handler list. A stage added twice is in the list
/// twice. Reporting those is a rule's job.
/// </para>
/// <para>
/// Registration flags are held by <see cref="RequestFlowValidationContext"/>, not this model.
/// They select which built-in rules run and remain available to rules added by the application.
/// </para>
/// <para>
/// The library builds this. A rule test builds one with <see cref="RequestFlowModelBuilder"/>.
/// Every list on the model is read-only, so one rule cannot change what the next one reads.
/// </para>
/// </remarks>
public sealed class RequestFlowModel
{
    /// <exception cref="ArgumentNullException"/>
    internal RequestFlowModel(RequestModel[] requests, StageDeclarationModel[] stageDeclarations)
        : this(requests, stageDeclarations, [], [], [])
    { }

    /// <exception cref="ArgumentNullException"/>
    internal RequestFlowModel(
        RequestModel[] requests,
        StageDeclarationModel[] stageDeclarations,
        EventModel[] events,
        EventSubscriptionModel[] eventSubscriptions)
        : this(requests, stageDeclarations, events, eventSubscriptions, [])
    { }

    /// <exception cref="ArgumentNullException"/>
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
    /// One entry per <c>AddStage</c> or <c>AddStreamStage</c> call, in the order the calls ran, duplicates included.
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
