using RequestFlow;

namespace RequestFlow.Tests.Unit.Stages;

public sealed class StreamContinuationTests
{
    [Fact]
    public async Task Given_A_Continuation_Over_A_Delegate_When_Invoking_Then_Yields_The_Delegates_Items()
    {
        StreamContinuation<int> sut = StreamContinuation<int>.Over(_ => Items(1, 2, 3));

        List<int> items = await sut.Invoke().CollectAsync();

        items.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Given_A_Fallback_Token_When_Invoking_Without_One_Then_The_Delegate_Gets_The_Fallback()
    {
        using var source = new CancellationTokenSource();
        CancellationToken seen = default;
        StreamContinuation<int> sut = StreamContinuation<int>.Over(
            token =>
            {
                seen = token;
                return Items(1);
            },
            source.Token);

        await sut.Invoke().CollectAsync();

        seen.ShouldBe(source.Token);
    }

    [Fact]
    public async Task Given_A_Fallback_Token_When_Invoking_With_A_Token_Then_The_Delegate_Gets_The_Named_One()
    {
        using var fallback = new CancellationTokenSource();
        using var named = new CancellationTokenSource();
        CancellationToken seen = default;
        StreamContinuation<int> sut = StreamContinuation<int>.Over(
            token =>
            {
                seen = token;
                return Items(1);
            },
            fallback.Token);

        await sut.Invoke(named.Token).CollectAsync();

        seen.ShouldBe(named.Token);
    }

    // Invoke enters the level below on the call, so a stage that invokes and drops the sequence has
    // already run it.
    [Fact]
    public void Given_A_Continuation_Over_A_Delegate_When_Invoking_Without_Enumerating_Then_The_Delegate_Has_Run()
    {
        bool ran = false;
        StreamContinuation<int> sut = StreamContinuation<int>.Over(
            _ =>
            {
                ran = true;
                return Items(1);
            });

        _ = sut.Invoke();

        ran.ShouldBeTrue();
    }

    // Invoke is not an iterator, so the guard throws from the call rather than at first enumeration.
    [Fact]
    public void Given_A_Default_Continuation_When_Invoking_Then_Throws_Invalid_Operation_Exception()
    {
        StreamContinuation<int> sut = default;

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => sut.Invoke());

        exception.Message.ShouldContain("StreamContinuation<TItem>.Over");
    }

    [Fact]
    public void Given_A_Null_Delegate_When_Building_A_Continuation_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => StreamContinuation<int>.Over(null!));
    }

    #region Helpers

    private static async IAsyncEnumerable<int> Items(params int[] values)
    {
        foreach (int value in values)
        {
            await Task.Yield();
            yield return value;
        }
    }

    #endregion
}
