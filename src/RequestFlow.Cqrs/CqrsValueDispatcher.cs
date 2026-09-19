using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow.Cqrs;

internal sealed class CqrsValueDispatcher(IValueRequestDispatcher dispatcher)
    : IValueCommandDispatcher, IValueQueryDispatcher
{
    private readonly IValueRequestDispatcher _dispatcher =
        dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    /// <inheritdoc />
    public ValueTask<TResponse> SendAsync<TResponse>(
        IValueCommand<TResponse> command,
        CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(command);

        return _dispatcher.SendAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SendAsync(
        IValueCommand command,
        CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(command);

        return _dispatcher.SendAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<TResponse> SendAsync<TResponse>(
        IValueQuery<TResponse> query,
        CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(query);

        return _dispatcher.SendAsync(query, cancellationToken);
    }
}
