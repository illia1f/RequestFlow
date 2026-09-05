using System;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

internal static class ValueLevelFactory
{
    public static ValueLevelEntry<TResponse> Stage<TRequest, TResponse>(
        Type stageType,
        ValueLevelEntry<TResponse> below)
        where TRequest : IValueRequest<TResponse>
        => (request, services, cancellationToken) =>
        {
            object stage = services.GetRequiredService(stageType);
            var next = new ValueContinuation<TResponse>(below, request, services, cancellationToken);

            return ((IValueRequestStage<TRequest, TResponse>)stage)
                .HandleAsync((TRequest)request, next, cancellationToken);
        };

    public static ValueVoidLevelEntry VoidStage<TRequest>(
        Type stageType,
        ValueVoidLevelEntry below)
        where TRequest : IValueRequest<NoResult>
        => (request, services, cancellationToken) =>
        {
            object stage = services.GetRequiredService(stageType);
            var next = new ValueContinuation(below, request, services, cancellationToken);

            return ((IValueRequestStage<TRequest>)stage)
                .HandleAsync((TRequest)request, next, cancellationToken);
        };

    public static ValueVoidLevelEntry TypedVoidStage<TRequest>(
        Type stageType,
        ValueVoidLevelEntry below)
        where TRequest : IValueRequest<NoResult>
    {
        ValueLevelEntry<NoResult> typedBelow =
            (request, services, cancellationToken) => ValueNoResultBridge.Complete(
                below(request, services, cancellationToken));

        return (request, services, cancellationToken) =>
        {
            object stage = services.GetRequiredService(stageType);
            var next = new ValueContinuation<NoResult>(
                typedBelow,
                request,
                services,
                cancellationToken);

            return ValueNoResultBridge.Discard(
                ((IValueRequestStage<TRequest, NoResult>)stage)
                    .HandleAsync((TRequest)request, next, cancellationToken));
        };
    }

    public static ValueLevelEntry<TResponse> Handler<TRequest, TResponse>()
        where TRequest : IValueRequest<TResponse>
        => (request, services, cancellationToken) =>
        {
            var handler = (IValueRequestHandler<TRequest, TResponse>)
                services.GetRequiredService(typeof(IValueRequestHandler<TRequest, TResponse>));

            return handler.HandleAsync((TRequest)request, cancellationToken);
        };

    public static ValueVoidLevelEntry VoidHandler<TRequest>()
        where TRequest : IValueRequest<NoResult>
        => (request, services, cancellationToken) =>
        {
            var handler = (IValueRequestHandler<TRequest>)
                services.GetRequiredService(typeof(IValueRequestHandler<TRequest>));

            return handler.HandleAsync((TRequest)request, cancellationToken);
        };
}
