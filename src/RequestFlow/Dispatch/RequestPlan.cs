using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Closed plan for one request/response pair. Resolves the handler from the supplied
/// provider on each call, so scoped and transient handlers work even though the plan
/// itself lives for the process lifetime.
/// </summary>
internal sealed class RequestPlan<TRequest, TResponse> : RequestPlan<TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public override Task<TResponse> ExecuteAsync(
        IRequest<TResponse> request, IServiceProvider services, CancellationToken cancellationToken)
    {
        var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        return handler.HandleAsync((TRequest)request, cancellationToken);
    }
}
