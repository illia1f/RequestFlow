using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Closed plan for one request/response pair wrapped in stages. The chain of levels is built when
/// the dispatch map freezes; each level resolves its stage or handler from the supplied provider
/// when it runs.
/// </summary>
internal sealed class StagedRequestPlan<TRequest, TResponse>(StageChain chain) : RequestPlan<TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly LevelEntry<TResponse> _root = ChainBuilder.Typed<TRequest, TResponse>(chain);

    /// <inheritdoc />
    public override Task<TResponse> ExecuteAsync(
        object request, IServiceProvider services, CancellationToken cancellationToken)
        => _root(request, services, cancellationToken);
}
