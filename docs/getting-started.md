# Getting started

From an empty project to the first dispatched request.

## Install

Install the preview runtime package:

```
dotnet add package RequestFlow --prerelease
```

`RequestFlow` includes `RequestFlow.Abstractions`. Projects that only define requests and handlers can reference the abstractions package alone, including for [ValueTask](value-tasks.md) and [stream](streaming.md#packages) handlers. The package has no dependencies on `net8.0` or `net10.0`; on `netstandard2.0` and `net462`, it depends on `Microsoft.Bcl.AsyncInterfaces`.

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

Keep `IRequest` as the default. Consider the [ValueTask request family](value-tasks.md) when measurements show that `Task` allocations matter on a path that completes synchronously.

## Register

`AddRequestFlow` scans the assemblies you point it at and registers every handler it finds:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly());
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
    .RegisterHandlersFromCallingAssembly());

IServiceProvider provider = services.BuildServiceProvider().ValidateRequestFlow();

using IServiceScope scope = provider.CreateScope();
var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

OrderId id = await dispatcher.SendAsync(new CreateOrder("c42"));
```

Resolve the dispatcher inside a scope so dependencies such as `DbContext` live for one unit of work. See [Service lifetimes](lifetimes.md) for configuration and [Exceptions](exceptions.md) for failures and fixes.
