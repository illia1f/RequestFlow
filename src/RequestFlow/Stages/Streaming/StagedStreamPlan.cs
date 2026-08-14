using System;
using System.Collections.Generic;
using System.Threading;

namespace RequestFlow;

/// <summary>
/// Closed plan for one stream request wrapped in stages. The chain of levels is built when the
/// dispatch map freezes; each level resolves its stage or handler from the supplied provider when
/// it runs.
/// </summary>
internal sealed class StagedStreamPlan<TRequest, TItem>(StageChain chain) : StreamPlan<TItem>
    where TRequest : IStreamRequest<TItem>
{
    private readonly StreamLevelEntry<TItem> _root = StreamChainBuilder.Build<TRequest, TItem>(chain);

    /// <inheritdoc />
    public override IAsyncEnumerable<TItem> Execute(
        object request, IServiceProvider services, CancellationToken cancellationToken)
        => _root(request, services, cancellationToken);
}
