using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace RequestFlow;

/// <summary>
/// Reflection scan over the configured assemblies for handlers, requests, and events,
/// run once inside <c>AddRequestFlow</c>.
/// </summary>
internal static class HandlerScanner
{
    public static ScanResult Scan(IReadOnlyList<Assembly> assemblies)
    {
        List<HandlerDiscovery> handlers = [];
        List<Type> requestTypes = [];
        List<EventHandlerDiscovery> eventHandlers = [];
        List<Type> eventTypes = [];

        foreach (var assembly in assemblies)
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                if (type is null || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                    continue;

                Type[]? interfaces = GetLoadableInterfaces(type);
                if (interfaces is null)
                    continue;

                handlers.AddRange(Discover(type, interfaces));
                eventHandlers.AddRange(DiscoverEventHandlers(type, interfaces));

                if (ContainsRequestContract(interfaces))
                    requestTypes.Add(type);

                if (ContainsEventContract(interfaces))
                    eventTypes.Add(type);
            }
        }

        return new ScanResult(handlers, requestTypes, eventHandlers, eventTypes);
    }

    private static Type?[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // Failed slots in exception.Types are null; the loaded types are still scannable.
            return exception.Types;
        }
    }

    private static Type[]? GetLoadableInterfaces(Type type)
    {
        try
        {
            return type.GetInterfaces();
        }
        // An interface from an undeployed assembly fails the load, not just the type; skip the type.
        catch (Exception exception) when (
            exception is TypeLoadException
                or FileNotFoundException
                or FileLoadException
                or BadImageFormatException)
        {
            return null;
        }
    }

    internal static List<HandlerDiscovery> Discover(Type type)
        => Discover(type, type.GetInterfaces());

    internal static List<EventHandlerDiscovery> DiscoverEventHandlers(Type type)
        => DiscoverEventHandlers(type, type.GetInterfaces());

    private static List<HandlerDiscovery> Discover(Type type, Type[] interfaces)
    {
        List<HandlerDiscovery> handlers = [];

        foreach (var iface in interfaces)
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(IRequestHandler<,>)
                || definition == typeof(IStreamRequestHandler<,>))
            {
                Type[] args = iface.GetGenericArguments();
                handlers.Add(new HandlerDiscovery(type, args[0], args[1], isVoid: false, iface));
            }
            else if (definition == typeof(IRequestHandler<>))
            {
                Type[] args = iface.GetGenericArguments();
                handlers.Add(new HandlerDiscovery(type, args[0], typeof(NoResult), isVoid: true, iface));
            }
        }

        return handlers;
    }

    private static bool ContainsRequestContract(Type[] interfaces)
    {
        foreach (var iface in interfaces)
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(IRequest<>) || definition == typeof(IStreamRequest<>))
                return true;
        }

        return false;
    }

    private static List<EventHandlerDiscovery> DiscoverEventHandlers(Type type, Type[] interfaces)
    {
        List<EventHandlerDiscovery> eventHandlers = [];

        foreach (var iface in interfaces)
        {
            if (!iface.IsGenericType
                || iface.GetGenericTypeDefinition() != typeof(IEventHandler<>))
            {
                continue;
            }

            eventHandlers.Add(new EventHandlerDiscovery(
                type, iface.GetGenericArguments()[0]));
        }

        return eventHandlers;
    }

    private static bool ContainsEventContract(Type[] interfaces)
    {
        foreach (var iface in interfaces)
        {
            if (iface == typeof(IEvent))
                return true;
        }

        return false;
    }
}

/// <summary>
/// One handler the scan found, before registration decides how it lives.
/// </summary>
internal sealed class HandlerDiscovery(
    Type implementationType, Type requestType, Type responseType, bool isVoid, Type contract)
{
    /// <summary>
    /// The concrete handler class discovered by the scan.
    /// </summary>
    public Type ImplementationType { get; } = implementationType;

    /// <summary>
    /// The closed request type the handler handles.
    /// </summary>
    public Type RequestType { get; } = requestType;

    /// <summary>
    /// The response type; <see cref="NoResult"/> for void handlers.
    /// </summary>
    public Type ResponseType { get; } = responseType;

    /// <summary>
    /// True when the handler implements <see cref="IRequestHandler{TRequest}"/>.
    /// </summary>
    public bool IsVoid { get; } = isVoid;

    /// <summary>
    /// The closed core contract the scan matched this handler through.
    /// </summary>
    public Type Contract { get; } = contract;

    /// <summary>
    /// The open definition of <see cref="Contract"/>, which is what tells one handler family from another.
    /// </summary>
    public Type ContractDefinition { get; } = contract.GetGenericTypeDefinition();
}

internal sealed class EventHandlerDiscovery(Type handlerType, Type declaredEventType)
{
    public Type HandlerType { get; } = handlerType;

    public Type DeclaredEventType { get; } = declaredEventType;
}

internal sealed class ScanResult(
    IReadOnlyList<HandlerDiscovery> handlers,
    IReadOnlyList<Type> requestTypes,
    IReadOnlyList<EventHandlerDiscovery> eventHandlers,
    IReadOnlyList<Type> eventTypes)
{
    public IReadOnlyList<HandlerDiscovery> Handlers { get; } = handlers;

    /// <summary>
    /// Every discovered request type, handled or not; validation reports the difference.
    /// </summary>
    public IReadOnlyList<Type> RequestTypes { get; } = requestTypes;

    public IReadOnlyList<EventHandlerDiscovery> EventHandlers { get; } = eventHandlers;

    public IReadOnlyList<Type> EventTypes { get; } = eventTypes;

    public IEnumerable<EventHandlerDiscovery> EventHandlersExcept(HashSet<Type> excludedHandlerTypes)
    {
        foreach (var discovery in EventHandlers)
        {
            if (!excludedHandlerTypes.Contains(discovery.HandlerType))
                yield return discovery;
        }
    }
}
