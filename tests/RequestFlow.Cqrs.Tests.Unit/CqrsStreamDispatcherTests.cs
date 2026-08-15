using System.Runtime.CompilerServices;
using RequestFlow;
using RequestFlow.Cqrs;

namespace RequestFlow.Cqrs.Tests.Unit;

public sealed class CqrsStreamDispatcherTests
{
    [Fact]
    public void Given_Stream_Query_When_Streaming_Query_Then_Returns_Stream_Dispatcher_Sequence()
    {
        var query = new ListNames();
        IAsyncEnumerable<int> sequence = Sequence();
        _streams.Stream(query, Arg.Any<CancellationToken>()).Returns(sequence);

        IAsyncEnumerable<int> result = _sut.Stream(query);

        result.ShouldBeSameAs(sequence);
    }

    [Fact]
    public void Given_Stream_Query_When_Streaming_Query_Then_Forwards_Query_And_Token()
    {
        var query = new ListNames();
        using var cts = new CancellationTokenSource();

        _sut.Stream(query, cts.Token);

        _streams.Received(1).Stream(query, cts.Token);
    }

    [Fact]
    public void Given_Null_Stream_Query_When_Streaming_Query_Then_Throws_Argument_Null_Exception()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(
            () => _sut.Stream((IStreamQuery<int>)null!));

        exception.ParamName.ShouldBe("query");
    }

    [Fact]
    public void Given_Null_Stream_Dispatcher_When_Creating_Dispatcher_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => new CqrsStreamDispatcher(null!));
    }

    #region Initialization

    private readonly IStreamDispatcher _streams;
    private readonly CqrsStreamDispatcher _sut;

    public CqrsStreamDispatcherTests()
    {
        _streams = Substitute.For<IStreamDispatcher>();
        _sut = new CqrsStreamDispatcher(_streams);
    }

    #endregion

    #region Helpers

    // Public so NSubstitute can proxy dispatcher interfaces closed over this type.
    public sealed record ListNames : IStreamQuery<int>;

    // The assembly scan demands one handler per request type, even for requests only mocked here.
    public sealed class ListNamesHandler : IStreamQueryHandler<ListNames, int>
    {
        public async IAsyncEnumerable<int> Handle(
            ListNames request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return 0;
        }
    }

    // Never enumerated; the test only checks the instance passes through untouched.
    private static async IAsyncEnumerable<int> Sequence()
    {
        await Task.Yield();
        yield return 1;
    }

    #endregion
}
