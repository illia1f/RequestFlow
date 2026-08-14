using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class StreamDispatcherTests
{
    [Fact]
    public void Given_A_Null_Request_When_Streaming_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => _sut.Stream<int>(null!));
    }

    [Fact]
    public void Given_An_Unmapped_Request_When_Streaming_Then_Throws_From_The_Call()
    {
        HandlerNotFoundException exception =
            Should.Throw<HandlerNotFoundException>(() => _sut.Stream(new Unmapped()));

        exception.RequestType.ShouldBe(typeof(Unmapped));
    }

    // The item type is a reference type here on purpose. Variance conversions do not apply to a
    // value type, so IStreamRequest<int> is not an IStreamRequest<object> and the covariant call
    // would not compile against Tail.
    [Fact]
    public void Given_A_Covariant_Item_Type_When_Streaming_Then_Throws_From_The_Call()
    {
        ResponseTypeMismatchException exception =
            Should.Throw<ResponseTypeMismatchException>(() => _sut.Stream<object>(new Words()));

        exception.RequestType.ShouldBe(typeof(Words));
        exception.ExpectedResponseType.ShouldBe(typeof(string));
        exception.ActualResponseType.ShouldBe(typeof(object));
    }

    [Fact]
    public async Task Given_A_Mapped_Request_When_Enumerating_Then_Yields_The_Handlers_Items()
    {
        List<int> items = await _sut.Stream(new Tail(3)).CollectAsync();

        items.ShouldBe([0, 1, 2]);
    }

    // Lookup is eager and iteration is cold, so nothing of the handler's runs until MoveNextAsync.
    [Fact]
    public async Task Given_A_Mapped_Request_When_Streaming_Without_Enumerating_Then_The_Handler_Has_Not_Run()
    {
        TailHandler.Started = 0;

        IAsyncEnumerable<int> stream = _sut.Stream(new Tail(3));

        TailHandler.Started.ShouldBe(0);

        await stream.CollectAsync();

        TailHandler.Started.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Handler_Returning_A_Null_Sequence_When_Enumerating_Then_Throws_Handler_Null_Stream_Exception()
    {
        HandlerNullStreamException exception = await Should.ThrowAsync<HandlerNullStreamException>(
            () => _sut.Stream(new Nothing()).CollectAsync());

        exception.RequestType.ShouldBe(typeof(Nothing));
    }

    [Fact]
    public void Given_A_Handler_Returning_A_Null_Sequence_When_Streaming_Then_The_Call_Itself_Does_Not_Throw()
    {
        Should.NotThrow(() => _sut.Stream(new Nothing()));
    }

    [Fact]
    public async Task Given_A_Handler_That_Throws_When_Enumerating_Then_The_Exception_Reaches_The_Caller_Unwrapped()
    {
        NotSupportedException exception = await Should.ThrowAsync<NotSupportedException>(
            () => _sut.Stream(new Boom()).CollectAsync());

        exception.Message.ShouldBe("from handler");
    }

    [Fact]
    public void Given_Both_Dispatchers_On_One_Provider_When_Resolving_Both_Then_Validation_Runs_Once()
    {
        CountingRule.Runs = 0;
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<StreamDispatcherTests>())
            .AddValidationRule<CountingRule>();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        scope.ServiceProvider.GetRequiredService<IStreamDispatcher>();

        CountingRule.Runs.ShouldBe(1);
    }

    // Exercises the whole seam the other tests build around: the scan, PlanTypeFor's stream
    // branch, the plan landing in the frozen map, and the dispatcher resolved from a real
    // container, rather than a hand-built map and a substituted provider.
    [Fact]
    public async Task Given_A_Scanned_Stream_Handler_When_Streaming_Through_The_Container_Then_Yields_Its_Items()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<StreamDispatcherTests>());
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IStreamDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IStreamDispatcher>();

        List<int> items = await dispatcher.Stream(new Tail(3)).CollectAsync();

        items.ShouldBe([0, 1, 2]);
    }

    #region Initialization

    private readonly StreamDispatcher _sut;

    public StreamDispatcherTests()
    {
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IStreamRequestHandler<Tail, int>)).Returns(new TailHandler());
        services.GetService(typeof(IStreamRequestHandler<Nothing, int>)).Returns(new NothingHandler());
        services.GetService(typeof(IStreamRequestHandler<Words, string>)).Returns(new WordsHandler());
        services.GetService(typeof(IStreamRequestHandler<Boom, int>)).Returns(new BoomHandler());

        // The map omits Unmapped on purpose so dispatch can miss.
        var plans = new Dictionary<Type, RequestPlanBase>
        {
            [typeof(Tail)] = new StreamPlan<Tail, int>(),
            [typeof(Nothing)] = new StreamPlan<Nothing, int>(),
            [typeof(Words)] = new StreamPlan<Words, string>(),
            [typeof(Boom)] = new StreamPlan<Boom, int>(),
        };
        _sut = new StreamDispatcher(new DispatchMap(plans), services);
    }

    #endregion

    #region Helpers

    public sealed record Tail(int Count) : IStreamRequest<int>;

    public sealed record Nothing : IStreamRequest<int>;

    public sealed record Words : IStreamRequest<string>;

    public sealed record Unmapped : IStreamRequest<int>;

    public sealed record Boom : IStreamRequest<int>;

    // Handle itself is not an iterator, so Started moves the moment it is called rather than on
    // first MoveNextAsync. That is what lets the cold-iteration test tell a deferring dispatcher
    // apart from one that resolves and calls the handler eagerly.
    public sealed class TailHandler : IStreamRequestHandler<Tail, int>
    {
        public static int Started;

        public IAsyncEnumerable<int> Handle(Tail request, CancellationToken cancellationToken)
        {
            Started++;

            return Iterate(request, cancellationToken);
        }

        private static async IAsyncEnumerable<int> Iterate(
            Tail request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 0; i < request.Count; i++)
            {
                await Task.Yield();
                yield return i;
            }
        }
    }

    public sealed class NothingHandler : IStreamRequestHandler<Nothing, int>
    {
        public IAsyncEnumerable<int> Handle(Nothing request, CancellationToken cancellationToken)
            => null!;
    }

    public sealed class WordsHandler : IStreamRequestHandler<Words, string>
    {
        public async IAsyncEnumerable<string> Handle(
            Words request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return "word";
        }
    }

    public sealed class UnmappedHandler : IStreamRequestHandler<Unmapped, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Unmapped request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return 0;
        }
    }

    // Yields one item before it throws, so the exception has to cross a live enumerator and the
    // dispatcher's own await foreach on its way out.
    public sealed class BoomHandler : IStreamRequestHandler<Boom, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Boom request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return 0;

            throw new NotSupportedException("from handler");
        }
    }

    private sealed class CountingRule : IRequestFlowValidationRule
    {
        public static int Runs;

        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
        {
            Runs++;
            return [];
        }
    }

    #endregion
}
