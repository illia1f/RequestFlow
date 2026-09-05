using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal abstract class ValueRequestPlan<TResponse> : RequestPlanBase
{
    public sealed override Type ResponseType => typeof(TResponse);

    public abstract ValueTask<TResponse> ExecuteAsync(
        object request,
        IServiceProvider services,
        CancellationToken cancellationToken);
}
