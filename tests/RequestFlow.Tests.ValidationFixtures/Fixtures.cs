using System.Runtime.CompilerServices;
using RequestFlow;
using RequestFlow.Cqrs;

namespace RequestFlow.Tests.ValidationFixtures;

/// <summary>
/// A request with no handler anywhere in this assembly; startup validation must report it.
/// </summary>
public sealed record Lonely : IRequest<int>;

/// <summary>
/// A request with two handlers; startup validation must report the duplicate.
/// </summary>
public sealed record Duplicated : IRequest<int>;

public sealed class FirstDuplicatedHandler : IRequestHandler<Duplicated, int>
{
    public Task<int> HandleAsync(Duplicated request, CancellationToken cancellationToken)
        => Task.FromResult(1);
}

public sealed class SecondDuplicatedHandler : IRequestHandler<Duplicated, int>
{
    public Task<int> HandleAsync(Duplicated request, CancellationToken cancellationToken)
        => Task.FromResult(2);
}

/// <summary>
/// A request carrying two response contracts with a handler for each; startup validation must
/// report the duplicate. A stage declared over the int contract reaches the second handler only,
/// which is what the validation model has to see.
/// </summary>
public sealed record Forked : IRequest<string>, IRequest<int>;

public sealed class ForkedStringHandler : IRequestHandler<Forked, string>
{
    public Task<string> HandleAsync(Forked request, CancellationToken cancellationToken)
        => Task.FromResult("forked");
}

public sealed class ForkedIntHandler : IRequestHandler<Forked, int>
{
    public Task<int> HandleAsync(Forked request, CancellationToken cancellationToken)
        => Task.FromResult(1);
}

/// <summary>
/// A request whose handler declares a wider response type than the contract; startup validation
/// must report it under RF0112. Covariance on <c>IRequest&lt;TResponse&gt;</c> is what lets the pair compile.
/// </summary>
public sealed record Wide : IRequest<string>;

public sealed class WideHandler : IRequestHandler<Wide, object>
{
    public Task<object> HandleAsync(Wide request, CancellationToken cancellationToken)
        => Task.FromResult<object>("wide");
}

/// <summary>
/// Base request with a handler of its own; <see cref="Orphaned"/> inherits its contract.
/// </summary>
public record Rooted : IRequest<int>;

/// <summary>
/// Inherits <c>IRequest&lt;int&gt;</c> from <see cref="Rooted"/> but has no handler;
/// startup validation must report it.
/// </summary>
public sealed record Orphaned : Rooted;

public sealed class RootedHandler : IRequestHandler<Rooted, int>
{
    public Task<int> HandleAsync(Rooted request, CancellationToken cancellationToken)
        => Task.FromResult(0);
}

/// <summary>
/// Classified as both a command and a query; the AddCqrs validation rule must report it.
/// Handled so it adds no unhandled-request noise to tests that scan this assembly.
/// </summary>
public sealed record Confused : ICommand<int>, IQuery<int>;

public sealed class ConfusedHandler : IRequestHandler<Confused, int>
{
    public Task<int> HandleAsync(Confused request, CancellationToken cancellationToken)
        => Task.FromResult(0);
}

/// <summary>
/// A void command also classified as a query; exercises the split rule's
/// void-command path. Unhandled, like <see cref="Lonely"/>.
/// </summary>
public sealed record VoidConfused : ICommand, IQuery<int>;

/// <summary>
/// A command also classified as a stream query; the AddCqrs rule reports it as CQRS0001, and the
/// base contracts collide too, so core validation reports the same type as RF0109.
/// Handled on the command side so it adds no unhandled-request noise.
/// </summary>
public sealed record StreamConfused : ICommand<int>, IStreamQuery<string>;

public sealed class StreamConfusedHandler : IRequestHandler<StreamConfused, int>
{
    public Task<int> HandleAsync(StreamConfused request, CancellationToken cancellationToken)
        => Task.FromResult(0);
}

/// <summary>
/// A stream request with no handler anywhere in this assembly; startup validation must report it.
/// </summary>
public sealed record LonelyStream : IStreamRequest<int>;

/// <summary>
/// A stream request carrying two item types; startup validation must report it under RF0108.
/// Handled so it adds no unhandled-request noise.
/// </summary>
public sealed record ForkedStream : IStreamRequest<string>, IStreamRequest<int>;

