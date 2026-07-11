using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Default <see cref="IRequestDispatcher"/> over the frozen dispatch map.
/// </summary>
internal sealed class RequestDispatcher(DispatchMap map, IServiceProvider services) : IRequestDispatcher
{
    private readonly DispatchMap _map = map ?? throw new ArgumentNullException(nameof(map));
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <inheritdoc />
    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        Type requestType = request.GetType();
        if (!_map.TryGet(requestType, out RequestPlanBase? plan) || plan is null)
            throw new HandlerNotFoundException(requestType);

        if (plan is not RequestPlan<TResponse> typedPlan)
            throw new ResponseTypeMismatchException(requestType, expected: plan.ResponseType, actual: typeof(TResponse));

        return typedPlan.ExecuteAsync(request, _services, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendAsync(IRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return SendAsync<NoResult>(request, cancellationToken);
    }
}
