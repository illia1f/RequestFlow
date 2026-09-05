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
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        return _dispatcher.SendAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SendAsync(
        IValueCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        return _dispatcher.SendAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<TResponse> SendAsync<TResponse>(
        IValueQuery<TResponse> query,
        CancellationToken cancellationToken = default)
    {
        if (query is null)
            throw new ArgumentNullException(nameof(query));

        return _dispatcher.SendAsync(query, cancellationToken);
    }
}
