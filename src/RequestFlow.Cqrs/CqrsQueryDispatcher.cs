using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow.Cqrs;

internal sealed class CqrsQueryDispatcher(IRequestDispatcher dispatcher) : IQueryDispatcher
{
    private readonly IRequestDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    /// <inheritdoc />
    public Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(query);

        return _dispatcher.SendAsync(query, cancellationToken);
    }
}
