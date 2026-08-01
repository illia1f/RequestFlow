using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Stage chain that terminates at the standalone <see cref="IRequestHandler{TRequest}"/>.
/// Its stages come in both contract shapes, so the array is untyped and each level picks.
/// </summary>
internal sealed class VoidStageExecutor<TRequest>(
    object[] stages,
    IRequestHandler<TRequest> handler,
    TRequest request,
    CancellationToken cancellationToken)
    : StageExecutor<TRequest, NoResult>(stages.Length, request, cancellationToken)
    where TRequest : IRequest<NoResult>
{
    /// <inheritdoc />
    protected override Task<NoResult> InvokeStageAsync(
        int index, TRequest request, StageDelegate<NoResult> next, CancellationToken cancellationToken)
    {
        object stage = stages[index];
        if (stage is IRequestStage<TRequest, NoResult> typed)
            return typed.HandleAsync(request, next, cancellationToken);

        // The void shape wraps the same continuation, so both forms share its guard state.
        return NoResultBridge.CompleteOrNull(
            ((IRequestStage<TRequest>)stage).HandleAsync(request, new StageDelegate(next.Invoke), cancellationToken));
    }

    /// <inheritdoc />
    protected override Task<NoResult> InvokeHandlerAsync(TRequest request, CancellationToken cancellationToken)
        => NoResultBridge.CompleteOrNull(handler.HandleAsync(request, cancellationToken));

    /// <inheritdoc />
    protected override Type StageTypeAt(int index) => stages[index].GetType();
}
