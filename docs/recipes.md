# Recipes

Stages and adapters to paste into an application that is moving off MediatR.

## Pre and post processing

- One stage replaces `IRequestPreProcessor` and `IRequestPostProcessor`: code before `next.InvokeAsync()` is the pre step, code after it is the post step, and both share the method's locals.
- The post step runs only when the rest of the chain returns. Put it in a `finally` to run it after a failure too.
- Registration order is execution order, outermost first ([Registering](stages.md#registering)). An open two-parameter stage also wraps [void requests](stages.md#void-requests).

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RequestFlow;

public sealed class TimingStage<TRequest, TResponse>(ILogger<TimingStage<TRequest, TResponse>> logger)
    : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();

        TResponse response = await next.InvokeAsync();

        logger.LogInformation(
            "{Request} took {Elapsed}", typeof(TRequest).Name, Stopwatch.GetElapsedTime(started));
        return response;
    }
}
```

- To keep existing processor classes, put them behind an application-owned interface and run them from one stage. RequestFlow does not scan for that interface, so the application registers each implementation.

```csharp
public interface IRequestPreparation<TRequest>
{
    Task PrepareAsync(TRequest request, CancellationToken cancellationToken);
}

public sealed class PreparationStage<TRequest, TResponse>(
    IEnumerable<IRequestPreparation<TRequest>> preparations)
    : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
    {
        foreach (IRequestPreparation<TRequest> preparation in preparations)
            await preparation.PrepareAsync(request, cancellationToken);

        return await next.InvokeAsync();
    }
}

services.AddTransient<IRequestPreparation<CreateOrder>, AuthorizeCreateOrder>();
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .AddStage(typeof(TimingStage<,>))
    .AddStage(typeof(PreparationStage<,>)));
```

## Exception handling

- A `try`/`catch` around `next.InvokeAsync()` replaces both `IRequestExceptionAction`, which observes and rethrows, and `IRequestExceptionHandler`, which replaces the response.
- RequestFlow never wraps handler or stage exceptions, so the `catch` sees the original type ([What RequestFlow never wraps](exceptions.md#what-requestflow-never-wraps)).
- A stage sees failures from the levels below it only. Register it before the stages whose failures it should observe.

```csharp
using Microsoft.Extensions.Logging;
using RequestFlow;

public sealed class ExceptionLoggingStage<TRequest, TResponse>(
    ILogger<ExceptionLoggingStage<TRequest, TResponse>> logger)
    : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next.InvokeAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Request} failed", typeof(TRequest).Name);
            throw;
        }
    }
}
```

- To replace the response, return one from the `catch` instead of rethrowing. The stage has to construct that response, so it names the response type: a closed stage for one request, or the one-parameter form for a shared result type, which [`ErrorTranslationStage`](stages.md#which-requests-a-stage-reaches) shows.

## FluentValidation

- RequestFlow takes no dependency on FluentValidation and does not register validators. Keep the validator registration the application already has.
- A request with no validator passes through, because the container supplies an empty sequence.

```csharp
using FluentValidation;
using FluentValidation.Results;
using RequestFlow;

public sealed class ValidationStage<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
    {
        List<ValidationFailure> failures = [];

        foreach (IValidator<TRequest> validator in validators)
        {
            ValidationResult result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next.InvokeAsync();
    }
}

services.AddScoped<IValidator<CreateOrder>, CreateOrderValidator>();
services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .AddStage(typeof(ValidationStage<,>)));
```

## Tracing with ActivitySource

- The stage holds no per-request state, so it registers as a singleton ([Lifetime](stages.md#lifetime)).
- The source sits in a non-generic class. A static field in the generic stage would create one `ActivitySource` per closed stage type.
- `ActivitySource` is in-box on `net8.0` and later. On `netstandard2.0` or `net462`, the application references `System.Diagnostics.DiagnosticSource` itself.
- On .NET 9 and later, `activity?.AddException(exception)` also records the exception as an event.

```csharp
using System.Diagnostics;
using RequestFlow;

public static class RequestTracing
{
    public static readonly ActivitySource Source = new("MyApp.Requests");
}

public sealed class TracingStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
    {
        using Activity? activity = RequestTracing.Source.StartActivity(typeof(TRequest).Name);

        try
        {
            return await next.InvokeAsync();
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw;
        }
    }
}

services.AddRequestFlow(o => o
    .RegisterHandlersFromCallingAssembly()
    .AddStage(typeof(TracingStage<,>), s => s.AsSingleton()));
```

## Untyped send

- RequestFlow has no `Send(object)`, by design. This adapter is application code over `IRequestDispatcher`. Register it scoped, like the dispatcher: `services.AddScoped<UntypedRequestSender>()`.
- The first send of a request type finds its `IRequest<TResponse>` contract by reflection and caches a delegate. A void request returns `null`.
- It covers Task requests only. ValueTask and stream requests need their own adapter over `IValueRequestDispatcher` or `IStreamDispatcher`.
- Events need no adapter: `IEventPublisher.PublishAsync(IEvent)` already dispatches on the runtime type.
- A request type with no handler still throws `HandlerNotFoundException`.

```csharp
using System.Collections.Concurrent;
using System.Reflection;
using RequestFlow;

public sealed class UntypedRequestSender(IRequestDispatcher dispatcher)
{
    private delegate Task<object?> Sender(
        IRequestDispatcher dispatcher, object request, CancellationToken cancellationToken);

    private static readonly ConcurrentDictionary<Type, Sender> Senders = new();

    private static readonly MethodInfo SendTypedMethod = typeof(UntypedRequestSender)
        .GetMethod(nameof(SendTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public Task<object?> SendAsync(object request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        Sender sender = Senders.GetOrAdd(request.GetType(), CreateSender);
        return sender(dispatcher, request, cancellationToken);
    }

    private static Sender CreateSender(Type requestType)
    {
        Type contract = Array.Find(
            requestType.GetInterfaces(),
            static type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequest<>))
            ?? throw new ArgumentException($"'{requestType}' does not implement IRequest<TResponse>.");

        return (Sender)SendTypedMethod
            .MakeGenericMethod(contract.GetGenericArguments()[0])
            .CreateDelegate(typeof(Sender));
    }

    private static async Task<object?> SendTypedAsync<TResponse>(
        IRequestDispatcher dispatcher, object request, CancellationToken cancellationToken)
    {
        TResponse response = await dispatcher.SendAsync((IRequest<TResponse>)request, cancellationToken);
        return response is NoResult ? null : response;
    }
}
```

## Dispatching from a hosted service

- Dispatchers and the publisher are scoped by default, so a singleton such as a hosted service resolves them from a scope it creates. [Why the dispatcher is scoped](lifetimes.md#why-the-dispatcher-is-scoped) has the reason and the single-send form.
- Create the scope inside the loop. Each unit of work then gets its own scoped dependencies, such as a `DbContext`, disposed when the iteration ends.
- An exception from `SendAsync` leaves `ExecuteAsync` and ends the loop. Catch inside the loop what the worker should survive.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RequestFlow;

public sealed class OutboxWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

            await dispatcher.SendAsync(new FlushOutbox(), stoppingToken);
        }
    }
}
```
