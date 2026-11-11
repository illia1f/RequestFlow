using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow.Cqrs;

internal sealed class CqrsValueQueryDispatcher(IValueRequestDispatcher dispatcher)
    : IValueQueryDispatcher
{
    private readonly IValueRequestDispatcher _dispatcher =
        dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    /// <inheritdoc />
    public ValueTask<TResponse> SendAsync<TResponse>(
        IValueQuery<TResponse> query,
        CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(query);

        return _dispatcher.SendAsync(query, cancellationToken);
    }
}
