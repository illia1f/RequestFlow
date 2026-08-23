using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Everything user-configurable about RequestFlow, passed to the <c>AddRequestFlow</c> configure delegate.
/// </summary>
public sealed class RequestFlowOptions
{
    internal List<Assembly> Assemblies { get; } = [];

    internal List<GenericHandlerDeclaration> Declarations { get; } = [];

    internal List<StageDeclaration> StageDeclarations { get; } = [];

    internal List<EventStrategyDeclaration> EventStrategyDeclarations { get; } = [];

    internal bool UnusedStagesDisallowed { get; private set; }

    internal bool UnhandledEventsAllowed { get; private set; }

    internal bool UnusedEventHandlersDisallowed { get; private set; }

    internal ServiceLifetime HandlerLifetime { get; private set; } = ServiceLifetime.Transient;

    internal ServiceLifetime DispatcherLifetime { get; private set; } = ServiceLifetime.Scoped;

    /// <exception cref="ArgumentNullException"/>
    internal RequestFlowOptions Apply(Action<RequestFlowOptions> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        configure(this);

        return this;
    }

    /// <summary>
    /// Registers this call's handlers with a scoped lifetime instead of the default transient.
    /// Applies only to the handlers this call discovers; a later call decides for its own.
    /// Transient and scoped are the whole set: a singleton handler pins every dependency it
    /// injects for the life of the process.
    /// </summary>
    public RequestFlowOptions WithScopedHandlers()
    {
        HandlerLifetime = ServiceLifetime.Scoped;
        return this;
    }

    internal bool UnhandledRequestsAllowed { get; private set; }

    /// <summary>
    /// Skips the missing-handler check when the dispatch map freezes. Intended for
    /// contracts assemblies whose requests are handled elsewhere. Applies to all
    /// registered assemblies once any call opts in; duplicate-handler validation is unaffected.
    /// </summary>
    public RequestFlowOptions AllowUnhandledRequests()
    {
        UnhandledRequestsAllowed = true;
        return this;
    }

    /// <summary>
    /// Selects <see cref="ParallelPublishStrategy"/> as the global event strategy. A conflicting
    /// global declaration from any <c>AddRequestFlow</c> call is a startup validation problem.
    /// </summary>
    public RequestFlowOptions PublishEventsInParallel()
        => PublishAllEventsWith<ParallelPublishStrategy>();

