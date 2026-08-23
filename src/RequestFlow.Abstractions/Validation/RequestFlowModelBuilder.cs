using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Builds a <see cref="RequestFlowModel"/> by hand, which is how a validation rule is unit
/// tested without a container.
/// </summary>
/// <remarks>
/// A partial model is allowed on purpose. A rule that never reads handlers is tested against
/// requests that have none, so the builder does not require shapes the freeze would always produce.
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
    /// Adds a request type, or configures one already added. Repeated calls for one type
    /// configure a single entry, the way the freeze groups handlers by request type.
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
    /// Adds one stage declaration, as a single <c>AddStage</c> call would.
    /// </summary>
    /// <remarks>
    /// Leaving <paramref name="contractType"/> null records <c>IRequestStage&lt;TRequest, TResponse&gt;</c>, matching
    /// <see cref="RequestModelBuilder.AddStage"/>. Naming one takes an open generic interface,
    /// since that is what a rule compares against.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestFlowModelBuilder AddStageDeclaration(Type stageType, Type? contractType = null)
        => AddStageDeclaration(stageType, RequestFlowLifetime.Transient, contractType);

    /// <summary>
    /// Adds one stage declaration registered with the named lifetime.
    /// </summary>
    /// <remarks>
    /// The overload without a lifetime records <see cref="RequestFlowLifetime.Transient"/>, which is
    /// what <c>AddStage</c> uses unless the application asks for something else.
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
    /// Adds a known event type, which has to be a concrete closed type implementing
    /// <see cref="IEvent"/>, as only those get a plan at the freeze.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestFlowModelBuilder AddEvent(Type eventType)
    {
        if (eventType is null)
            throw new ArgumentNullException(nameof(eventType));

        // Rejected here rather than dropped at Build, where the rule under test would see an empty
        // event list and report nothing.
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
    /// Adds one event handler subscription. The declared event type has to implement
    /// <see cref="IEvent"/> or be <see cref="IEvent"/> itself, the way
    /// <c>IEventHandler&lt;TEvent&gt;</c> constrains it.
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
    /// Produces the context a rule is given at startup, wrapping a freshly built model.
    /// </summary>
    /// <remarks>
    /// The flags default to what registration does unless the application opts out: a request or
    /// an event with no handler is a problem, a stage or a subscription that reached nothing is not.
    /// </remarks>
    public RequestFlowValidationContext BuildContext(
        bool unhandledRequestsAllowed = false, bool unusedStagesDisallowed = false)
        => BuildContext(unhandledRequestsAllowed, unusedStagesDisallowed, false, false);

    /// <summary>
    /// Produces the context with the event flags named as well. All four parameters are required,
    /// so a call passing only the first two binds the two-flag overload.
    /// </summary>
    public RequestFlowValidationContext BuildContext(
        bool unhandledRequestsAllowed,
        bool unusedStagesDisallowed,
        bool unhandledEventsAllowed,
        bool unusedEventHandlersDisallowed)
        => new(
            Build(),
            unhandledRequestsAllowed,
            unusedStagesDisallowed,
            unhandledEventsAllowed,
            unusedEventHandlersDisallowed);

    private readonly struct StageDeclarationInput(
        Type stageType, RequestFlowLifetime lifetime, Type contractType)
    {
        public Type StageType { get; } = stageType;

        public RequestFlowLifetime Lifetime { get; } = lifetime;

        public Type ContractType { get; } = contractType;
    }
}
