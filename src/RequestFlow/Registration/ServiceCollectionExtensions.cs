using System;
using System.Collections.Generic;
using System.Reflection;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
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
    /// Registers handlers, dispatchers, and the event publisher.
    /// Calls are additive; previously registered assemblies and generic closings are skipped.
    /// First dispatcher or publisher resolution, <c>ValidateRequestFlow</c>, or <c>InspectRequestFlow</c> validates and freezes the maps once per provider.
    /// Invalid registrations produce one <see cref="RequestFlowValidationException"/> listing every problem.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="RequestFlowValidationException"/>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(DeploymentWarnings.Trimming)]
    [RequiresDynamicCode(DeploymentWarnings.NativeAot)]
#endif
    public static RequestFlowBuilder AddRequestFlow(
        this IServiceCollection services, Action<RequestFlowOptions> configure)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        RequestFlowOptions options = new RequestFlowOptions().Apply(configure);

        RequestFlowRegistry registry = GetOrAddRegistry(services);
        registry.AddExemptions(options.Exemptions);
        if (options.AllUnhandledRequestsAllowed)
            registry.AllowAllUnhandledRequests();
        registry.AddEventStrategyDeclarations(options.EventStrategyDeclarations);
        if (options.AllUnhandledEventsAllowed)
            registry.AllowAllUnhandledEvents();
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

        Validated<Type> manualHandlers =
            RegistrationValidator.ValidateManualHandlerDeclarations(options.ManualHandlers);
        List<HandlerRegistration> handlers = scan.BuildHandlerRegistrations(
            closed, manualHandlers.Valid, options.ExcludedHandlers, options.HandlerLifetime);
        IReadOnlyList<HandlerRegistration> newHandlers = registry.AddNewHandlers(handlers);
        Validated<Type> manualEventHandlers =
            RegistrationValidator.ValidateEventHandlerDeclarations(options.ManualEventHandlers);
        List<EventHandlerRegistration> eventHandlers = scan.BuildEventHandlerRegistrations(
            manualEventHandlers, options.ExcludedEventHandlers, options.HandlerLifetime);
        IReadOnlyList<EventHandlerRegistration> newEventHandlers = registry.AddNewEventHandlers(eventHandlers);

        Validated<StageDeclaration> stages = RegistrationValidator.ValidateStageDeclarations(options.StageDeclarations);
        registry.AddStageDeclarations(stages.Valid);

        Validated<Type> manualEvents = RegistrationValidator.ValidateEventDeclarations(options.ManualEvents);

        List<RequestFlowValidationProblem> problems =
            [.. declarations.Problems, .. closed.Problems, .. stages.Problems,
             .. manualEventHandlers.Problems, .. manualHandlers.Problems, .. manualEvents.Problems];
        List<Type> requestTypes =
            [.. scan.RequestTypes, .. scan.GetRequestTypesHandledBy(options.ExcludedHandlers)];
        List<Type> eventTypes = [.. scan.EventTypes, .. manualEvents.Valid];
        registry.Add(requestTypes, eventTypes, problems);

        RegisterHandlers(services, newHandlers);
        RegisterEventHandlers(services, newEventHandlers);
        RegisterStages(services, registry);
        RegisterEventStrategies(services, registry);

        services.TryAddSingleton(sp => registry.Freeze(sp));
        services.TryAddSingleton(sp => sp.GetRequiredService<FrozenPlans>().Dispatch);
        services.TryAddSingleton(sp => sp.GetRequiredService<FrozenPlans>().Events);
        services.TryAdd(new ServiceDescriptor(
            typeof(IRequestDispatcher), typeof(RequestDispatcher), options.DispatcherLifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(IValueRequestDispatcher), typeof(ValueRequestDispatcher), options.DispatcherLifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(IStreamDispatcher), typeof(StreamDispatcher), options.DispatcherLifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(IEventPublisher), typeof(EventPublisher), options.DispatcherLifetime));

        return new RequestFlowBuilder(services);
    }

    private static List<HandlerRegistration> BuildHandlerRegistrations(
        this ScanResult scan,
        Validated<Type> closed,
        IReadOnlyList<Type> manual,
        HashSet<Type> excluded,
        ServiceLifetime lifetime)
    {
        List<HandlerRegistration> handlers = [];
        foreach (var discovery in scan.GetHandlersExcept(excluded))
            handlers.Add(new HandlerRegistration(discovery, lifetime));

        foreach (var closedType in closed.Valid)
        {
            foreach (var discovery in HandlerScanner.Discover(closedType))
                handlers.Add(new HandlerRegistration(discovery, lifetime));
        }

        foreach (var handlerType in manual)
        {
            foreach (var discovery in HandlerScanner.Discover(handlerType))
                handlers.Add(new HandlerRegistration(discovery, lifetime));
        }

        return handlers;
    }

    // Exclusions filter the scan only; a manual add is an explicit opt-in and lands either way.
    private static List<EventHandlerRegistration> BuildEventHandlerRegistrations(
        this ScanResult scan, Validated<Type> manual, HashSet<Type> excluded, ServiceLifetime lifetime)
    {
        List<EventHandlerRegistration> handlers = [];
        foreach (var discovery in scan.GetEventHandlersExcept(excluded))
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
