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
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        Type requestType = request.GetType();

        if (!_map.TryGetPlanFor(requestType, out RequestPlanBase? plan))
            throw new HandlerNotFoundException(requestType);

        if (plan is not ValueRequestPlan<TResponse> typedPlan)
            throw new ResponseTypeMismatchException(
                requestType,
                expected: plan!.ResponseType,
                actual: typeof(TResponse));

        return typedPlan.ExecuteAsync(request, _services, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SendAsync(
        IValueRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        Type requestType = request.GetType();

        if (!_map.TryGetPlanFor(requestType, out RequestPlanBase? plan))
            throw new HandlerNotFoundException(requestType);

        if (plan is ValueVoidRequestPlan voidPlan)
            return voidPlan.ExecuteVoidAsync(request, _services, cancellationToken);

        if (plan is ValueRequestPlan<NoResult> typedPlan)
            return ValueNoResultBridge.Discard(
                typedPlan.ExecuteAsync(request, _services, cancellationToken));

        throw new ResponseTypeMismatchException(
            requestType,
            expected: plan!.ResponseType,
            actual: typeof(NoResult));
    }
}
