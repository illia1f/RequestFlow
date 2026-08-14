using System;
using System.Collections.Generic;
using System.Threading;

namespace RequestFlow;

/// <summary>
/// Closed plan for one stream request with no stages over it: the handler level on its own,
/// resolving the handler from the supplied provider on each call.
/// </summary>
internal sealed class StreamPlan<TRequest, TItem> : StreamPlan<TItem>
    where TRequest : IStreamRequest<TItem>
{
    private readonly StreamLevelEntry<TItem> _handler = StreamChainBuilder.Handler<TRequest, TItem>();

    /// <inheritdoc />
    public override IAsyncEnumerable<TItem> Execute(
        object request, IServiceProvider services, CancellationToken cancellationToken)
        => _handler(request, services, cancellationToken);
}
