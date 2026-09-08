using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace RequestFlow;

/// <summary>
/// The frozen registration and options shared by all validation rules.
/// </summary>
/// <remarks>
/// Build test contexts with <see cref="RequestFlowModelBuilder"/>.
/// </remarks>
public sealed class RequestFlowValidationContext
{
    /// <exception cref="ArgumentNullException"/>
    internal RequestFlowValidationContext(
        RequestFlowModel model,
        bool allUnhandledRequestsAllowed,
        bool unusedStagesDisallowed,
        bool allUnhandledEventsAllowed,
        bool unusedEventHandlersDisallowed,
        UnhandledMessageExemptions? exemptions = null)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        AllUnhandledRequestsAllowed = allUnhandledRequestsAllowed;
        UnusedStagesDisallowed = unusedStagesDisallowed;
        AllUnhandledEventsAllowed = allUnhandledEventsAllowed;
        UnusedEventHandlersDisallowed = unusedEventHandlersDisallowed;
        exemptions ??= new UnhandledMessageExemptions();
        UnhandledRequestTypes = new ReadOnlyCollection<Type>(exemptions.RequestTypes.ToArray());
        UnhandledRequestAssemblies = new ReadOnlyCollection<Assembly>(exemptions.RequestAssemblies.ToArray());
        UnhandledEventTypes = new ReadOnlyCollection<Type>(exemptions.EventTypes.ToArray());
        UnhandledEventAssemblies = new ReadOnlyCollection<Assembly>(exemptions.EventAssemblies.ToArray());
    }

    /// <summary>
    /// The frozen request, stage, and event registration.
    /// </summary>
    public RequestFlowModel Model { get; }

    /// <summary>
    /// True when missing handlers are permitted for every request.
    /// Selected exemptions do not set this flag.
    /// </summary>
    public bool AllUnhandledRequestsAllowed { get; }

    /// <summary>
    /// True when a stage that reaches no request is a validation error.
    /// </summary>
    public bool UnusedStagesDisallowed { get; }

    /// <summary>
    /// True when missing handlers are permitted for every known event.
    /// Selected exemptions do not set this flag.
    /// </summary>
    public bool AllUnhandledEventsAllowed { get; }

    /// <summary>
    /// True when an event subscription that reaches no known event is a problem.
    /// </summary>
    public bool UnusedEventHandlersDisallowed { get; }

    /// <summary>
    /// Exact request types permitted to have no handler.
    /// </summary>
    public IReadOnlyList<Type> UnhandledRequestTypes { get; }

    /// <summary>
    /// Assemblies whose request types are permitted to have no handler.
    /// </summary>
    public IReadOnlyList<Assembly> UnhandledRequestAssemblies { get; }

    /// <summary>
    /// Exact event types permitted to have no handler.
    /// </summary>
    public IReadOnlyList<Type> UnhandledEventTypes { get; }

    /// <summary>
    /// Assemblies whose event types are permitted to have no handler.
    /// </summary>
    public IReadOnlyList<Assembly> UnhandledEventAssemblies { get; }

    /// <summary>
    /// Whether the global policy or a selected exemption permits this request to have no handler.
    /// </summary>
    public bool AllowsUnhandledRequest(Type requestType)
    {
        if (requestType is null)
            throw new ArgumentNullException(nameof(requestType));
        return AllUnhandledRequestsAllowed || Matches(requestType, UnhandledRequestTypes, UnhandledRequestAssemblies);
    }

    /// <summary>
    /// Whether the global policy or a selected exemption permits this event to have no handler.
    /// </summary>
    public bool AllowsUnhandledEvent(Type eventType)
    {
        if (eventType is null)
            throw new ArgumentNullException(nameof(eventType));
        return AllUnhandledEventsAllowed || Matches(eventType, UnhandledEventTypes, UnhandledEventAssemblies);
    }

    private static bool Matches(Type type, IReadOnlyList<Type> types, IReadOnlyList<Assembly> assemblies)
    {
        foreach (Type allowed in types)
        {
            if (allowed == type)
                return true;
        }
        foreach (Assembly assembly in assemblies)
        {
            if (assembly == type.Assembly)
                return true;
        }
        return false;
    }
}
