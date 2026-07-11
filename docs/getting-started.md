# Getting started

From an empty project to the first dispatched request.

## Install

The packages are not on NuGet yet. Once they are, install the runtime package:

```
dotnet add package RequestFlow
```

It pulls in `RequestFlow.Abstractions`, the contracts package with no dependencies of its own. Projects that only define requests and handlers, such as a domain layer, can reference `RequestFlow.Abstractions` alone.

All types live in the `RequestFlow` namespace, so one `using RequestFlow;` covers requests, handlers, and the dispatcher. The registration extensions live in `Microsoft.Extensions.DependencyInjection`, which a typical `Program.cs` already imports.

## Define a request and its handler

A request declares what it returns through `IRequest<TResponse>`. A handler pairs with it through `IRequestHandler<TRequest, TResponse>`:

```csharp
using RequestFlow;

public sealed record OrderId(Guid Value);

public sealed record CreateOrder(string CustomerId) : IRequest<OrderId>;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrder, OrderId>
{
    public Task<OrderId> HandleAsync(CreateOrder request, CancellationToken cancellationToken)
        => Task.FromResult(new OrderId(Guid.NewGuid()));
}
```

Each request type has exactly one handler. Startup validation enforces this: a request with no handler, or with two, fails before the first dispatch (see [exceptions.md](exceptions.md)).

A request that returns nothing implements `IRequest` instead, and its handler returns plain `Task`:

```csharp
public sealed record ClearCache : IRequest;

public sealed class ClearCacheHandler : IRequestHandler<ClearCache>
{
    public Task HandleAsync(ClearCache request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

## Register

`AddRequestFlow` scans the assemblies you point it at and registers every handler it finds:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<CreateOrderHandler>());
```

That one call registers the handlers, the `IRequestDispatcher`, and the validation that runs when the first dispatcher is resolved. To surface registration problems at startup instead, call `ValidateRequestFlow` once after building the provider:

```csharp
var app = builder.Build();
app.Services.ValidateRequestFlow();
```

[registration.md](registration.md) covers the remaining options: generic handlers, opting out of the missing-handler check, and lifetimes.

## Send a request

Inject `IRequestDispatcher` and call `SendAsync`. In ASP.NET Core it is available anywhere the container reaches:

```csharp
app.MapPost("/orders", async (CreateOrder request, IRequestDispatcher dispatcher, CancellationToken ct) =>
{
    OrderId id = await dispatcher.SendAsync(request, ct);
    return Results.Ok(id);
});
```

Void requests dispatch the same way and return `Task`:

```csharp
await dispatcher.SendAsync(new ClearCache(), ct);
```

Outside a web host, the same flow works with a plain `ServiceCollection`:

```csharp
var services = new ServiceCollection();
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<CreateOrderHandler>());

IServiceProvider provider = services.BuildServiceProvider().ValidateRequestFlow();

using IServiceScope scope = provider.CreateScope();
var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

OrderId id = await dispatcher.SendAsync(new CreateOrder("c42"));
```

The scope matters: the dispatcher is registered scoped so that handler dependencies such as a `DbContext` live per unit of work. [lifetimes.md](lifetimes.md) explains the lifetime choices and how to change them; [exceptions.md](exceptions.md) lists every exception RequestFlow throws and how to fix each one.
