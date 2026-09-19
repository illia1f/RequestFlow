using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Default <see cref="IStreamDispatcher"/> over the frozen dispatch map.
/// </summary>
internal sealed class StreamDispatcher(DispatchMap map, IServiceProvider services) : IStreamDispatcher
{
    private readonly DispatchMap _map = map ?? throw new ArgumentNullException(nameof(map));
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <inheritdoc />
    // Not an iterator, so a lookup failure throws from this call rather than at first enumeration.
    public IAsyncEnumerable<TItem> Stream<TItem>(
        IStreamRequest<TItem> request, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(request);

        Type requestType = request.GetType();

        if (!_map.TryGetPlanFor(requestType, out RequestPlanBase? plan))
            return ThrowHelper.HandlerNotFound<IAsyncEnumerable<TItem>>(requestType);

        if (plan is not StreamPlan<TItem> streamPlan)
            return ThrowHelper.ResponseTypeMismatch<IAsyncEnumerable<TItem>>(
                requestType, expected: plan.ResponseType, actual: typeof(TItem));

        return cancellationToken.CanBeCanceled
            ? Walk(streamPlan, request, _services, cancellationToken)
            : WalkWithoutDispatchToken(streamPlan, request, _services);
    }

    private static async IAsyncEnumerable<TItem> WalkWithoutDispatchToken<TItem>(
        StreamPlan<TItem> plan,
        object request,
        IServiceProvider services,
        [EnumeratorCancellation] CancellationToken iterationToken = default)
    {
        await foreach (TItem item in plan.Execute(request, services, iterationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    // The dispatch token arrives as a parameter and the iteration token through the attribute, so
    // the two are joined when the walk starts rather than when the enumerator is handed out.
    private static async IAsyncEnumerable<TItem> Walk<TItem>(
        StreamPlan<TItem> plan,
        object request,
        IServiceProvider services,
        CancellationToken cancellationToken,
        [EnumeratorCancellation] CancellationToken iterationToken = default)
    {
        using CancellationTokenSource? linked =
            TokenLink.Combine(cancellationToken, iterationToken, out CancellationToken token);

        // The joined token travels only as the Handle argument. 
        // WithCancellation here would tie it back onto the returned sequence and override a token a stage substituted for the levels below it.
        await foreach (TItem item in plan.Execute(request, services, token).ConfigureAwait(false))
        {
            yield return item;
        }
    }
}
