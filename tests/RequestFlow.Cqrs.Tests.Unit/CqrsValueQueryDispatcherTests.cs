namespace RequestFlow.Cqrs.Tests.Unit;

public sealed class CqrsValueQueryDispatcherTests
{
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
    public void Given_A_Null_Value_Query_When_Sending_Then_Throws_Argument_Null_Exception()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => { _sut.SendAsync((IValueQuery<string>)null!); });

        exception.ParamName.ShouldBe("query");
    }

    [Fact]
    public void Given_A_Null_Core_Dispatcher_When_Creating_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => new CqrsValueQueryDispatcher(null!));
    }

    #region Initialization

    private readonly IValueRequestDispatcher _core =
        Substitute.For<IValueRequestDispatcher>();
    private readonly CqrsValueQueryDispatcher _sut;

    public CqrsValueQueryDispatcherTests()
        => _sut = new CqrsValueQueryDispatcher(_core);

    #endregion

    #region Helpers

    public sealed record FindName(string Id) : IValueQuery<string>;

    public sealed class FindNameHandler : IValueQueryHandler<FindName, string>
    {
        public ValueTask<string> HandleAsync(
            FindName request,
            CancellationToken cancellationToken)
            => new(request.Id);
    }

    #endregion
}
