using System;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Translates a container lifetime into the lifetime the validation model carries.
/// </summary>
/// <remarks>
/// The model ships in the dependency-free abstractions package, so it cannot name
/// <see cref="ServiceLifetime"/>. An unknown value throws rather than passing a wrong lifetime to a rule.
/// </remarks>
internal static class ModelLifetime
{
    /// <exception cref="ArgumentOutOfRangeException"/>
    public static RequestFlowLifetime Of(ServiceLifetime lifetime)
        => lifetime switch
        {
            ServiceLifetime.Transient => RequestFlowLifetime.Transient,
            ServiceLifetime.Scoped => RequestFlowLifetime.Scoped,
            ServiceLifetime.Singleton => RequestFlowLifetime.Singleton,
            _ => throw new ArgumentOutOfRangeException(
                nameof(lifetime), lifetime, $"Unknown service lifetime '{lifetime}'."),
        };
}
