using RequestFlow;
using RequestFlow.Cqrs;

namespace RequestFlow.Cqrs.Tests.Unit;

public sealed class CqrsQueryDispatcherTests
{
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
    public void Given_Null_Query_When_Sending_Query_Then_Throws_Argument_Null_Exception()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => { _sut.SendAsync((IQuery<string>)null!); });

        exception.ParamName.ShouldBe("query");
    }

    [Fact]
    public void Given_Null_Request_Dispatcher_When_Creating_Dispatcher_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => new CqrsQueryDispatcher(null!));
    }

    #region Initialization

    private readonly IRequestDispatcher _requests;
    private readonly CqrsQueryDispatcher _sut;

    public CqrsQueryDispatcherTests()
    {
        _requests = Substitute.For<IRequestDispatcher>();
        _sut = new CqrsQueryDispatcher(_requests);
    }

    #endregion

    #region Helpers

    // Public so NSubstitute can proxy dispatcher interfaces closed over this type.
    public sealed record FindName(string Id) : IQuery<string>;

    // The assembly scan demands one handler per request type, even for requests only mocked here.
    public sealed class FindNameHandler : IQueryHandler<FindName, string>
    {
        public Task<string> HandleAsync(FindName request, CancellationToken cancellationToken)
            => Task.FromResult(request.Id);
    }

    #endregion
}
