using System;
using System.Collections.Generic;
using System.Reflection;

namespace RequestFlow;

internal sealed class UnhandledMessageExemptions
{
    internal List<Type> RequestTypes { get; } = [];
    internal List<Assembly> RequestAssemblies { get; } = [];
    internal List<Type> EventTypes { get; } = [];
    internal List<Assembly> EventAssemblies { get; } = [];

    internal void AddRequest(Type requestType)
    {
        if (requestType is null)
            throw new ArgumentNullException(nameof(requestType));

        bool isRequest = false;
        foreach (Type contract in requestType.GetInterfaces())
        {
            if (!contract.IsGenericType)
                continue;
            Type definition = contract.GetGenericTypeDefinition();
            if (definition == typeof(IRequest<>) || definition == typeof(IValueRequest<>)
                || definition == typeof(IStreamRequest<>))
            {
                isRequest = true;
                break;
            }
        }

        if (!isRequest || requestType.IsAbstract || requestType.IsInterface || requestType.ContainsGenericParameters)
            throw new ArgumentException("Name a concrete, closed Task, ValueTask, or stream request type.", nameof(requestType));

        AddUnique(RequestTypes, requestType);
    }

    internal void AddEvent(Type eventType)
    {
        if (eventType is null)
            throw new ArgumentNullException(nameof(eventType));
        if (!EventClosure.IsConcreteClosedEvent(eventType))
            throw new ArgumentException("Name a concrete, closed event type.", nameof(eventType));

        AddUnique(EventTypes, eventType);
    }

    internal void AddRequestAssembly(Assembly assembly)
        => AddUnique(RequestAssemblies, assembly ?? throw new ArgumentNullException(nameof(assembly)));

    internal void AddEventAssembly(Assembly assembly)
        => AddUnique(EventAssemblies, assembly ?? throw new ArgumentNullException(nameof(assembly)));

    internal void Add(UnhandledMessageExemptions other)
    {
        foreach (Type type in other.RequestTypes)
            AddUnique(RequestTypes, type);
        foreach (Assembly assembly in other.RequestAssemblies)
            AddUnique(RequestAssemblies, assembly);
        foreach (Type type in other.EventTypes)
            AddUnique(EventTypes, type);
        foreach (Assembly assembly in other.EventAssemblies)
            AddUnique(EventAssemblies, assembly);
    }

    private static void AddUnique<T>(List<T> values, T value)
    {
        if (!values.Contains(value))
            values.Add(value);
    }
}
