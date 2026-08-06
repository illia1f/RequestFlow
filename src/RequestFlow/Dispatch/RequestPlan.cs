using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Closed plan for one request/response pair with no stages over it: the handler level on its own,
/// resolving the handler from the supplied provider on each call.
/// </summary>
internal sealed class RequestPlan<TRequest, TResponse> : RequestPlan<TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly LevelEntry<TResponse> _handler = ChainBuilder.TypedHandler<TRequest, TResponse>();

    /// <inheritdoc />
    public override Task<TResponse> ExecuteAsync(
        object request, IServiceProvider services, CancellationToken cancellationToken)
        => _handler(request, services, cancellationToken);
}
