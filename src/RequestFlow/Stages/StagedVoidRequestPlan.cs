using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Closed plan for one void request wrapped in stages. The chain of levels, and the contract shape
/// each one runs under, are settled when the dispatch map freezes; each level resolves its stage or
/// handler from the supplied provider when it runs.
/// </summary>
internal sealed class StagedVoidRequestPlan<TRequest>(StageChain chain) : RequestPlan<NoResult>
    where TRequest : IRequest<NoResult>
{
    private readonly LevelEntry<NoResult> _root = ChainBuilder.Void<TRequest>(chain);

    /// <inheritdoc />
    public override Task<NoResult> ExecuteAsync(
        object request, IServiceProvider services, CancellationToken cancellationToken)
        => _root(request, services, cancellationToken);
}
