using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal sealed class RequestDispatcher(DispatchMap map, IServiceProvider services) : IRequestDispatcher
{
    private readonly DispatchMap _map = map ?? throw new ArgumentNullException(nameof(map));
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <inheritdoc />
    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(request);

        Type requestType = request.GetType();

        if (!_map.TryGetPlanFor(requestType, out RequestPlanBase? plan))
            return ThrowHelper.HandlerNotFound<Task<TResponse>>(requestType);

        if (plan is not RequestPlan<TResponse> typedPlan)
            return ThrowHelper.ResponseTypeMismatch<Task<TResponse>>(
                requestType, expected: plan.ResponseType, actual: typeof(TResponse));

        return typedPlan.ExecuteAsync(request, _services, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendAsync(IRequest request, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(request);

        return SendAsync<NoResult>(request, cancellationToken);
    }
}
