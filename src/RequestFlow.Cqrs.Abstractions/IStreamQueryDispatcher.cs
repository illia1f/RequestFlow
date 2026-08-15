using System;
using System.Collections.Generic;
using System.Threading;

namespace RequestFlow.Cqrs;

/// <summary>
/// Streams a query to its single handler. Injection sites holding this interface
/// can stream queries only, never send commands.
/// </summary>
public interface IStreamQueryDispatcher
{
    /// <summary>
    /// Dispatches <paramref name="query"/> to its handler and returns the sequence it produces,
    /// wrapped in whatever stream stages apply. The lookup happens on this call; the handler
    /// runs on the first enumeration.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="HandlerNotFoundException"/>
    /// <exception cref="ResponseTypeMismatchException"/>
    IAsyncEnumerable<TItem> Stream<TItem>(IStreamQuery<TItem> query, CancellationToken cancellationToken = default);
}
