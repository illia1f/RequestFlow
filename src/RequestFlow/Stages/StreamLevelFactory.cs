// The methods run at freeze. The bodies they return are the dispatch path.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Builds one level of a stream chain: a stage, or the handler beneath the last one. A level is
/// built when the dispatch map freezes and closes over its position, reaching its stage or handler
/// through the contract.
/// </summary>
internal static class StreamLevelFactory
{
    public static StreamLevelEntry<TItem> Stage<TRequest, TItem>(
        Type stageType, StreamLevelEntry<TItem> below)
        where TRequest : IStreamRequest<TItem>
        => (request, services, cancellationToken) =>
        {
            object stage = services.GetRequiredService(stageType);
            var next = new StreamContinuation<TItem>(below, request, services, cancellationToken);

            return NullStreamGuard.FromStage(
                ((IStreamRequestStage<TRequest, TItem>)stage).Handle((TRequest)request, next, cancellationToken),
                stageType);
        };

    // Each typeof stays inside the body, so the body captures nothing and the JIT folds it away.
    public static StreamLevelEntry<TItem> Handler<TRequest, TItem>()
        where TRequest : IStreamRequest<TItem>
        => (request, services, cancellationToken) =>
        {
            var handler = (IStreamRequestHandler<TRequest, TItem>)
                services.GetRequiredService(typeof(IStreamRequestHandler<TRequest, TItem>));

            return NullStreamGuard.FromHandler(
                handler.Handle((TRequest)request, cancellationToken), typeof(TRequest));
        };
}
