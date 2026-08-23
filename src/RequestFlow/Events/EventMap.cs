using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

namespace RequestFlow;

internal sealed class EventMap(Dictionary<Type, EventPlan> plans)
{
#if NET8_0_OR_GREATER
    private readonly FrozenDictionary<Type, EventPlan> _plans = plans.ToFrozenDictionary();
#else
    private readonly Dictionary<Type, EventPlan> _plans = plans;
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetPlanFor(Type eventType, out EventPlan? plan)
        => _plans.TryGetValue(eventType, out plan);
}
