using System;

namespace RequestFlow;

internal static class EventTypeSpecificity
{
    public const int ExactMatchTier = 0;
    public const int BaseClassTier = 1;
    public const int EventInterfaceTier = 2;
    public const int CatchAllEventTier = 3;

    public static int Tier(Type eventType, Type declaredEventType)
    {
        if (declaredEventType == eventType)
            return ExactMatchTier;
        if (declaredEventType == typeof(IEvent))
            return CatchAllEventTier;

        return declaredEventType.IsInterface ? EventInterfaceTier : BaseClassTier;
    }

    public static int DeliverySpecificity(Type eventType, Type declaredEventType)
    {
        if (declaredEventType.IsInterface)
            return -declaredEventType.GetInterfaces().Length;

        return ClassDistance(eventType, declaredEventType);
    }

    private static int ClassDistance(Type eventType, Type declaredEventType)
    {
        int distance = 0;
        for (Type? current = eventType; current is not null; current = current.BaseType)
        {
            if (current == declaredEventType)
                return distance;

            distance++;
        }

        return int.MaxValue;
    }
}
