using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow.Cqrs;

/// <summary>
/// Sends ValueTask commands to their handlers.
/// </summary>
public interface IValueCommandDispatcher
{
    /// <summary>
    /// Dispatches <paramref name="command"/> to its handler and returns the response.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="HandlerNotFoundException"/>
    /// <exception cref="ResponseTypeMismatchException"/>
    ValueTask<TResponse> SendAsync<TResponse>(
        IValueCommand<TResponse> command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a void <paramref name="command"/> to its handler.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="HandlerNotFoundException"/>
    /// <exception cref="ResponseTypeMismatchException"/>
    ValueTask SendAsync(
        IValueCommand command,
        CancellationToken cancellationToken = default);
}
