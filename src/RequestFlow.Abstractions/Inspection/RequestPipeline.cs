using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RequestFlow;

/// <summary>
/// A validated request's declared handler, ordered stages, and stage exclusions.
/// </summary>
/// <remarks>
/// Describes registration, not an execution trace. Stages can short-circuit or repeat their continuations.
/// Application DI replacements and factories can change the resolved instances without changing this description.
/// </remarks>
public sealed class RequestPipeline
{
    internal RequestPipeline(
        Type requestType,
        RequestPipelineFamily family,
        Type handlerServiceType,
        HandlerModel declaredHandler,
        RequestPipelineStage[] stages,
        ExcludedStageModel[] excludedStages)
    {
        RequestType = requestType;
        Family = family;
        HandlerServiceType = handlerServiceType;
        DeclaredHandler = declaredHandler;
        Stages = new ReadOnlyCollection<RequestPipelineStage>(stages);
        ExcludedStages = new ReadOnlyCollection<ExcludedStageModel>(excludedStages);
    }

    /// <summary>
    /// The exact registered request type.
    /// </summary>
    public Type RequestType { get; }

    /// <summary>
    /// The Task, ValueTask, or stream family of this pipeline.
    /// </summary>
    public RequestPipelineFamily Family { get; }

    /// <summary>
    /// The closed core handler contract dispatch resolves from DI.
    /// </summary>
    public Type HandlerServiceType { get; }

    /// <summary>
    /// The handler and lifetime declared through RequestFlow registration.
    /// </summary>
    /// <remarks>
    /// Handler filters use this implementation type. Application DI replacements do not change it.
    /// </remarks>
    public HandlerModel DeclaredHandler { get; }

    /// <summary>
    /// The selected stages in registration order, outermost first.
    /// </summary>
    public IReadOnlyList<RequestPipelineStage> Stages { get; }

    /// <summary>
    /// Registered stages that did not match this request, in declaration order.
    /// </summary>
    public IReadOnlyList<ExcludedStageModel> ExcludedStages { get; }
}
