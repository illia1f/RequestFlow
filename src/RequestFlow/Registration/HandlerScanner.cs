using System;
using System.Collections.Generic;
using System.Reflection;

namespace RequestFlow;

/// <summary>
/// Reflection scan over the configured assemblies for handlers and request types,
/// run once inside <c>AddRequestFlow</c>.
/// </summary>
internal static class HandlerScanner
{
    public static ScanResult Scan(IReadOnlyList<Assembly> assemblies)
    {
        List<HandlerDiscovery> handlers = [];
        List<Type> requestTypes = [];

        foreach (var assembly in assemblies)
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                if (type is null || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                    continue;

                handlers.AddRange(Discover(type));

                if (IsRequestType(type))
                    requestTypes.Add(type);
            }
        }

        return new ScanResult(handlers, requestTypes);
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

    internal static List<HandlerDiscovery> Discover(Type type)
    {
        List<HandlerDiscovery> handlers = [];

        foreach (var iface in type.GetInterfaces())
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

    private static bool IsRequestType(Type type)
    {
        foreach (var iface in type.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(IRequest<>) || definition == typeof(IStreamRequest<>))
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
    /// The open definition of <see cref="Contract"/>, which is what tells one handler family from
    /// another.
    /// </summary>
    public Type ContractDefinition { get; } = contract.GetGenericTypeDefinition();
}

/// <summary>
/// Handlers and request types discovered by one scan pass.
/// </summary>
internal sealed class ScanResult(IReadOnlyList<HandlerDiscovery> handlers, IReadOnlyList<Type> requestTypes)
{
    public IReadOnlyList<HandlerDiscovery> Handlers { get; } = handlers;

    /// <summary>
    /// Every discovered request type, handled or not; validation reports the difference.
    /// </summary>
    public IReadOnlyList<Type> RequestTypes { get; } = requestTypes;
}
