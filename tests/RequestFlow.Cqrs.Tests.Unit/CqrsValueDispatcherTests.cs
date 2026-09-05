namespace RequestFlow.Cqrs.Tests.Unit;

public sealed class CqrsValueDispatcherTests
{
    [Fact]
    public async Task Given_A_Value_Command_When_Sending_Then_Forwards_It_And_The_Token()
    {
        using var source = new CancellationTokenSource();
        var completion = new TaskCompletionSource<string>();
        var command = new Rename("new-name");
        var expected = new ValueTask<string>(completion.Task);
        _core.SendAsync(command, source.Token)
            .Returns(expected);

        ValueTask<string> result = _sut.SendAsync(command, source.Token);

        result.ShouldBe(expected);
        completion.SetResult("renamed");
        (await result).ShouldBe("renamed");
        await _core.Received(1).SendAsync(command, source.Token);
    }

    [Fact]
    public async Task Given_A_Void_Value_Command_When_Sending_Then_Forwards_It_And_The_Token()
    {
        using var source = new CancellationTokenSource();
        var completion = new TaskCompletionSource<object?>();
        var command = new Purge();
        var expected = new ValueTask(completion.Task);
        _core.SendAsync((IValueRequest)command, source.Token)
            .Returns(expected);

        ValueTask result = _sut.SendAsync(command, source.Token);

        result.ShouldBe(expected);
        completion.SetResult(null);
        await result;
        await _core.Received(1).SendAsync((IValueRequest)command, source.Token);
    }

    [Fact]
    public async Task Given_A_Value_Query_When_Sending_Then_Forwards_It_And_The_Token()
    {
        using var source = new CancellationTokenSource();
        var completion = new TaskCompletionSource<string>();
        var query = new FindName("42");
        var expected = new ValueTask<string>(completion.Task);
        _core.SendAsync(query, source.Token)
            .Returns(expected);

        ValueTask<string> result = _sut.SendAsync(query, source.Token);

        result.ShouldBe(expected);
        completion.SetResult("name");
        (await result).ShouldBe("name");
        await _core.Received(1).SendAsync(query, source.Token);
    }

    [Fact]
    public async Task Given_A_Null_Value_Command_When_Sending_Then_Throws_Argument_Null_Exception()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _sut.SendAsync((IValueCommand<string>)null!));
    }

    [Fact]
    public async Task Given_A_Null_Void_Value_Command_When_Sending_Then_Throws_Argument_Null_Exception()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _sut.SendAsync((IValueCommand)null!));
    }

    [Fact]
    public async Task Given_A_Null_Value_Query_When_Sending_Then_Throws_Argument_Null_Exception()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _sut.SendAsync((IValueQuery<string>)null!));
    }

    [Fact]
    public void Given_A_Null_Core_Dispatcher_When_Creating_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => new CqrsValueDispatcher(null!));
    }

    #region Initialization

    private readonly IValueRequestDispatcher _core =
        Substitute.For<IValueRequestDispatcher>();
    private readonly CqrsValueDispatcher _sut;

    public CqrsValueDispatcherTests()
        => _sut = new CqrsValueDispatcher(_core);

    #endregion

    #region Helpers

    public sealed record Rename(string Name) : IValueCommand<string>;

    public sealed record Purge : IValueCommand;

    public sealed record FindName(string Id) : IValueQuery<string>;

    public sealed class RenameHandler : IValueCommandHandler<Rename, string>
    {
        public ValueTask<string> HandleAsync(
            Rename request,
            CancellationToken cancellationToken)
            => new(request.Name);
    }

    public sealed class PurgeHandler : IValueCommandHandler<Purge>
    {
        public ValueTask HandleAsync(Purge request, CancellationToken cancellationToken)
            => default;
    }

    public sealed class FindNameHandler : IValueQueryHandler<FindName, string>
    {
        public ValueTask<string> HandleAsync(
            FindName request,
            CancellationToken cancellationToken)
            => new(request.Id);
    }

    #endregion
}
