using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Closed plan for one void request wrapped in stages. The ordered stage types and the
/// contract shape each one runs under are fixed when the dispatch map freezes; each stage
/// instance and the handler resolve from the supplied provider when their level first runs, so
/// DI lifetimes hold and a level the chain never reaches is never built.
/// </summary>
internal sealed class StagedVoidRequestPlan<TRequest>(Type[] stageTypes, bool[] typedShapes) : RequestPlan<NoResult>
    where TRequest : IRequest<NoResult>
{
    /// <inheritdoc />
    public override Task<NoResult> ExecuteAsync(
        IRequest<NoResult> request, IServiceProvider services, CancellationToken cancellationToken)
        => new VoidStageExecutor<TRequest>(
            stageTypes, typedShapes, services, (TRequest)request, cancellationToken).RunAsync();
}
