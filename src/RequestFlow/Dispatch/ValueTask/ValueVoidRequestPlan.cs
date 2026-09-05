using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal abstract class ValueVoidRequestPlan : ValueRequestPlan<NoResult>
{
    public abstract ValueTask ExecuteVoidAsync(
        object request,
        IServiceProvider services,
        CancellationToken cancellationToken);

    public sealed override ValueTask<NoResult> ExecuteAsync(
        object request,
        IServiceProvider services,
        CancellationToken cancellationToken)
        => ValueNoResultBridge.Complete(
            ExecuteVoidAsync(request, services, cancellationToken));
}

internal sealed class ValueVoidRequestPlan<TRequest> : ValueVoidRequestPlan
    where TRequest : IValueRequest<NoResult>
{
    private readonly ValueVoidLevelEntry _handler =
        ValueChainBuilder.VoidHandler<TRequest>();

    public override ValueTask ExecuteVoidAsync(
        object request,
        IServiceProvider services,
        CancellationToken cancellationToken)
        => _handler(request, services, cancellationToken);
}
