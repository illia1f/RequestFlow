using Orders.Modules.Orders.Rules;
using Orders.Modules.Orders.Stages;
using Orders.Modules.Orders.Validation;
using RequestFlow;

namespace Orders.Modules.Orders;

/// <summary>
/// Provides service registration for the Orders module.
/// </summary>
public static class OrdersModule
{
    /// <summary>
    /// Registers the Orders module's services, handlers, stages, event policies, and validation rules.
    /// </summary>
    public static RequestFlowBuilder AddOrdersModule(this IServiceCollection services)
    {
        services.AddSingleton<OrderStore>();

        return services
            .AddRequestFlow(options =>
            {
                options.RegisterHandlersFromCallingAssembly();
                options.AddStage(typeof(LoggingStage<,>));
                options.AddStage(
                    typeof(ValidationStage<,>), stage => stage.WhereHandlerImplements<IOrdersCommandHandler>());
                options.PublishEventsInParallel<OrderPlaced>();
                options.PublishEventsFailFast<OrderCancelled>();
            })
            .AddValidationRule<CommandNamingRule>()
            .AddValidationRule<CommandValidationStageRule>();
    }
}
