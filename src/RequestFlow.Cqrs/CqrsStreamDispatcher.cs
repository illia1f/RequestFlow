using System;
using System.Collections.Generic;
using System.Threading;

namespace RequestFlow.Cqrs;

/// <summary>
/// Default <see cref="IStreamQueryDispatcher"/> forwarding to <see cref="IStreamDispatcher"/>.
/// </summary>
internal sealed class CqrsStreamDispatcher(IStreamDispatcher dispatcher) : IStreamQueryDispatcher
{
    private readonly IStreamDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    /// <inheritdoc />
    public IAsyncEnumerable<TItem> Stream<TItem>(IStreamQuery<TItem> query, CancellationToken cancellationToken = default)
    {
        if (query is null)
            throw new ArgumentNullException(nameof(query));

        return _dispatcher.Stream(query, cancellationToken);
    }
}
