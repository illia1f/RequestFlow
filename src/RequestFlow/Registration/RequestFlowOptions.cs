using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    internal List<Type> ManualHandlers { get; } = [];

    internal HashSet<Type> ExcludedHandlers { get; } = [];

    internal List<Type> ManualEventHandlers { get; } = [];

    internal HashSet<Type> ExcludedEventHandlers { get; } = [];

    internal bool UnusedStagesDisallowed { get; private set; }

    internal bool UnhandledEventsAllowed { get; private set; }

    internal bool UnusedEventHandlersDisallowed { get; private set; }

    internal ServiceLifetime HandlerLifetime { get; private set; } = ServiceLifetime.Transient;

    internal ServiceLifetime DispatcherLifetime { get; private set; } = ServiceLifetime.Scoped;

    private Assembly? ConfigurationDelegateAssembly { get; set; }

    /// <exception cref="ArgumentNullException"/>
    internal RequestFlowOptions Apply(Action<RequestFlowOptions> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        try
        {
            foreach (Action<RequestFlowOptions> invocation in configure.GetInvocationList())
            {
                ConfigurationDelegateAssembly = invocation.Method.Module.Assembly;
                invocation(this);
            }
        }
        finally
        {
            ConfigurationDelegateAssembly = null;
        }

        return this;
    }

    /// <summary>
    /// Registers this call's Task, ValueTask, and stream handlers as scoped instead of transient.
    /// Each later call sets the lifetime of the handlers it discovers.
    /// </summary>
    public RequestFlowOptions WithScopedHandlers()
    {
        HandlerLifetime = ServiceLifetime.Scoped;
        return this;
    }

    internal bool UnhandledRequestsAllowed { get; private set; }

    /// <summary>
    /// Skips missing-handler validation for all registered assemblies once any call opts in.
    /// Duplicate-handler validation still runs.
    /// </summary>
    public RequestFlowOptions AllowUnhandledRequests()
    {
        UnhandledRequestsAllowed = true;
        return this;
    }

    /// <summary>
    /// Selects <see cref="ParallelPublishStrategy"/> as the global event strategy.
    /// A conflicting global declaration from any <c>AddRequestFlow</c> call is a startup validation problem.
    /// </summary>
    public RequestFlowOptions PublishEventsInParallel()
        => PublishAllEventsWith<ParallelPublishStrategy>();

    /// <summary>
    /// Selects <see cref="ParallelPublishStrategy"/> for known events assignable to
    /// <typeparamref name="TEvent"/>.
    /// </summary>
    public RequestFlowOptions PublishEventsInParallel<TEvent>()
        where TEvent : IEvent
        => PublishEventsWith<TEvent, ParallelPublishStrategy>();

    /// <summary>
    /// Explicitly selects the default <see cref="SequentialPublishStrategy"/> as the global event strategy.
    /// A conflicting global declaration from any <c>AddRequestFlow</c> call is a startup validation problem.
    /// </summary>
    public RequestFlowOptions PublishEventsSequentially()
        => PublishAllEventsWith<SequentialPublishStrategy>();

    /// <summary>
    /// Selects <see cref="SequentialPublishStrategy"/> for known events assignable to
    /// <typeparamref name="TEvent"/>.
    /// </summary>
    public RequestFlowOptions PublishEventsSequentially<TEvent>()
        where TEvent : IEvent
        => PublishEventsWith<TEvent, SequentialPublishStrategy>();

    /// <summary>
    /// Selects <see cref="FailFastPublishStrategy"/> as the global event strategy.
    /// A conflicting global declaration from any <c>AddRequestFlow</c> call is a startup validation problem.
    /// </summary>
    public RequestFlowOptions PublishEventsFailFast()
        => PublishAllEventsWith<FailFastPublishStrategy>();

    /// <summary>
    /// Selects <see cref="FailFastPublishStrategy"/> for known events assignable to
    /// <typeparamref name="TEvent"/>.
    /// </summary>
    public RequestFlowOptions PublishEventsFailFast<TEvent>()
        where TEvent : IEvent
        => PublishEventsWith<TEvent, FailFastPublishStrategy>();

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
    /// Registers every Task, ValueTask, and stream handler contract on <typeparamref name="THandler"/> without scanning its assembly.
    /// </summary>
    public RequestFlowOptions AddHandler<THandler>()
        where THandler : class
    {
        ManualHandlers.Add(typeof(THandler));
        return this;
    }

    /// <summary>
    /// Keeps <typeparamref name="THandler"/>'s Task, ValueTask, and stream handler contracts out
    /// of this call's scan. A later call naming the same assembly does not re-scan it, so the
    /// exclusion holds until <see cref="AddHandler{THandler}"/> names the handler.
    /// </summary>
    public RequestFlowOptions ExcludeHandler<THandler>()
        where THandler : class
    {
        ExcludedHandlers.Add(typeof(THandler));
        return this;
    }

    /// <summary>
    /// Registers every <see cref="IEventHandler{TEvent}"/> contract on <typeparamref name="THandler"/> without scanning its assembly.
    /// </summary>
    public RequestFlowOptions AddEventHandler<THandler>()
        where THandler : class
    {
        ManualEventHandlers.Add(typeof(THandler));
        return this;
    }

    /// <summary>
    /// Keeps <typeparamref name="THandler"/>'s event handler contracts out of this call's scan.
    /// A later call naming the same assembly does not re-scan it, so the exclusion holds until
    /// <see cref="AddEventHandler{THandler}"/> names the handler.
    /// </summary>
    public RequestFlowOptions ExcludeEventHandler<THandler>()
        where THandler : class
    {
        ExcludedEventHandlers.Add(typeof(THandler));
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
    /// Scans the assembly containing the active <c>AddRequestFlow</c> configuration delegate.
    /// </summary>
    /// <remarks>
    /// Outside <c>AddRequestFlow</c>, this method falls back to <see cref="Assembly.GetCallingAssembly"/>,
    /// whose result can change under method inlining or tail calls.
    /// See the <see href="https://learn.microsoft.com/dotnet/api/system.reflection.assembly.getcallingassembly#remarks">.NET documentation</see>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public RequestFlowOptions RegisterHandlersFromCallingAssembly()
    {
        Assembly assembly = ConfigurationDelegateAssembly ?? Assembly.GetCallingAssembly();

        return RegisterHandlersFromAssembly(assembly);
    }

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
    /// type parameter, closed over each type in <paramref name="closingTypes"/>.
    /// The scan ignores open generic handlers; every closing must be declared here.
    /// Only null arguments throw at the call; an invalid declaration surfaces as a
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
    /// Registers a stage for applicable Task handlers, in execution order, outermost first.
    /// </summary>
    /// <remarks>
    /// Open generic constraints select requests. Closed stages also cover derived requests through contravariance.
    /// <paramref name="configure"/> can filter handlers and change the default transient lifetime.
    /// Repeated stage types are duplicates regardless of filters.
    /// Invalid stages are reported in <see cref="RequestFlowValidationException"/> when validation runs.
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
    /// Registers a stage for applicable ValueTask handlers, in execution order, outermost first.
    /// </summary>
    /// <remarks>
    /// Uses the registration rules of <see cref="AddStage(Type, Action{StageOptions})"/>.
    /// Task, ValueTask, and stream stages run in separate chains.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="InvalidOperationException"/>
    public RequestFlowOptions AddValueStage(
        Type stageType,
        Action<StageOptions>? configure = null)
        => DeclareStage(stageType, configure, StageFamily.Value);

    /// <summary>
    /// Registers <typeparamref name="TStage"/> under the same rules as
    /// <see cref="AddValueStage(Type, Action{StageOptions})"/>.
    /// </summary>
    public RequestFlowOptions AddValueStage<TStage>(
        Action<StageOptions>? configure = null)
        where TStage : class
        => AddValueStage(typeof(TStage), configure);

    /// <summary>
    /// Registers a stage for applicable stream handlers, in execution order, outermost first.
    /// </summary>
    /// <remarks>
    /// Uses the registration rules of <see cref="AddStage(Type, Action{StageOptions})"/>.
    /// Task, ValueTask, and stream stages run in separate chains.
    /// Registering one stage type through different family calls is a duplicate (<c>RF0103</c>).
    /// Async iterator stages allocate per enumeration, per level.
    /// Stages that return the sequence from <c>next.Invoke()</c> without iterating add no allocation.
    /// Neither cost grows with item count.
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
    /// Reports stages that reach no registered request.
    /// Applies to all registered stages once any call opts in.
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
