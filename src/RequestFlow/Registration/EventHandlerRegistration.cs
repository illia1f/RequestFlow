using System;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

internal sealed class EventHandlerRegistration(
    EventHandlerDiscovery discovery,
    ServiceLifetime lifetime)
{
    public Type HandlerType { get; } = discovery.HandlerType;

    public Type DeclaredEventType { get; } = discovery.DeclaredEventType;

    public ServiceLifetime Lifetime { get; } = lifetime;
}
