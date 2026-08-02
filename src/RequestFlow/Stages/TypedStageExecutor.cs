using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Stage chain that terminates at <see cref="IRequestHandler{TRequest, TResponse}"/>.
/// </summary>
internal sealed class TypedStageExecutor<TRequest, TResponse>(
    Type[] stageTypes,
    IServiceProvider services,
    TRequest request,
    CancellationToken cancellationToken)
    : StageExecutor<TRequest, TResponse>(stageTypes.Length, request, cancellationToken)
    where TRequest : IRequest<TResponse>
{
    private IRequestHandler<TRequest, TResponse>? _handler;

    /// <inheritdoc />
    protected override object ResolveStage(int index) => services.GetRequiredService(stageTypes[index]);

    /// <inheritdoc />
    protected override Task<TResponse> InvokeStageAsync(
        int index, object stage, IContinuation<TResponse> next, TRequest request, CancellationToken cancellationToken)
        => ((IRequestStage<TRequest, TResponse>)stage).HandleAsync(request, next, cancellationToken);

    /// <inheritdoc />
    protected override Task<TResponse> InvokeHandlerAsync(TRequest request, CancellationToken cancellationToken)
        => (_handler ??= services.GetRequiredService<IRequestHandler<TRequest, TResponse>>())
            .HandleAsync(request, cancellationToken);

    /// <inheritdoc />
    protected override Type StageTypeAt(int index) => stageTypes[index];
}
