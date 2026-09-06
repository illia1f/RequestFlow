using Orders.Modules.Audit;
using Orders.Modules.Orders;
using RequestFlow;

namespace Orders.Api;

internal static class PipelineInspectionExample
{
    public static void Print(IServiceProvider services)
    {
        Console.WriteLine("RequestFlow pipeline inspection");
        Console.WriteLine("Declared registration; DI replacements and factories can supply different instances.");

        Print(services.InspectRequestFlow<CreateOrderCommand>());
        Print(services.InspectRequestFlow<GetOrderQuery>());
        Print(services.InspectRequestFlow<GetOrderActivityQuery>());
    }

    private static void Print(RequestPipeline pipeline)
    {
        Console.WriteLine();
        Console.WriteLine($"Request: {TypeName(pipeline.RequestType)}");
        Console.WriteLine($"Family: {pipeline.Family}");
        Console.WriteLine($"Response: {(pipeline.DeclaredHandler.ResponseType is { } response ? TypeName(response) : "(none)")}");
        Console.WriteLine($"Declared handler: {TypeName(pipeline.DeclaredHandler.HandlerType)} ({pipeline.DeclaredHandler.Lifetime})");
        Console.WriteLine($"Handler service: {TypeName(pipeline.HandlerServiceType)}");
        Console.WriteLine("Stages, outermost first:");

        if (pipeline.Stages.Count == 0)
            Console.WriteLine("  (none)");

        for (int i = 0; i < pipeline.Stages.Count; i++)
        {
            RequestPipelineStage stage = pipeline.Stages[i];
            Console.WriteLine($"  {i + 1}. {TypeName(stage.ClosedType)} ({stage.DeclaredLifetime})");
        }

        Console.WriteLine("Excluded stages:");
        if (pipeline.ExcludedStages.Count == 0)
            Console.WriteLine("  (none)");

        foreach (ExcludedStageModel stage in pipeline.ExcludedStages)
            Console.WriteLine($"  {TypeName(stage.DeclaredType)}: {stage.ReasonCode}");
    }

    private static string TypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        string name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
    }
}
