using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Closed plan for one request/response pair wrapped in stages. The ordered stage types are
/// fixed when the dispatch map freezes; each stage instance and the handler resolve from the
/// supplied provider when their level first runs, so DI lifetimes hold and a level the chain
/// never reaches is never built.
/// </summary>
internal sealed class StagedRequestPlan<TRequest, TResponse>(Type[] stageTypes) : RequestPlan<TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public override Task<TResponse> ExecuteAsync(
        IRequest<TResponse> request, IServiceProvider services, CancellationToken cancellationToken)
        => new TypedStageExecutor<TRequest, TResponse>(
            stageTypes, services, (TRequest)request, cancellationToken).RunAsync();
}
