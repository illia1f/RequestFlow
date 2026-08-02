using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Stage chain that terminates at the standalone <see cref="IRequestHandler{TRequest}"/>.
/// Its stages come in both contract shapes, so which shape each level runs is settled when the
/// dispatch map freezes and read from <paramref name="typedShapes"/> here.
/// </summary>
internal sealed class VoidStageExecutor<TRequest>(
    Type[] stageTypes,
    bool[] typedShapes,
    IServiceProvider services,
    TRequest request,
    CancellationToken cancellationToken)
    : StageExecutor<TRequest, NoResult>(stageTypes.Length, request, cancellationToken)
    where TRequest : IRequest<NoResult>
{
    private IRequestHandler<TRequest>? _handler;

    /// <inheritdoc />
    protected override object ResolveStage(int index) => services.GetRequiredService(stageTypes[index]);

    /// <inheritdoc />
    protected override Task<NoResult> InvokeStageAsync(
        int index, object stage, IContinuation<NoResult> next, TRequest request, CancellationToken cancellationToken)
    {
        if (typedShapes[index])
            return ((IRequestStage<TRequest, NoResult>)stage).HandleAsync(request, next, cancellationToken);

        // The void shape's Task converts to Task<NoResult>, so both forms reach the same level object and share its guard state.
        return NoResultBridge.CompleteOrNull(
            ((IRequestStage<TRequest>)stage).HandleAsync(request, (IContinuation)next, cancellationToken));
    }

    /// <inheritdoc />
    protected override Task<NoResult> InvokeHandlerAsync(TRequest request, CancellationToken cancellationToken)
        => NoResultBridge.CompleteOrNull(
            (_handler ??= services.GetRequiredService<IRequestHandler<TRequest>>())
                .HandleAsync(request, cancellationToken));

    /// <inheritdoc />
    protected override Type StageTypeAt(int index) => stageTypes[index];
}
