using System;

namespace RequestFlow;

/// <summary>
/// One handler found for a request.
/// </summary>
public sealed class HandlerModel
{
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
    /// The response the handler produces, the item type for a stream handler, or null when it handles a void request.
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
    /// Each <c>AddRequestFlow</c> call sets the lifetime of the handlers it discovers.
    /// Application overrides affect resolution without changing this value.
    /// </remarks>
    public RequestFlowLifetime Lifetime { get; }

    /// <summary>
    /// The open generic handler contract this handler implements.
    /// </summary>
    /// <remarks>
    /// Accepts any open generic interface, including ValueTask, stream, and package-defined contracts.
    /// </remarks>
    public Type ContractType { get; }
}
