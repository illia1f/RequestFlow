using System;

namespace RequestFlow;

/// <summary>
/// One selected stage in a validated request pipeline.
/// </summary>
public sealed class RequestPipelineStage
{
    internal RequestPipelineStage(
        Type declaredType, Type closedType, RequestFlowLifetime declaredLifetime, Type? handlerFilter)
    {
        DeclaredType = declaredType;
        ClosedType = closedType;
        DeclaredLifetime = declaredLifetime;
        HandlerFilter = handlerFilter;
    }

    /// <summary>
    /// The open or closed stage type supplied at registration.
    /// </summary>
    public Type DeclaredType { get; }

    /// <summary>
    /// The closed stage service type selected for this request.
    /// </summary>
    public Type ClosedType { get; }

    /// <summary>
    /// The lifetime declared through RequestFlow, before application DI replacements.
    /// </summary>
    public RequestFlowLifetime DeclaredLifetime { get; }

    /// <summary>
    /// The handler filter supplied at registration, or null when no filter was specified.
    /// </summary>
    public Type? HandlerFilter { get; }
}
