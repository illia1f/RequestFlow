using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow.Cqrs;

internal sealed class CqrsCommandDispatcher(IRequestDispatcher dispatcher) : ICommandDispatcher
{
    private readonly IRequestDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    /// <inheritdoc />
    public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(command);

        return _dispatcher.SendAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(command);

        return _dispatcher.SendAsync(command, cancellationToken);
    }
}
