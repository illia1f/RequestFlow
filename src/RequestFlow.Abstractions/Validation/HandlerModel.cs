using System;

namespace RequestFlow;

/// <summary>
/// One handler found for a request.
/// </summary>
public sealed class HandlerModel
{
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    internal HandlerModel(
        Type handlerType,
        Type? responseType = null,
        Type? contractType = null,
        RequestFlowLifetime lifetime = RequestFlowLifetime.Transient)
    {
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        ResponseType = responseType;
        Lifetime = lifetime;
        ContractType = responseType is null
            ? OpenContract.OrDefault(contractType, typeof(IRequestHandler<>), typeof(IRequestHandler<,>))
            : OpenContract.OrDefault(contractType, typeof(IRequestHandler<,>), typeof(IRequestHandler<>));
    }

    /// <summary>
    /// The class implementing the handler, not the interface it implements.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// What the handler returns, or null when it handles a void request.
    /// </summary>
    public Type? ResponseType { get; }

    /// <summary>
    /// True when the handler covers a void request.
    /// </summary>
    public bool IsVoid => ResponseType is null;

    /// <summary>
    /// The lifetime this handler is registered with.
    /// </summary>
    /// <remarks>
    /// Each <c>AddRequestFlow</c> call decides for the handlers it found, so two handlers in one
    /// registration can differ. A handler the application registers by hand afterwards wins at
    /// resolution without changing this.
    /// </remarks>
    public RequestFlowLifetime Lifetime { get; }

    /// <summary>
    /// The open generic handler contract this handler implements.
    /// </summary>
    /// <remarks>
    /// A <see cref="Type"/> rather than an enum, so a package the core knows nothing about can
    /// record its own contract here and its own rule can match on it. It has to be
    /// <c>IRequestHandler</c> or an interface built on one.
    /// </remarks>
    public Type ContractType { get; }
}