public sealed class ForkedStreamHandler : IStreamRequestHandler<ForkedStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        ForkedStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        yield return 1;
    }
}

/// <summary>
/// A type carrying a request contract and a stream contract at once; startup validation must report
/// it under RF0109. Handled on the request side so it adds no unhandled-request noise.
/// </summary>
public sealed record MixedFamilies : IRequest<string>, IStreamRequest<int>;

public sealed class MixedFamiliesHandler : IRequestHandler<MixedFamilies, string>
{
    public Task<string> HandleAsync(MixedFamilies request, CancellationToken cancellationToken)
        => Task.FromResult("mixed");
}

/// <summary>
/// Carries both families through the CQRS contracts alone; startup validation must still report
/// it under RF0109. Handled on the query side so it adds no unhandled-request noise.
/// </summary>
public sealed record MixedCqrsFamilies : IQuery<string>, IStreamQuery<int>;

public sealed class MixedCqrsFamiliesHandler : IQueryHandler<MixedCqrsFamilies, string>
{
    public Task<string> HandleAsync(MixedCqrsFamilies request, CancellationToken cancellationToken)
        => Task.FromResult("mixed");
}

/// <summary>
/// A stream request whose handler declares a wider item type than the contract; startup validation
/// must report it under RF0110. Covariance on <c>IStreamRequest&lt;TItem&gt;</c> is what lets the pair compile.
/// </summary>
public sealed record WideStream : IStreamRequest<string>;

public sealed class WideStreamHandler : IStreamRequestHandler<WideStream, object>
{
    public async IAsyncEnumerable<object> Handle(
        WideStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        yield return "wide";
    }
}

/// <summary>
/// A stream request handled by an open generic handler closed through RegisterGenericHandler.
/// </summary>
public sealed record Counted : IStreamRequest<int>;

public sealed class GenericStreamHandler<TRequest> : IStreamRequestHandler<TRequest, int>
    where TRequest : IStreamRequest<int>
{
    public async IAsyncEnumerable<int> Handle(
        TRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        yield return 1;
    }
}

/// <summary>
/// A stream stage that reaches no request in this assembly; DisallowUnusedStages must report it.
/// </summary>
public sealed class UnreachedStreamStage : IStreamRequestStage<LonelyStream, int>
{
    public IAsyncEnumerable<int> Handle(
        LonelyStream request, StreamContinuation<int> next, CancellationToken cancellationToken)
        => next.Invoke(cancellationToken);
}

/// <summary>
/// An event with no applicable handler; startup validation must report it under RF0114.
/// </summary>
public sealed record UnhandledValidationEvent : IEvent;

/// <summary>
/// An abstract event used by a subscription that reaches no concrete event in this assembly.
/// </summary>
public abstract record DeadValidationEventBase : IEvent;

/// <summary>
/// An event interface used by a subscription that reaches no concrete event in this assembly.
/// </summary>
public interface IDeadValidationEvent : IEvent
{ }

