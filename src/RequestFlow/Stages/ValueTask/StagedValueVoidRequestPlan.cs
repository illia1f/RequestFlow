using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal sealed class StagedValueVoidRequestPlan<TRequest>(StageChain chain)
    : ValueVoidRequestPlan
    where TRequest : IValueRequest<NoResult>
{
    private readonly ValueVoidLevelEntry _root = ValueChainBuilder.Void<TRequest>(chain);

    public override ValueTask ExecuteVoidAsync(
        object request,
        IServiceProvider services,
        CancellationToken cancellationToken)
        => _root(request, services, cancellationToken);
}
