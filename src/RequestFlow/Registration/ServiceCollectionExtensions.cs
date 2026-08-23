using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestFlow;

// Microsoft's convention for IServiceCollection extensions: AddRequestFlow needs no extra using.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// RequestFlow registration entry point.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers RequestFlow: scans the configured assemblies and registers the discovered
    /// handlers. Calls are additive; assemblies and closings already registered by an
    /// earlier call are skipped. The first dispatcher resolution or call to
    /// <c>ValidateRequestFlow</c> validates and builds the request and event maps together once
    /// per provider. Invalid registration throws a <see cref="RequestFlowValidationException"/>
    /// that lists every problem.
    /// Returns a <see cref="RequestFlowBuilder"/> for chaining optional feature registrations.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="RequestFlowValidationException"/>
    public static RequestFlowBuilder AddRequestFlow(
        this IServiceCollection services, Action<RequestFlowOptions> configure)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        RequestFlowOptions options = new RequestFlowOptions().Apply(configure);

        RequestFlowRegistry registry = GetOrAddRegistry(services);
        if (options.UnhandledRequestsAllowed)
            registry.AllowUnhandledRequests();
        registry.AddEventStrategyDeclarations(options.EventStrategyDeclarations);
        if (options.UnhandledEventsAllowed)
            registry.AllowUnhandledEvents();
        if (options.UnusedEventHandlersDisallowed)
            registry.DisallowUnusedEventHandlers();
        if (options.UnusedStagesDisallowed)
            registry.DisallowUnusedStages();

        Validated<GenericHandlerDeclaration> declarations = RegistrationValidator.ValidateDeclarations(options.Declarations);

        IReadOnlyList<GenericHandlerClosing> newClosings = registry.AddNewClosings(
            GenericHandlerClosing.Expand(declarations.Valid));
        Validated<Type> closed = RegistrationValidator.ValidateClosings(newClosings);

        IReadOnlyList<Assembly> newAssemblies = registry.AddNewAssemblies(options.Assemblies);
        ScanResult scan = HandlerScanner.Scan(newAssemblies);

        List<HandlerRegistration> handlers = scan.Registrations(closed, options.HandlerLifetime);
        Validated<Type> manualEventHandlers =
            RegistrationValidator.ValidateEventHandlerDeclarations(options.ManualEventHandlers);
        List<EventHandlerRegistration> eventHandlers = scan.EventRegistrations(
            manualEventHandlers, options.ExcludedEventHandlers, options.HandlerLifetime);
        IReadOnlyList<EventHandlerRegistration> newEventHandlers = registry.AddNewEventHandlers(eventHandlers);

        Validated<StageDeclaration> stages = RegistrationValidator.ValidateStageDeclarations(options.StageDeclarations);
        registry.AddStageDeclarations(stages.Valid);

        // Manual event-handler problems come after the stage ones so the report stays in
        // ascending code order, which docs/validation-rules.md promises.
        List<RequestFlowValidationProblem> problems =
            [.. declarations.Problems, .. closed.Problems, .. stages.Problems, .. manualEventHandlers.Problems];
        registry.Add(handlers, scan.RequestTypes, scan.EventTypes, problems);

        RegisterHandlers(services, handlers);
        RegisterEventHandlers(services, newEventHandlers);
        RegisterStages(services, registry);
        RegisterEventStrategies(services, registry);

        services.TryAddSingleton(sp => registry.Freeze(sp));
        services.TryAddSingleton(sp => sp.GetRequiredService<FrozenPlans>().Dispatch);
        services.TryAddSingleton(sp => sp.GetRequiredService<FrozenPlans>().Events);
        services.TryAdd(new ServiceDescriptor(
            typeof(IRequestDispatcher), typeof(RequestDispatcher), options.DispatcherLifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(IStreamDispatcher), typeof(StreamDispatcher), options.DispatcherLifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(IEventPublisher), typeof(EventPublisher), options.DispatcherLifetime));

        return new RequestFlowBuilder(services);
    }

    private static List<HandlerRegistration> Registrations(
        this ScanResult scan, Validated<Type> closed, ServiceLifetime lifetime)
    {
        List<HandlerRegistration> handlers = [];
        foreach (var discovery in scan.Handlers)
            handlers.Add(new HandlerRegistration(discovery, lifetime));

        foreach (var closedType in closed.Valid)
        {
            foreach (var discovery in HandlerScanner.Discover(closedType))
                handlers.Add(new HandlerRegistration(discovery, lifetime));
        }

        return handlers;
    }

    // Exclusions filter the scan only; a manual add is an explicit opt-in and lands either way.
    private static List<EventHandlerRegistration> EventRegistrations(
        this ScanResult scan, Validated<Type> manual, HashSet<Type> excluded, ServiceLifetime lifetime)
    {
        List<EventHandlerRegistration> handlers = [];
        foreach (var discovery in scan.EventHandlersExcept(excluded))
            handlers.Add(new EventHandlerRegistration(discovery, lifetime));

        foreach (var handlerType in manual.Valid)
        {
            foreach (var discovery in HandlerScanner.DiscoverEventHandlers(handlerType))
                handlers.Add(new EventHandlerRegistration(discovery, lifetime));
        }

        return handlers;
    }

    private static RequestFlowRegistry GetOrAddRegistry(IServiceCollection services)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ImplementationInstance is RequestFlowRegistry existing)
                return existing;
        }

        var registry = new RequestFlowRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    private static void RegisterHandlers(IServiceCollection services, IReadOnlyList<HandlerRegistration> handlers)
    {
        foreach (var handler in handlers)
        {
            services.Add(new ServiceDescriptor(
                handler.Contract, handler.ImplementationType, handler.Lifetime));
        }
    }

    private static void RegisterEventHandlers(
        IServiceCollection services, IReadOnlyList<EventHandlerRegistration> handlers)
    {
        HashSet<Type> registered = [];
        foreach (var handler in handlers)
        {
            if (!registered.Add(handler.HandlerType))
                continue;

            services.Add(new ServiceDescriptor(
                handler.HandlerType, handler.HandlerType, handler.Lifetime));
        }
    }

    private static void RegisterStages(IServiceCollection services, RequestFlowRegistry registry)
    {
        foreach (var declaration in registry.StageDeclarations)
        {
            foreach (var handler in registry.Handlers)
            {
                if (!registry.ClosingCache.TryClose(declaration, handler, out Type closedStageType))
                    continue;

                if (!registry.TryAddClosedStageType(closedStageType))
                    continue;

                services.Add(new ServiceDescriptor(closedStageType, closedStageType, declaration.Lifetime));
            }
        }
    }

    private static void RegisterEventStrategies(
        IServiceCollection services, RequestFlowRegistry registry)
    {
        var registered = new HashSet<Type>();
        foreach (EventStrategyDeclaration declaration in registry.EventStrategyDeclarations)
        {
            Type strategyType = declaration.StrategyType;
            if (EventStrategyTypes.IsBuiltIn(strategyType)
                || strategyType.IsInterface
                || strategyType.IsAbstract
                || !registered.Add(strategyType))
            {
                continue;
            }

            services.TryAdd(new ServiceDescriptor(
                strategyType,
                strategyType,
                declaration.Lifetime));
        }
    }
}
