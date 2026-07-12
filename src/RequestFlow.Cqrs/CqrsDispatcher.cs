using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow.Cqrs;

/// <summary>
/// Default <see cref="ICommandDispatcher"/> and <see cref="IQueryDispatcher"/>
/// forwarding to <see cref="IRequestDispatcher"/>.
/// </summary>
internal sealed class CqrsDispatcher(IRequestDispatcher dispatcher) : ICommandDispatcher, IQueryDispatcher
{
    private readonly IRequestDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    /// <inheritdoc />
    public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        return _dispatcher.SendAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        return _dispatcher.SendAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        if (query is null)
            throw new ArgumentNullException(nameof(query));

        return _dispatcher.SendAsync(query, cancellationToken);
    }
}
