using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

namespace RequestFlow;

internal sealed class DispatchMap(Dictionary<Type, RequestPlanBase> plans)
{
#if NET8_0_OR_GREATER
    private readonly FrozenDictionary<Type, RequestPlanBase> _plans = plans.ToFrozenDictionary();
#else
    private readonly Dictionary<Type, RequestPlanBase> _plans = plans;
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetPlanFor(Type requestType, out RequestPlanBase? plan)
        => _plans.TryGetValue(requestType, out plan);
}
