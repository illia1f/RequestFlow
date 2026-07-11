# Registration

Every `AddRequestFlow` option: what each one registers and when to reach for it.

## The options

`AddRequestFlow` takes a configure delegate, and every option chains on it:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .WithHandlerLifetime(ServiceLifetime.Scoped));
```

| Option                                        | What it does                                                                  |
| --------------------------------------------- | ------------------------------------------------------------------------------ |
| `RegisterHandlersFromAssemblyContaining<T>()` | Scans the assembly containing `T`                                              |
| `RegisterHandlersFromAssembly(assembly)`      | Scans the given assembly                                                        |
| `RegisterGenericHandler(handlerType, ...)`    | Closes an open generic handler over the declared types                          |
| `AllowUnhandledRequests()`                    | Skips the missing-handler check at startup validation                           |
| `WithHandlerLifetime(lifetime)`               | Lifetime for this call's handlers, transient by default (see [lifetimes.md](lifetimes.md)) |
| `WithTransientDispatcher()`                   | Registers the dispatcher transient instead of scoped (see [lifetimes.md](lifetimes.md))    |

## What the scan picks up

The scan looks at every concrete class in the configured assemblies and registers those that implement `IRequestHandler<TRequest, TResponse>` or `IRequestHandler<TRequest>`. A class implementing several handler interfaces registers once per interface, so one class can handle several request types.

The scan also records every request type it sees. Startup validation uses that list to report requests no handler covers.

Abstract classes, interfaces, and open generic definitions are skipped. Open generic handlers need an explicit declaration, covered below.

## Multiple calls are additive

`AddRequestFlow` can be called any number of times, once per module for example. Each call adds to the same registration, and validation checks the combined result. An assembly registered by an earlier call is skipped, so its handlers keep the settings of the call that first registered it:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Orders.Module>()
    .WithHandlerLifetime(ServiceLifetime.Scoped));

services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Reporting.Module>());
```

## Generic handlers

One handler implementation can serve a family of generic requests. The scan ignores open generics, so each closing is declared with `RegisterGenericHandler`:

```csharp
public sealed record Audit<T>(string Payload) : IRequest<string>;

public sealed class AuditHandler<T> : IRequestHandler<Audit<T>, string>
{
    public Task<string> HandleAsync(Audit<T> request, CancellationToken cancellationToken)
        => Task.FromResult($"{typeof(T).Name}:{request.Payload}");
}

services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Program>()
    .RegisterGenericHandler(typeof(AuditHandler<>), typeof(Order), typeof(User)));
```

Each declared closing produces one concrete handler: `AuditHandler<Order>` handles `Audit<Order>`, `AuditHandler<User>` handles `Audit<User>`. A request whose closing was not declared has no handler:

```csharp
await dispatcher.SendAsync(new Audit<Order>("o1"));   // handled by AuditHandler<Order>
await dispatcher.SendAsync(new Audit<Refund>("r7"));  // HandlerNotFoundException
```

The undeclared closing surfaces at dispatch rather than at startup, because a generic request definition is not a scannable request type. Declare every closing you dispatch.

The handler type must be a concrete open generic definition with exactly one type parameter that implements a handler interface. Each closing type must be a closed type that satisfies the handler's `where` constraints. A declaration that breaks these rules fails startup validation with a problem naming the type; the full list is in [exceptions.md](exceptions.md). Declaring the same closing twice does nothing: the first declaration wins, the same rule as repeated assemblies.

## AllowUnhandledRequests

By default every scanned request type must have a handler, checked at startup validation. `AllowUnhandledRequests` skips that check. The intended case is a contracts assembly whose requests are handled in a different application:

```csharp
services.AddRequestFlow(o => o
    .RegisterHandlersFromAssemblyContaining<Contracts.CreateOrder>()
    .AllowUnhandledRequests());
```

The setting is sticky: once any call opts in, the check is off for every registered assembly, not only that call's. The safety net also moves: a request that reaches `SendAsync` without a handler now throws `HandlerNotFoundException` at dispatch instead of failing at startup. The duplicate-handler check stays on either way.

## ValidateRequestFlow

Registration problems normally surface the first time a dispatcher is resolved. `ValidateRequestFlow` runs the same validation right after the provider is built, so a misconfigured application fails at startup instead of on its first request:

```csharp
var app = builder.Build();
app.Services.ValidateRequestFlow();
```

It returns the provider, so it chains in non-hosted code too:

```csharp
IServiceProvider provider = services.BuildServiceProvider().ValidateRequestFlow();
```

All problems are reported in one `RequestFlowValidationException`, not one at a time (see [exceptions.md](exceptions.md)). The container's own `ValidateOnBuild` cannot catch these problems; [lifetimes.md](lifetimes.md) explains why and covers validation timing in detail.
