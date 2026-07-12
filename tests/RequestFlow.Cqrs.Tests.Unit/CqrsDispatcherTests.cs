using RequestFlow;
using RequestFlow.Cqrs;

namespace RequestFlow.Cqrs.Tests.Unit;

public sealed class CqrsDispatcherTests
{
    [Fact]
    public async Task Given_Typed_Command_When_Sending_Command_Then_Returns_Request_Dispatcher_Response()
    {
        var command = new Rename("bob");
        _requests.SendAsync(command, Arg.Any<CancellationToken>()).Returns("renamed bob");

        string result = await _sut.SendAsync(command);

        result.ShouldBe("renamed bob");
    }

    [Fact]
    public async Task Given_Typed_Command_When_Sending_Command_Then_Forwards_Command_And_Token()
    {
        var command = new Rename("bob");
        using var cts = new CancellationTokenSource();

        await _sut.SendAsync(command, cts.Token);

        await _requests.Received(1).SendAsync(command, cts.Token);
    }

    [Fact]
    public async Task Given_Void_Command_When_Sending_Command_Then_Forwards_To_Void_Send()
    {
        var command = new Purge();
        using var cts = new CancellationTokenSource();

        await _sut.SendAsync(command, cts.Token);

        await _requests.Received(1).SendAsync((IRequest)command, cts.Token);
    }

    [Fact]
    public async Task Given_Query_When_Sending_Query_Then_Returns_Request_Dispatcher_Response()
    {
        var query = new FindName("42");
        _requests.SendAsync(query, Arg.Any<CancellationToken>()).Returns("bob");

        string result = await _sut.SendAsync(query);

        result.ShouldBe("bob");
    }

    [Fact]
    public async Task Given_Query_When_Sending_Query_Then_Forwards_Query_And_Token()
    {
        var query = new FindName("42");
        using var cts = new CancellationTokenSource();

        await _sut.SendAsync(query, cts.Token);

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

    // AddRequestFlow scans this assembly and demands a handler per request type, so each
    // fixture record needs a concrete handler even though the tests only use the mocks.
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
