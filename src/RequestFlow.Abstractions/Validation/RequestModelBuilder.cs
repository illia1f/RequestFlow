using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Collects handlers and closed stages for one request type.
/// </summary>
/// <remarks>
/// Configure through <see cref="RequestFlowModelBuilder.AddRequest"/>.
/// </remarks>
public sealed class RequestModelBuilder
{
    private readonly List<HandlerModel> _handlers = [];
    private readonly List<ClosedStageModel> _stages = [];

    internal RequestModelBuilder()
    { }

    /// <summary>
    /// Adds a handler covering this request. A null response type records a void handler.
    /// </summary>
    /// <remarks>
    /// A null <paramref name="contractType"/> records <c>IRequestHandler&lt;TRequest&gt;</c> for void handlers
    /// and <c>IRequestHandler&lt;TRequest, TResponse&gt;</c> otherwise.
    /// For ValueTask, stream, or package-defined handlers, pass the open generic handler interface.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestModelBuilder AddHandler(
        Type handlerType, Type? responseType = null, Type? contractType = null)
        => AddHandler(handlerType, RequestFlowLifetime.Transient, responseType, contractType);

    /// <summary>
    /// Adds a handler registered with the named lifetime.
    /// </summary>
    /// <remarks>
    /// The overload without a lifetime uses <see cref="RequestFlowLifetime.Transient"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestModelBuilder AddHandler(
        Type handlerType, RequestFlowLifetime lifetime, Type? responseType = null, Type? contractType = null)
    {
        _handlers.Add(new HandlerModel(handlerType, responseType, contractType, lifetime));

        return this;
    }

    /// <summary>
    /// Adds one stage to this request's chain, in the order it wraps the handler.
    /// </summary>
    /// <remarks>
    /// A null <paramref name="contractType"/> records <c>IRequestStage&lt;TRequest, TResponse&gt;</c>.
    /// For other contracts, pass the open generic stage interface.
    /// Use the same <paramref name="declaredType"/> and contract in <see cref="RequestFlowModelBuilder.AddStageDeclaration(Type, Type)"/>
    /// so <see cref="StageDeclarationModel.ReachedRequests"/> includes this request.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestModelBuilder AddStage(Type declaredType, Type closedType, Type? contractType = null)
    {
        _stages.Add(new ClosedStageModel(declaredType, closedType, contractType));

        return this;
    }

    internal RequestModel Build(Type requestType)
        => new(requestType, [.. _handlers], [.. _stages]);
}
