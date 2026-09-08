using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Builds a <see cref="RequestFlowModel"/> for testing validation rules without a container.
/// </summary>
/// <remarks>
/// Partial models are allowed, so tests need only supply the data their rule reads.
/// </remarks>
public sealed class RequestFlowModelBuilder
{
    private readonly Dictionary<Type, RequestModelBuilder> _requests = [];
    private readonly List<Type> _requestOrder = [];
    private readonly List<StageDeclarationInput> _stageDeclarations = [];
    private readonly List<Type> _eventTypes = [];
    private readonly List<EventSubscriptionInput> _eventSubscriptions = [];
    private readonly List<EventStrategyInput> _eventStrategies = [];

    /// <summary>
    /// Adds a request type or configures its existing entry.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public RequestFlowModelBuilder AddRequest(Type requestType, Action<RequestModelBuilder>? configure = null)
    {
        if (requestType is null)
            throw new ArgumentNullException(nameof(requestType));

        if (!_requests.TryGetValue(requestType, out RequestModelBuilder? request))
        {
            request = new RequestModelBuilder();
            _requests[requestType] = request;
            _requestOrder.Add(requestType);
        }

        configure?.Invoke(request);

        return this;
    }

    /// <summary>
    /// Adds one stage declaration.
    /// </summary>
    /// <remarks>
    /// A null <paramref name="contractType"/> records <c>IRequestStage&lt;TRequest, TResponse&gt;</c>.
    /// For other contracts, pass the open generic interface used by <see cref="RequestModelBuilder.AddStage"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestFlowModelBuilder AddStageDeclaration(Type stageType, Type? contractType = null)
        => AddStageDeclaration(stageType, RequestFlowLifetime.Transient, contractType);

    /// <summary>
    /// Adds one stage declaration registered with the named lifetime.
    /// </summary>
    /// <remarks>
    /// The overload without a lifetime uses <see cref="RequestFlowLifetime.Transient"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestFlowModelBuilder AddStageDeclaration(
        Type stageType, RequestFlowLifetime lifetime, Type? contractType = null)
    {
        if (stageType is null)
            throw new ArgumentNullException(nameof(stageType));

        // Resolve now, so a bad contract throws from the call that named it rather than from Build.
        Type resolved = OpenContract.OrDefault(
            contractType, typeof(IRequestStage<,>), typeof(IRequestStage<>));

        _stageDeclarations.Add(new StageDeclarationInput(stageType, lifetime, resolved));

        return this;
    }

    /// <summary>
    /// Adds a concrete, closed event type implementing <see cref="IEvent"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestFlowModelBuilder AddEvent(Type eventType)
    {
        if (eventType is null)
            throw new ArgumentNullException(nameof(eventType));

        // Reject invalid events here so a rule test cannot silently run against an empty event list.
        if (!EventClosure.IsConcreteClosedEvent(eventType))
        {
            throw new ArgumentException(
                $"'{eventType.FullName}' is not a concrete closed type implementing IEvent, so the " +
                "model cannot hold it as a known event. Name the concrete event type; an interface, " +
                "a base class, or an open generic belongs in AddEventHandler instead.",
                nameof(eventType));
        }

        _eventTypes.Add(eventType);

        return this;
    }

    /// <summary>
    /// Adds an event handler subscription for <see cref="IEvent"/> or a type implementing it.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestFlowModelBuilder AddEventHandler(
        Type handlerType,
        Type declaredEventType,
        RequestFlowLifetime lifetime = RequestFlowLifetime.Transient)
    {
        if (handlerType is null)
            throw new ArgumentNullException(nameof(handlerType));
        if (declaredEventType is null)
            throw new ArgumentNullException(nameof(declaredEventType));

        // A type outside the constraint would still reach every event assignable to it in the
        // closure, so the builder cannot accept a subscription the registry could never produce.
        if (!EventClosure.IsEventContract(declaredEventType))
        {
            throw new ArgumentException(
                $"'{declaredEventType.FullName}' does not implement IEvent, so no IEventHandler<TEvent> " +
                "can declare it. Name the event type, one of its base classes, an event interface, or IEvent itself.",
                nameof(declaredEventType));
        }

        _eventSubscriptions.Add(new EventSubscriptionInput(handlerType, declaredEventType, lifetime));

        return this;
    }

    /// <summary>
    /// Adds one event publish strategy declaration. A null <paramref name="declaredEventType"/>
    /// records the global fallback.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestFlowModelBuilder PublishEventsWith(
        Type? declaredEventType,
        Type strategyType,
        RequestFlowLifetime lifetime = RequestFlowLifetime.Singleton)
    {
        if (strategyType is null)
            throw new ArgumentNullException(nameof(strategyType));

        if (declaredEventType is not null && !EventClosure.IsEventContract(declaredEventType))
        {
            throw new ArgumentException(
                $"'{declaredEventType.FullName}' does not implement IEvent, so an event strategy " +
                "cannot target it. Name an event type, one of its base classes, an event interface, " +
                "IEvent itself, or null for the global fallback.",
                nameof(declaredEventType));
        }

        _eventStrategies.Add(new EventStrategyInput(
            declaredEventType, strategyType, lifetime));

        return this;
    }

    /// <summary>
    /// Produces the model, preserving request and known-event order.
    /// </summary>
    /// <remarks>
    /// Callable more than once, with each call producing an independent model.
    /// </remarks>
    public RequestFlowModel Build()
    {
        RequestModel[] requests = new RequestModel[_requestOrder.Count];
        for (int i = 0; i < requests.Length; i++)
        {
            Type requestType = _requestOrder[i];
            requests[i] = _requests[requestType].Build(requestType);
        }

        StageDeclarationModel[] declarations = new StageDeclarationModel[_stageDeclarations.Count];
        for (int i = 0; i < declarations.Length; i++)
        {
            StageDeclarationInput input = _stageDeclarations[i];
            declarations[i] = new StageDeclarationModel(
                input.StageType, input.Lifetime, StageReach.Of(requests, input.StageType),
                input.ContractType);
        }

        EventClosureResult eventClosure = EventClosure.Build(
            _eventTypes, _eventSubscriptions, _eventStrategies);

        return new RequestFlowModel(
            requests,
            declarations,
            eventClosure.Events,
            eventClosure.EventSubscriptions,
            eventClosure.EventStrategies);
    }

    /// <summary>
    /// Builds a fresh model and its validation context.
    /// </summary>
    /// <remarks>
    /// By default, unhandled requests and events are errors; unused stages and subscriptions are allowed.
    /// </remarks>
    public RequestFlowValidationContext BuildContext(
        bool allUnhandledRequestsAllowed = false, bool unusedStagesDisallowed = false)
        => BuildContext(allUnhandledRequestsAllowed, unusedStagesDisallowed, false, false);

    /// <summary>
    /// Builds a validation context with request, stage, and event flags.
    /// </summary>
    public RequestFlowValidationContext BuildContext(
        bool allUnhandledRequestsAllowed,
        bool unusedStagesDisallowed,
        bool allUnhandledEventsAllowed,
        bool unusedEventHandlersDisallowed)
        => new(
            Build(),
            allUnhandledRequestsAllowed,
            unusedStagesDisallowed,
            allUnhandledEventsAllowed,
            unusedEventHandlersDisallowed);

    private readonly struct StageDeclarationInput(
        Type stageType, RequestFlowLifetime lifetime, Type contractType)
    {
        public Type StageType { get; } = stageType;

        public RequestFlowLifetime Lifetime { get; } = lifetime;

        public Type ContractType { get; } = contractType;
    }
}
