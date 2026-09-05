using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal sealed class ValueRequestPlan<TRequest, TResponse> : ValueRequestPlan<TResponse>
    where TRequest : IValueRequest<TResponse>
{
    private readonly ValueLevelEntry<TResponse> _handler =
        ValueChainBuilder.TypedHandler<TRequest, TResponse>();

    public override ValueTask<TResponse> ExecuteAsync(
        object request,
        IServiceProvider services,
        CancellationToken cancellationToken)
        => _handler(request, services, cancellationToken);
}
