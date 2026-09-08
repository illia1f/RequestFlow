using System;
using System.Collections.Generic;
using System.Threading;

namespace RequestFlow.Cqrs;

/// <summary>
/// Streams a query to its single handler.
/// </summary>
public interface IStreamQueryDispatcher
{
    /// <summary>
    /// Resolves the plan for <paramref name="query"/> and returns its handler's sequence through the applicable stream stages.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="HandlerNotFoundException"/>
    /// <exception cref="ResponseTypeMismatchException"/>
    IAsyncEnumerable<TItem> Stream<TItem>(IStreamQuery<TItem> query, CancellationToken cancellationToken = default);
}
