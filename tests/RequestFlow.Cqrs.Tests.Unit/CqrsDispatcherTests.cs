using RequestFlow;
using RequestFlow.Cqrs;

namespace RequestFlow.Cqrs.Tests.Unit;

public sealed class CqrsDispatcherTests
{
    [Fact]
    public async Task Given_Typed_Command_When_Sending_Command_Then_Forwards_Command_And_Token_And_Returns_The_Same_Task()
    {
        var command = new Rename("bob");
        using var cts = new CancellationTokenSource();
        Task<string> expected = Task.FromResult("renamed bob");
        _requests.SendAsync(command, cts.Token).Returns(expected);

        Task<string> result = _sut.SendAsync(command, cts.Token);

        result.ShouldBeSameAs(expected);
        (await result).ShouldBe("renamed bob");
        await _requests.Received(1).SendAsync(command, cts.Token);
    }

    [Fact]
    public async Task Given_Void_Command_When_Sending_Command_Then_Forwards_To_Void_Send()
    {
        var command = new Purge();
        using var cts = new CancellationTokenSource();
        var completion = new TaskCompletionSource<object?>();
        _requests.SendAsync((IRequest)command, cts.Token).Returns(completion.Task);

        Task result = _sut.SendAsync(command, cts.Token);

        result.ShouldBeSameAs(completion.Task);
        completion.SetResult(null);
        await result;
        await _requests.Received(1).SendAsync((IRequest)command, cts.Token);
    }

    [Fact]
    public async Task Given_Query_When_Sending_Query_Then_Forwards_Query_And_Token_And_Returns_The_Same_Task()
    {
        var query = new FindName("42");
        using var cts = new CancellationTokenSource();
        Task<string> expected = Task.FromResult("bob");
        _requests.SendAsync(query, cts.Token).Returns(expected);

        Task<string> result = _sut.SendAsync(query, cts.Token);

        result.ShouldBeSameAs(expected);
        (await result).ShouldBe("bob");
        await _requests.Received(1).SendAsync(query, cts.Token);
    }

    [Fact]
    public async Task Given_Null_Typed_Command_When_Sending_Command_Then_Throws_Argument_Null_Exception()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.SendAsync((ICommand<string>)null!));
    }

    [Fact]
    public async Task Given_Null_Void_Command_When_Sending_Command_Then_Throws_Argument_Null_Exception()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.SendAsync((ICommand)null!));
    }

    [Fact]
    public async Task Given_Null_Query_When_Sending_Query_Then_Throws_Argument_Null_Exception()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.SendAsync((IQuery<string>)null!));
    }

    [Fact]
    public void Given_Null_Request_Dispatcher_When_Creating_Dispatcher_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => new CqrsDispatcher(null!));
    }

    #region Initialization

    private readonly IRequestDispatcher _requests;
    private readonly CqrsDispatcher _sut;

    public CqrsDispatcherTests()
    {
        _requests = Substitute.For<IRequestDispatcher>();
        _sut = new CqrsDispatcher(_requests);
    }

    #endregion

    #region Helpers

    // Public so NSubstitute can proxy dispatcher interfaces closed over these types.
    public sealed record Rename(string Name) : ICommand<string>;

    public sealed record Purge : ICommand;

    public sealed record FindName(string Id) : IQuery<string>;

    // The assembly scan demands one handler per request type, even for requests only mocked here.
    public sealed class RenameHandler : ICommandHandler<Rename, string>
    {
        public Task<string> HandleAsync(Rename request, CancellationToken cancellationToken)
            => Task.FromResult(request.Name);
    }

    public sealed class PurgeHandler : ICommandHandler<Purge>
    {
        public Task HandleAsync(Purge request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class FindNameHandler : IQueryHandler<FindName, string>
    {
        public Task<string> HandleAsync(FindName request, CancellationToken cancellationToken)
            => Task.FromResult(request.Id);
    }

    #endregion
}
