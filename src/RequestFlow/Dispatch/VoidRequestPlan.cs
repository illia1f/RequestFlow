using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Closed plan for one void request with no stages over it: the handler level on its own, resolving
/// the standalone handler from the supplied provider on each call.
/// </summary>
internal sealed class VoidRequestPlan<TRequest> : RequestPlan<NoResult>
    where TRequest : IRequest<NoResult>
{
    private readonly LevelEntry<NoResult> _handler = ChainBuilder.VoidHandler<TRequest>();

    /// <inheritdoc />
    public override Task<NoResult> ExecuteAsync(
        object request, IServiceProvider services, CancellationToken cancellationToken)
        => _handler(request, services, cancellationToken);
}
