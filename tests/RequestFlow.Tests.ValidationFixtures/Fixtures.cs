using RequestFlow;

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
