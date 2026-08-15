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
    /// earlier call are skipped. The dispatch map is validated and built once per provider,
    /// on its first dispatcher resolution, throwing a
    /// <see cref="RequestFlowValidationException"/> that lists every registration problem.
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

        Validated<GenericHandlerDeclaration> declarations = RegistrationValidator.ValidateDeclarations(options.Declarations);

        IReadOnlyList<GenericHandlerClosing> newClosings = registry.AddNewClosings(
            GenericHandlerClosing.Expand(declarations.Valid));
        Validated<Type> closed = RegistrationValidator.ValidateClosings(newClosings);

        IReadOnlyList<Assembly> newAssemblies = registry.AddNewAssemblies(options.Assemblies);
        ScanResult scan = HandlerScanner.Scan(newAssemblies);

        List<HandlerRegistration> handlers = scan.Registrations(closed, options.HandlerLifetime);

        Validated<StageDeclaration> stages = RegistrationValidator.ValidateStageDeclarations(options.StageDeclarations);
        registry.AddStageDeclarations(stages.Valid);
        if (options.UnusedStagesDisallowed)
            registry.DisallowUnusedStages();

        List<RequestFlowValidationProblem> problems = [.. declarations.Problems, .. closed.Problems, .. stages.Problems];
        registry.Add(handlers, scan.RequestTypes, problems);

        RegisterHandlers(services, handlers);
        RegisterStages(services, registry);

        services.TryAddSingleton(sp => registry.BuildDispatchMap(sp));
        services.TryAdd(new ServiceDescriptor(
            typeof(IRequestDispatcher), typeof(RequestDispatcher), options.DispatcherLifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(IStreamDispatcher), typeof(StreamDispatcher), options.DispatcherLifetime));

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
}
