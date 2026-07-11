using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Closed plan for one void request. Resolves the standalone handler from the supplied
/// provider on each call and completes with <see cref="NoResult"/>.
/// </summary>
internal sealed class VoidRequestPlan<TRequest> : RequestPlan<NoResult>
    where TRequest : IRequest<NoResult>
{
    /// <inheritdoc />
    public override Task<NoResult> ExecuteAsync(
        IRequest<NoResult> request, IServiceProvider services, CancellationToken cancellationToken)
    {
        var handler = services.GetRequiredService<IRequestHandler<TRequest>>();
        Task task = handler.HandleAsync((TRequest)request, cancellationToken);
        return task.Status == TaskStatus.RanToCompletion ? NoResult.Task : AwaitAsync(task);
    }

    private static async Task<NoResult> AwaitAsync(Task task)
    {
        await task.ConfigureAwait(false);
        return NoResult.Value;
    }
}
