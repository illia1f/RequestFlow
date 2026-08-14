using System;
using System.Collections.Generic;
using System.Threading;

namespace RequestFlow;

/// <summary>
/// Plan typed over the item only, so the dispatcher can enter it without knowing the concrete
/// request type. Stream plans sit in the same map as request plans.
/// </summary>
internal abstract class StreamPlan<TItem> : RequestPlanBase
{
    /// <inheritdoc />
    public sealed override Type ResponseType => typeof(TItem);

    /// <summary>
    /// Enters the top level of this plan's chain, which is the handler itself when no stage applies.
    /// </summary>
    public abstract IAsyncEnumerable<TItem> Execute(
        object request, IServiceProvider services, CancellationToken cancellationToken);
}
