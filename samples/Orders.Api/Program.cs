using Orders.Api;
using Orders.Api.Violations;
using Orders.Modules.Audit;
using Orders.Modules.Orders;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

builder.Services.AddOrdersModule().AddCqrs();
builder.Services.AddAuditModule();

if (args.Contains("--break-rules"))
    builder.Services.AddRequestFlow(options =>
        options.RegisterHandlersFromAssembly(typeof(RefundOrderCommand).Assembly));

WebApplication app = builder.Build();

app.Services.ValidateRequestFlow();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Orders.Api"));
}

app.MapOrders();
app.MapAuditModule();

app.Run();
