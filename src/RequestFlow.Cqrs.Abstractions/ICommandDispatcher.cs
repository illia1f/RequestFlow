using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow.Cqrs;

/// <summary>
/// Sends a command to its single handler. Injection sites holding this interface
/// can send commands only, never queries.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Dispatches <paramref name="command"/> to its handler and returns the response.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="HandlerNotFoundException"/>
    /// <exception cref="ResponseTypeMismatchException"/>
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a void <paramref name="command"/> to its handler.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="HandlerNotFoundException"/>
    /// <exception cref="ResponseTypeMismatchException"/>
    Task SendAsync(ICommand command, CancellationToken cancellationToken = default);
}
