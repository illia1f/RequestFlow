using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Closed plan for one request/response pair wrapped in stages. The ordered stage types are
/// fixed when the dispatch map freezes; the instances resolve from the supplied provider on
/// each call, so DI lifetimes hold.
/// </summary>
internal sealed class StagedRequestPlan<TRequest, TResponse>(Type[] stageTypes) : RequestPlan<TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public override Task<TResponse> ExecuteAsync(
        IRequest<TResponse> request, IServiceProvider services, CancellationToken cancellationToken)
    {
        var stages = new IRequestStage<TRequest, TResponse>[stageTypes.Length];
        for (int i = 0; i < stages.Length; i++)
            stages[i] = (IRequestStage<TRequest, TResponse>)services.GetRequiredService(stageTypes[i]);

        var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

        return new TypedStageExecutor<TRequest, TResponse>(
            stages, handler, (TRequest)request, cancellationToken).RunAsync();
    }
}
