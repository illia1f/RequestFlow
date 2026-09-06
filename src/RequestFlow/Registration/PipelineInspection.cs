using System;
using System.Collections.Generic;

namespace RequestFlow;

internal static class PipelineInspection
{
    public static Dictionary<Type, RequestPipeline> Capture(
        RequestFlowModel model,
        IReadOnlyList<HandlerRegistration> handlers,
        IReadOnlyList<StageDeclaration> declarations,
        StageClosingCache closings)
    {
        Dictionary<Type, HandlerRegistration> registrations = [];
        foreach (var handler in handlers)
            registrations.Add(handler.RequestType, handler);

        Dictionary<Type, RequestPipeline> pipelines = [];
        foreach (var request in model.Requests)
        {
            if (!registrations.TryGetValue(request.RequestType, out HandlerRegistration? handler))
                continue;

            List<RequestPipelineStage> stages = [];
            List<ExcludedStageModel> excluded = [];
            foreach (var declaration in declarations)
            {
                StageClosingResult result = closings.GetResult(declaration, handler);
                if (result.ClosedType is { } closedType)
                {
                    stages.Add(new RequestPipelineStage(
                        declaration.StageType,
                        closedType,
                        ModelLifetime.Of(declaration.Lifetime),
                        declaration.HandlerFilter));
                }
                else
                {
                    StageExclusionReason reason = result.ExclusionReason!.Value;
                    excluded.Add(new ExcludedStageModel(
                        declaration.StageType,
                        declaration.HandlerFilter,
                        reason));
                }
            }

            pipelines.Add(request.RequestType, new RequestPipeline(
                request.RequestType,
                FamilyOf(handler),
                handler.Contract,
                request.Handlers[0],
                [.. stages],
                [.. excluded]));
        }

        return pipelines;
    }

    private static RequestPipelineFamily FamilyOf(HandlerRegistration handler)
    {
        if (handler.ContractDefinition == typeof(IStreamRequestHandler<,>))
            return RequestPipelineFamily.Stream;
        if (handler.ContractDefinition == typeof(IValueRequestHandler<,>)
            || handler.ContractDefinition == typeof(IValueRequestHandler<>))
            return RequestPipelineFamily.ValueTask;

        return RequestPipelineFamily.Task;
    }
}
