namespace Orders.Modules.Audit;

/// <summary>
/// Provides service registration and endpoint mapping for the Audit module.
/// </summary>
public static class AuditModule
{
    /// <summary>
    /// Registers the Audit module's services and handlers.
    /// </summary>
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        services.AddSingleton<OrderMetrics>();
        services.AddSingleton<OrderAuditTrail>();
        services.AddRequestFlow(options =>
            options.RegisterHandlersFromCallingAssembly());

        return services;
    }

    /// <summary>
    /// Maps the Audit module's HTTP endpoints.
    /// </summary>
    public static void MapAuditModule(this IEndpointRouteBuilder routes)
        => routes.MapGetOrderActivity();
}
