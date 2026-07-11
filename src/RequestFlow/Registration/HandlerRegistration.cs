using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Container-neutral description of one discovered handler.
/// </summary>
internal sealed class HandlerRegistration(Type implementationType, Type requestType, Type responseType, bool isVoid)
{
    /// <summary>
    /// The concrete handler class discovered by the scan.
    /// </summary>
    public Type ImplementationType { get; } = implementationType;

    /// <summary>
    /// The closed request type the handler handles.
    /// </summary>
    public Type RequestType { get; } = requestType;

    /// <summary>
    /// The response type; <see cref="NoResult"/> for void handlers.
    /// </summary>
    public Type ResponseType { get; } = responseType;

    /// <summary>
    /// True when the handler implements <see cref="IRequestHandler{TRequest}"/>.
    /// </summary>
    public bool IsVoid { get; } = isVoid;
}

/// <summary>
/// Handlers and request types discovered by one scan pass.
/// </summary>
internal sealed class ScanResult(IReadOnlyList<HandlerRegistration> handlers, IReadOnlyList<Type> requestTypes)
{
    public IReadOnlyList<HandlerRegistration> Handlers { get; } = handlers;

    /// <summary>
    /// Every discovered request type, handled or not; validation reports the difference.
    /// </summary>
    public IReadOnlyList<Type> RequestTypes { get; } = requestTypes;
}
