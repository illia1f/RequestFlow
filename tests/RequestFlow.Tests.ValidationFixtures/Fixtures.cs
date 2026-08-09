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
