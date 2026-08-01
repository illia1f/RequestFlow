using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Stage chain that terminates at <see cref="IRequestHandler{TRequest, TResponse}"/>.
/// </summary>
internal sealed class TypedStageExecutor<TRequest, TResponse>(
    IRequestStage<TRequest, TResponse>[] stages,
    IRequestHandler<TRequest, TResponse> handler,
    TRequest request,
    CancellationToken cancellationToken)
    : StageExecutor<TRequest, TResponse>(stages.Length, request, cancellationToken)
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    protected override Task<TResponse> InvokeStageAsync(
        int index, TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken)
        => stages[index].HandleAsync(request, next, cancellationToken);

    /// <inheritdoc />
    protected override Task<TResponse> InvokeHandlerAsync(TRequest request, CancellationToken cancellationToken)
        => handler.HandleAsync(request, cancellationToken);

    /// <inheritdoc />
    protected override Type StageTypeAt(int index) => stages[index].GetType();
}
