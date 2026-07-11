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
        List<HandlerRegistration> handlers = [];
        List<Type> requestTypes = [];

        foreach (var assembly in assemblies)
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                if (type is null || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                    continue;

                handlers.AddRange(CollectHandlers(type));

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

    internal static List<HandlerRegistration> CollectHandlers(Type type)
    {
        List<HandlerRegistration> handlers = [];

        foreach (var iface in type.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(IRequestHandler<,>))
            {
                Type[] args = iface.GetGenericArguments();
                handlers.Add(new HandlerRegistration(type, args[0], args[1], isVoid: false));
            }
            else if (definition == typeof(IRequestHandler<>))
            {
                Type[] args = iface.GetGenericArguments();
                handlers.Add(new HandlerRegistration(type, args[0], typeof(NoResult), isVoid: true));
            }
        }

        return handlers;
    }

    private static bool IsRequestType(Type type)
    {
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IRequest<>))
                return true;
        }

        return false;
    }
}
