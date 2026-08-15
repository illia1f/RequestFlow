using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Collects the handlers and stage closings for one request type while a model is being built.
/// </summary>
/// <remarks>
/// Reached through <see cref="RequestFlowModelBuilder.AddRequest"/> and never constructed directly.
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
    /// Leaving <paramref name="contractType"/> null records the core handler contract:
    /// <c>IRequestHandler&lt;TRequest&gt;</c> for a void handler and
    /// <c>IRequestHandler&lt;TRequest, TResponse&gt;</c> otherwise. Naming one takes an open
    /// generic interface, since that is what a rule compares against.
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
    /// The overload without a lifetime records <see cref="RequestFlowLifetime.Transient"/>, which is
    /// what an <c>AddRequestFlow</c> call uses unless it calls <c>WithScopedHandlers</c>.
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
    /// Leaving <paramref name="contractType"/> null records
    /// <c>IRequestStage&lt;TRequest, TResponse&gt;</c>. A void stage names
    /// <c>IRequestStage&lt;TRequest&gt;</c> here and at the matching
    /// <see cref="RequestFlowModelBuilder.AddStageDeclaration(Type, Type)"/> call, or at neither.
    /// Naming one takes an open generic interface, since that is what a rule compares against.
    /// <para>
    /// A two-parameter stage over a void request keeps the typed contract, and its
    /// <paramref name="closedType"/> closes over <see cref="NoResult"/>, as
    /// <c>LoggingStage&lt;Purge, NoResult&gt;</c>. That is the shape the freeze produces, so a
    /// test reproducing it passes the same type.
    /// </para>
    /// <para>
    /// <paramref name="declaredType"/> is the type its
    /// <see cref="RequestFlowModelBuilder.AddStageDeclaration(Type, Type)"/> call names, which is
    /// what ties the declaration to this request in
    /// <see cref="StageDeclarationModel.ReachedRequests"/>. Naming the closed type instead leaves that list empty.
    /// </para>
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
