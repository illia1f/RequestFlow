// The methods run at freeze. The bodies they return are the dispatch path.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Builds one level of a stage chain: a stage, or the handler beneath the last one. A level is built
/// when the dispatch map freezes and closes over its position, reaching its stage or handler through the contract.
/// </summary>
/// <remarks>
/// Every level of every plan runs one shared body, so the call inside it sees every stage type in
/// the application and stays an interface dispatch. Compiling a body per level to make that call
/// monomorphic pays nothing: the cost of the shared body does not separate from zero.
/// </remarks>
internal static class LevelFactory
{
    public static LevelEntry<TResponse> Stage<TRequest, TResponse>(
        Type stageType, LevelEntry<TResponse> below)
        where TRequest : IRequest<TResponse>
        => (request, services, cancellationToken) =>
        {
            object stage = services.GetRequiredService(stageType);
            var next = new Continuation<TResponse>(below, request, services, cancellationToken);

            return NullTaskGuard.FromStage(
                ((IRequestStage<TRequest, TResponse>)stage).HandleAsync((TRequest)request, next, cancellationToken),
                stageType);
        };

    // The void shape's plain Task becomes a Task<NoResult>, so both shapes are entered as one type.
    public static LevelEntry<NoResult> VoidStage<TRequest>(Type stageType, LevelEntry<NoResult> below)
        where TRequest : IRequest<NoResult>
        => (request, services, cancellationToken) =>
        {
            object stage = services.GetRequiredService(stageType);
            var next = new Continuation(
                new Continuation<NoResult>(below, request, services, cancellationToken));

            return NullTaskGuard.FromStage(
                NoResultBridge.CompleteOrNull(
                    ((IRequestStage<TRequest>)stage).HandleAsync((TRequest)request, next, cancellationToken)),
                stageType);
        };

    // Each typeof stays inside the body, so the body captures nothing and the JIT folds it away.
    public static LevelEntry<TResponse> Handler<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
        => (request, services, cancellationToken) =>
        {
            var handler = (IRequestHandler<TRequest, TResponse>)
                services.GetRequiredService(typeof(IRequestHandler<TRequest, TResponse>));

            return NullTaskGuard.FromHandler(
                handler.HandleAsync((TRequest)request, cancellationToken), typeof(TRequest));
        };

    public static LevelEntry<NoResult> VoidHandler<TRequest>()
        where TRequest : IRequest<NoResult>
        => (request, services, cancellationToken) =>
        {
            var handler = (IRequestHandler<TRequest>)
                services.GetRequiredService(typeof(IRequestHandler<TRequest>));

            return NullTaskGuard.FromHandler(
                NoResultBridge.CompleteOrNull(handler.HandleAsync((TRequest)request, cancellationToken)),
                typeof(TRequest));
        };
}