    /// <summary>
    /// Selects <typeparamref name="TStrategy"/> as the fallback strategy for every event without
    /// a matching per-event declaration.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public RequestFlowOptions PublishAllEventsWith<TStrategy>(
        Action<EventStrategyOptions>? configure = null)
        where TStrategy : class, IEventPublishStrategy
        => DeclareEventStrategy(null, typeof(TStrategy), configure);

    /// <summary>
    /// Selects <typeparamref name="TStrategy"/> for known events assignable to
    /// <typeparamref name="TEvent"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public RequestFlowOptions PublishEventsWith<TEvent, TStrategy>(
        Action<EventStrategyOptions>? configure = null)
        where TEvent : IEvent
        where TStrategy : class, IEventPublishStrategy
        => DeclareEventStrategy(typeof(TEvent), typeof(TStrategy), configure);

    /// <summary>
    /// Permits a known event to have no applicable handler. Once enabled, this setting applies
    /// to every event registered by any <c>AddRequestFlow</c> call on the service collection.
    /// </summary>
    public RequestFlowOptions AllowUnhandledEvents()
    {
        UnhandledEventsAllowed = true;
        return this;
    }

    /// <summary>
    /// Reports an event handler subscription or per-event strategy declaration that reaches no
    /// known event. Once enabled, this setting applies to every declaration registered by any
    /// <c>AddRequestFlow</c> call on the service collection.
    /// </summary>
    public RequestFlowOptions DisallowUnusedEventHandlers()
    {
        UnusedEventHandlersDisallowed = true;
        return this;
    }

    /// <summary>
    /// Registers the dispatcher with a transient lifetime instead of the default scoped.
    /// The first <c>AddRequestFlow</c> call fixes the dispatcher lifetime; later calls cannot change it.
    /// </summary>
    public RequestFlowOptions WithTransientDispatcher()
    {
        DispatcherLifetime = ServiceLifetime.Transient;
        return this;
    }

    /// <summary>
    /// Scans the assembly containing <typeparamref name="T"/> for handlers and requests.
    /// </summary>
    public RequestFlowOptions RegisterHandlersFromAssemblyContaining<T>()
        => RegisterHandlersFromAssembly(typeof(T).Assembly);

    /// <summary>
    /// Scans <paramref name="assembly"/> for handlers and requests.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public RequestFlowOptions RegisterHandlersFromAssembly(Assembly assembly)
    {
        if (assembly is null)
            throw new ArgumentNullException(nameof(assembly));

        Assemblies.Add(assembly);

        return this;
    }

    /// <summary>
    /// Registers <paramref name="handlerType"/>, an open generic handler definition with one
    /// type parameter, closed over each type in <paramref name="closingTypes"/>. The scan
    /// ignores open generic handlers; every closing must be declared here. Only null
    /// arguments throw at the call; an invalid declaration surfaces as a
    /// <see cref="RequestFlowValidationException"/> problem when the dispatch map is built.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestFlowOptions RegisterGenericHandler(Type handlerType, params Type[] closingTypes)
    {
        if (handlerType is null)
            throw new ArgumentNullException(nameof(handlerType));
        if (closingTypes is null)
            throw new ArgumentNullException(nameof(closingTypes));

        foreach (var closingType in closingTypes)
        {
            if (closingType is null)
                throw new ArgumentException("Closing types must not contain null.", nameof(closingTypes));
        }

        Declarations.Add(new GenericHandlerDeclaration(handlerType, closingTypes));

        return this;
    }

    /// <summary>
    /// Registers <paramref name="stageType"/> to run around the handler of every request it
    /// applies to. Registration order is execution order, outermost first.
    /// </summary>
    /// <remarks>
    /// Pass an open generic definition such as <c>typeof(LoggingStage&lt;,&gt;)</c> to let the
    /// stage's own constraints decide which requests it reaches, or a closed stage class to
    /// target one request contract. A closed stage is not restricted to the request type it
    /// names: <c>TRequest</c> is contravariant, so it also wraps every request deriving from
    /// that one, and <paramref name="configure"/> narrows the set further. A stage type belongs
    /// to a chain once, so a second call naming it is a duplicate whatever it filters on.
    /// Each stage carries its own lifetime, transient unless <paramref name="configure"/> says
    /// otherwise. An invalid stage surfaces as a <see cref="RequestFlowValidationException"/>
    /// problem when the dispatch map is built.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="InvalidOperationException"/>
    public RequestFlowOptions AddStage(Type stageType, Action<StageOptions>? configure = null)
        => DeclareStage(stageType, configure, StageFamily.Request);

    /// <summary>
    /// Registers <typeparamref name="TStage"/> under the same rules as <see cref="AddStage(Type, Action{StageOptions})"/>.
    /// </summary>
    public RequestFlowOptions AddStage<TStage>(Action<StageOptions>? configure = null)
        where TStage : class
        => AddStage(typeof(TStage), configure);

    /// <summary>
    /// Registers <paramref name="stageType"/> to run around the handler of every stream request it
    /// applies to. Registration order is execution order, outermost first.
    /// </summary>
    /// <remarks>
    /// The stream counterpart of <see cref="AddStage(Type, Action{StageOptions})"/>, under the same
    /// rules for open generics, contravariance, handler filters, and lifetimes. A stream stage never
    /// reaches a task handler and a task stage never reaches a stream handler. A type implementing
    /// both contracts and declared through both calls is one stage type registered twice, which
    /// <c>RF0103</c> reports as a duplicate, so there is no way to register a type meant for both families.
    /// Unlike a request stage, a stream stage written as an async iterator costs once per
    /// enumeration and once per level: its own state machine and its own enumerator. A stage that
    /// returns the sequence from <c>next</c> without iterating it costs nothing, and neither cost
    /// grows with the number of items.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="InvalidOperationException"/>
    public RequestFlowOptions AddStreamStage(Type stageType, Action<StageOptions>? configure = null)
        => DeclareStage(stageType, configure, StageFamily.Stream);

    /// <summary>
    /// Registers <typeparamref name="TStage"/> under the same rules as <see cref="AddStreamStage(Type, Action{StageOptions})"/>.
    /// </summary>
    public RequestFlowOptions AddStreamStage<TStage>(Action<StageOptions>? configure = null)
        where TStage : class
        => AddStreamStage(typeof(TStage), configure);

    // Registration order is execution order, so a declaration is appended where the call was made.
    private RequestFlowOptions DeclareStage(Type stageType, Action<StageOptions>? configure, StageFamily family)
    {
        if (stageType is null)
            throw new ArgumentNullException(nameof(stageType));

        var stage = new StageOptions();
        configure?.Invoke(stage);

        StageDeclarations.Add(new StageDeclaration(stageType, stage.HandlerFilter, family, stage.Lifetime));

        return this;
    }

    /// <summary>
    /// Reports a stage that reaches no registered request as a validation problem instead of
    /// leaving it a silent no-op. Applies to all registered stages once any call opts in.
    /// </summary>
    public RequestFlowOptions DisallowUnusedStages()
    {
        UnusedStagesDisallowed = true;
        return this;
    }

    private RequestFlowOptions DeclareEventStrategy(
        Type? declaredEventType,
        Type strategyType,
        Action<EventStrategyOptions>? configure)
    {
        var strategy = new EventStrategyOptions(strategyType);
        configure?.Invoke(strategy);
        EventStrategyDeclarations.Add(new EventStrategyDeclaration(
            declaredEventType, strategyType, strategy.Lifetime));
        return this;
    }
}
