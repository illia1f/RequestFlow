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
/// must report it under RF0112. Covariance on <c>IRequest&lt;TResponse&gt;</c> is what lets the pair
/// compile.
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
/// A void command also classified as a query; exercises the split rule's void-command path.
/// Unhandled, like <see cref="Lonely"/>.
/// </summary>
public sealed record VoidConfused : ICommand, IQuery<int>;

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
/// A stream request whose handler declares a wider item type than the contract; startup validation
/// must report it under RF0110. Covariance on <c>IStreamRequest&lt;TItem&gt;</c> is what lets the
/// pair compile.
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
