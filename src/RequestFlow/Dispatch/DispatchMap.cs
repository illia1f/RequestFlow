using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

namespace RequestFlow;

/// <summary>
/// The frozen request-type-to-plan map, built once when the dispatcher is first resolved.
/// Read-only after construction, so lookups are lock-free on every target framework.
/// </summary>
internal sealed class DispatchMap(Dictionary<Type, RequestPlanBase> plans)
{
#if NET8_0_OR_GREATER
    private readonly FrozenDictionary<Type, RequestPlanBase> _plans = plans.ToFrozenDictionary();
#else
    private readonly Dictionary<Type, RequestPlanBase> _plans = plans;
#endif

    public bool TryGet(Type requestType, out RequestPlanBase? plan)
        => _plans.TryGetValue(requestType, out plan);
}
