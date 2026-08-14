using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Enters one level of a frozen stage chain: a stage, or the handler beneath the last one. 
/// A <see cref="Continuation{TResponse}"/> holds one of these and calls it again on every <see cref="Continuation{TResponse}.InvokeAsync"/>.
/// </summary>
/// <remarks>
/// The request travels as an object and the cast to its own type happens inside the level, where
/// that type is a constant.
/// </remarks>
/// <param name="request">The request being dispatched.</param>
/// <param name="services">The provider this walk resolves through.</param>
/// <param name="cancellationToken">The token this level and everything below it runs under.</param>
/// <typeparam name="TResponse">The response the chain produces.</typeparam>
internal delegate Task<TResponse> LevelEntry<TResponse>(
    object request, IServiceProvider services, CancellationToken cancellationToken);
