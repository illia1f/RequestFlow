using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestFlow;

// Microsoft's own convention for IServiceCollection extensions: AddRequestFlow is visible in Program.cs without an extra using.
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

        DeclarationResult declarations = RegistrationValidator.ValidateDeclarations(options.Declarations);

        IReadOnlyList<GenericHandlerClosing> newClosings = registry.AddNewClosings(Expand(declarations.ValidDeclarations));
        ClosingResult closed = RegistrationValidator.ValidateClosings(newClosings);

        IReadOnlyList<Assembly> newAssemblies = registry.AddNewAssemblies(options.Assemblies);
        ScanResult scan = HandlerScanner.Scan(newAssemblies);

        List<HandlerRegistration> handlers = CollectHandlers(scan, closed);

        StageDeclarationResult stages = RegistrationValidator.ValidateStageDeclarations(options.StageDeclarations);
        registry.AddStageDeclarations(stages.ValidDeclarations);
        if (options.UnusedStagesDisallowed)
            registry.DisallowUnusedStages();

        List<string> problems = [.. declarations.Problems, .. closed.Problems, .. stages.Problems];
        registry.Add(handlers, scan.RequestTypes, problems);

        RegisterHandlers(services, handlers, options.HandlerLifetime);
        RegisterStages(services, registry.StageDeclarations, registry.Handlers, registry.ClosingCache);

        services.TryAddSingleton(_ => registry.BuildDispatchMap());
        services.TryAdd(new ServiceDescriptor(
            typeof(IRequestDispatcher), typeof(RequestDispatcher), options.DispatcherLifetime));

        return new RequestFlowBuilder(services);
    }

    private static List<GenericHandlerClosing> Expand(IReadOnlyList<GenericHandlerDeclaration> declarations)
    {
        List<GenericHandlerClosing> expanded = [];
        foreach (var declaration in declarations)
        {
            expanded.AddRange(GenericHandlerClosing.Expand(declaration));
        }

        return expanded;
    }

    private static List<HandlerRegistration> CollectHandlers(ScanResult scan, ClosingResult closed)
    {
        List<HandlerRegistration> handlers = [.. scan.Handlers];
        foreach (var closedType in closed.ClosedTypes)
        {
            handlers.AddRange(HandlerScanner.CollectHandlers(closedType));
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

    private static void RegisterHandlers(
        IServiceCollection services, IReadOnlyList<HandlerRegistration> handlers, ServiceLifetime lifetime)
    {
        foreach (var handler in handlers)
        {
            Type service = handler.IsVoid
                ? typeof(IRequestHandler<>).MakeGenericType(handler.RequestType)
                : typeof(IRequestHandler<,>).MakeGenericType(handler.RequestType, handler.ResponseType);
            services.Add(new ServiceDescriptor(service, handler.ImplementationType, lifetime));
        }
    }

    // Walks the whole accumulated cross product on every call rather than a delta, so a stage
    // declared by an earlier call reaches requests scanned by a later one; the closing cache
    // makes the repeated pairs cheap. Skipping a type that is already present leaves a stage
    // the consumer registered themselves on its own lifetime; a keyed descriptor is a
    // different service, so it does not count as present.
    private static void RegisterStages(
        IServiceCollection services,
        IReadOnlyList<StageDeclaration> declarations,
        IReadOnlyList<HandlerRegistration> handlers,
        StageClosingCache closings)
    {
        HashSet<Type> registered = [];
        foreach (var descriptor in services)
        {
            if (!descriptor.IsKeyedService)
                registered.Add(descriptor.ServiceType);
        }

        foreach (var declaration in declarations)
        {
            foreach (var handler in handlers)
            {
                if (!closings.TryClose(declaration, handler, out Type closedStageType))
                    continue;

                if (!registered.Add(closedStageType))
                    continue;

                services.Add(new ServiceDescriptor(closedStageType, closedStageType, ServiceLifetime.Transient));
            }
        }
    }
}