/// <summary>
/// Declares two dead subscriptions so validation reports the base and interface contracts separately.
/// </summary>
public sealed class DeadValidationEventHandler :
    IEventHandler<DeadValidationEventBase>,
    IEventHandler<IDeadValidationEvent>
{
    public Task HandleAsync(
        DeadValidationEventBase @event, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task HandleAsync(IDeadValidationEvent @event, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

/// <summary>
/// A concrete event contract consumed by a handler in another assembly.
/// </summary>
public sealed record ExternalContractEvent : IEvent;

/// <summary>
/// A generic event whose closed int form has an exact handler.
/// </summary>
public sealed record ClosedGenericValidationEvent<T> : IEvent;

public sealed class ClosedGenericValidationEventHandler :
    IEventHandler<ClosedGenericValidationEvent<int>>
{
    public Task HandleAsync(
        ClosedGenericValidationEvent<int> @event, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

/// <summary>
/// Carries request and event contracts; startup validation must report it under RF0116.
/// </summary>
public sealed record RequestEventConflict : IRequest<int>, IEvent;

public sealed class RequestEventConflictRequestHandler :
    IRequestHandler<RequestEventConflict, int>
{
    public Task<int> HandleAsync(
        RequestEventConflict request, CancellationToken cancellationToken)
        => Task.FromResult(0);
}

public sealed class RequestEventConflictEventHandler :
    IEventHandler<RequestEventConflict>
{
    public Task HandleAsync(
        RequestEventConflict @event, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

/// <summary>
/// Carries stream-request and event contracts; startup validation must report it under RF0117.
/// </summary>
public sealed record StreamEventConflict : IStreamRequest<int>, IEvent;

public sealed class StreamEventConflictStreamHandler :
    IStreamRequestHandler<StreamEventConflict, int>
{
    public async IAsyncEnumerable<int> Handle(
        StreamEventConflict request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        yield return 0;
    }
}

public sealed class StreamEventConflictEventHandler :
    IEventHandler<StreamEventConflict>
{
    public Task HandleAsync(
        StreamEventConflict @event, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

/// <summary>
/// Carries request, stream-request, and event contracts so every pair is reported.
/// </summary>
public sealed record AllMessageContracts : IRequest<int>, IStreamRequest<int>, IEvent;

public sealed class AllMessageContractsRequestHandler :
    IRequestHandler<AllMessageContracts, int>
{
    public Task<int> HandleAsync(
        AllMessageContracts request, CancellationToken cancellationToken)
        => Task.FromResult(0);
}

public sealed class AllMessageContractsEventHandler :
    IEventHandler<AllMessageContracts>
{
    public Task HandleAsync(
        AllMessageContracts @event, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed record ForkedValue : IValueRequest<string>, IValueRequest<int>;

public sealed class ForkedValueHandler : IValueRequestHandler<ForkedValue, string>
{
    public ValueTask<string> HandleAsync(
        ForkedValue request,
        CancellationToken cancellationToken)
        => new("forked");
}

public sealed record WideValue : IValueRequest<string>;

public sealed class WideValueHandler : IValueRequestHandler<WideValue, object>
{
    public ValueTask<object> HandleAsync(
        WideValue request,
        CancellationToken cancellationToken)
        => new((object)"wide");
}

public sealed record MixedRequestValue : IRequest<string>, IValueRequest<int>;

public sealed record MixedStreamValue : IStreamRequest<string>, IValueRequest<int>;

public sealed record ValueEventConflict : IValueRequest<int>, IEvent;

public sealed record AllValueMessageContracts
    : IRequest<int>, IStreamRequest<int>, IValueRequest<int>, IEvent;

public sealed class MixedRequestValueHandler : IRequestHandler<MixedRequestValue, string>
{
    public Task<string> HandleAsync(
        MixedRequestValue request,
        CancellationToken cancellationToken)
        => Task.FromResult("task");
}

public sealed class MixedStreamValueHandler : IValueRequestHandler<MixedStreamValue, int>
{
    public ValueTask<int> HandleAsync(
        MixedStreamValue request,
        CancellationToken cancellationToken)
        => new(1);
}

public sealed class ValueEventValueHandler : IValueRequestHandler<ValueEventConflict, int>
{
    public ValueTask<int> HandleAsync(
        ValueEventConflict request,
        CancellationToken cancellationToken)
        => new(1);
}

public sealed class ValueEventHandler : IEventHandler<ValueEventConflict>
{
    public Task HandleAsync(
        ValueEventConflict @event,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class AllValueMessageContractsHandler
    : IRequestHandler<AllValueMessageContracts, int>
{
    public Task<int> HandleAsync(
        AllValueMessageContracts request,
        CancellationToken cancellationToken)
        => Task.FromResult(1);
}

public sealed class AllValueMessageContractsEventHandler
    : IEventHandler<AllValueMessageContracts>
{
    public Task HandleAsync(
        AllValueMessageContracts @event,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed record StageValue : IValueRequest<string>;

public sealed class StageValueHandler : IValueRequestHandler<StageValue, string>
{
    public ValueTask<string> HandleAsync(
        StageValue request,
        CancellationToken cancellationToken)
        => new("stage");
}

public sealed record MixedTaskQueryValueCommand
    : IQuery<int>, IValueCommand<int>;

public sealed class MixedTaskQueryValueCommandHandler
    : IQueryHandler<MixedTaskQueryValueCommand, int>
{
    public Task<int> HandleAsync(
        MixedTaskQueryValueCommand request,
        CancellationToken cancellationToken)
        => Task.FromResult(1);
}

public sealed class WideValueStage<TRequest> : IValueRequestStage<TRequest, object>
    where TRequest : IValueRequest<object>
{
    public ValueTask<object> HandleAsync(
        TRequest request,
        ValueContinuation<object> next,
        CancellationToken cancellationToken)
        => next.InvokeAsync(cancellationToken);
}
