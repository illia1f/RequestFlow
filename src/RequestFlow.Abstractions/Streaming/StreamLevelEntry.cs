using System;
using System.Collections.Generic;
using System.Threading;

namespace RequestFlow;

/// <summary>
/// Enters one level of a frozen stream chain: a stage, or the handler beneath the last one.
/// A <see cref="StreamContinuation{TItem}"/> holds one of these and calls
/// it again on every <see cref="StreamContinuation{TItem}.Invoke"/>.
/// </summary>
/// <remarks>
/// The request travels as an object and the cast to its own type happens inside the level, where
/// that type is a constant.
/// </remarks>
/// <param name="request">The request being dispatched.</param>
/// <param name="services">The provider this walk resolves through.</param>
/// <param name="cancellationToken">The token this level and everything below it runs under.</param>
/// <typeparam name="TItem">The type each element of the sequence has.</typeparam>
internal delegate IAsyncEnumerable<TItem> StreamLevelEntry<TItem>(
    object request, IServiceProvider services, CancellationToken cancellationToken);
