using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Closed plan for one void request wrapped in stages. The ordered stage types are fixed when
/// the dispatch map freezes; the instances resolve from the supplied provider on each call, so
/// DI lifetimes hold.
/// </summary>
internal sealed class StagedVoidRequestPlan<TRequest>(Type[] stageTypes) : RequestPlan<NoResult>
    where TRequest : IRequest<NoResult>
{
    /// <inheritdoc />
    public override Task<NoResult> ExecuteAsync(
        IRequest<NoResult> request, IServiceProvider services, CancellationToken cancellationToken)
    {
        var stages = new object[stageTypes.Length];
        for (int i = 0; i < stages.Length; i++)
            stages[i] = services.GetRequiredService(stageTypes[i]);

        var handler = services.GetRequiredService<IRequestHandler<TRequest>>();

        return new VoidStageExecutor<TRequest>(stages, handler, (TRequest)request, cancellationToken).RunAsync();
    }
}
