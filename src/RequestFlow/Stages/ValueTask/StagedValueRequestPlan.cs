using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal sealed class StagedValueRequestPlan<TRequest, TResponse>(StageChain chain)
    : ValueRequestPlan<TResponse>
    where TRequest : IValueRequest<TResponse>
{
    private readonly ValueLevelEntry<TResponse> _root =
        ValueChainBuilder.Typed<TRequest, TResponse>(chain);

    public override ValueTask<TResponse> ExecuteAsync(
        object request,
        IServiceProvider services,
        CancellationToken cancellationToken)
        => _root(request, services, cancellationToken);
}
