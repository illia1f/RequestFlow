using Orders.Api;
using Orders.Api.Orders;
using Orders.Api.Rules;
using Orders.Api.Stages;
using Orders.Api.Validation;
using Orders.Api.Violations;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<OrderStore>();
builder.Services.AddOpenApi();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

builder.Services
    .AddRequestFlow(options =>
    {
        options.RegisterHandlersFromAssemblyContaining<Program>();
        options.AddStage(typeof(LoggingStage<,>));
        options.AddStage(
            typeof(ValidationStage<,>), stage => stage.WhereHandlerImplements<IOrdersCommandHandler>());

        // The scan finds request types as well as handlers, so the types that break conventions
        // only stay out of a normal start by sitting in an assembly of their own.
        if (args.Contains("--break-rules"))
            options.RegisterHandlersFromAssembly(typeof(RefundOrderCommand).Assembly);
    })
    .AddCqrs()
    .AddValidationRule<CommandNamingRule>()
    .AddValidationRule<CommandValidatedRule>();

WebApplication app = builder.Build();

// Every problem lands here, at startup, instead of on the first request.
app.Services.ValidateRequestFlow();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Orders.Api"));
}

app.MapOrders();

app.Run();
