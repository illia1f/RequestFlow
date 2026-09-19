using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal sealed class ValueRequestDispatcher(DispatchMap map, IServiceProvider services)
    : IValueRequestDispatcher
{
    private readonly DispatchMap _map = map ?? throw new ArgumentNullException(nameof(map));
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <inheritdoc />
    public ValueTask<TResponse> SendAsync<TResponse>(
        IValueRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(request);

        Type requestType = request.GetType();

        if (!_map.TryGetPlanFor(requestType, out RequestPlanBase? plan))
            return ThrowHelper.HandlerNotFound<ValueTask<TResponse>>(requestType);

        if (plan is not ValueRequestPlan<TResponse> typedPlan)
            return ThrowHelper.ResponseTypeMismatch<ValueTask<TResponse>>(
                requestType,
                expected: plan.ResponseType,
                actual: typeof(TResponse));

        return typedPlan.ExecuteAsync(request, _services, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SendAsync(
        IValueRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(request);

        Type requestType = request.GetType();

        if (!_map.TryGetPlanFor(requestType, out RequestPlanBase? plan))
            return ThrowHelper.HandlerNotFound<ValueTask>(requestType);

        if (plan is ValueVoidRequestPlan voidPlan)
            return voidPlan.ExecuteVoidAsync(request, _services, cancellationToken);

        if (plan is ValueRequestPlan<NoResult> typedPlan)
            return ValueNoResultBridge.Discard(
                typedPlan.ExecuteAsync(request, _services, cancellationToken));

        return ThrowHelper.ResponseTypeMismatch<ValueTask>(
            requestType,
            expected: plan.ResponseType,
            actual: typeof(NoResult));
    }
}
