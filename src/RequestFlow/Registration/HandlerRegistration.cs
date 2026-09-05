using System;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Container-neutral description of one discovered handler and the lifetime it registers under.
/// </summary>
internal sealed class HandlerRegistration(HandlerDiscovery discovery, ServiceLifetime lifetime)
{
    /// <summary>
    /// The concrete handler class discovered by the scan.
    /// </summary>
    public Type ImplementationType { get; } = discovery.ImplementationType;

    /// <summary>
    /// The closed request type the handler handles.
    /// </summary>
    public Type RequestType { get; } = discovery.RequestType;

    /// <summary>
    /// The response type; <see cref="NoResult"/> for void handlers.
    /// </summary>
    public Type ResponseType { get; } = discovery.ResponseType;

    /// <summary>
    /// True when the handler implements the plain <see cref="IRequestHandler{TRequest}"/>
    /// or <see cref="IValueRequestHandler{TRequest}"/> contract.
    /// </summary>
    public bool IsVoid { get; } = discovery.IsVoid;

    /// <summary>
    /// The closed core contract the scan matched this handler through.
    /// </summary>
    public Type Contract { get; } = discovery.Contract;

    /// <summary>
    /// The open definition of <see cref="Contract"/>, which is what tells one handler family from another.
    /// </summary>
    public Type ContractDefinition { get; } = discovery.ContractDefinition;

    /// <summary>
    /// The lifetime the <c>AddRequestFlow</c> call that found this handler registers it with.
    /// </summary>
    public ServiceLifetime Lifetime { get; } = lifetime;
}
