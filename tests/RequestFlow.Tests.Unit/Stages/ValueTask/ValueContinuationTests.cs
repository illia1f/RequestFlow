using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class ValueContinuationTests
{
    [Theory]
    [InlineData(typeof(ValueContinuation<string>))]
    [InlineData(typeof(ValueContinuation))]
    public void Given_A_Value_Continuation_Shape_When_Inspecting_Its_Type_Then_It_Is_A_Value_Type(Type shape)
        => shape.IsValueType.ShouldBeTrue();

    [Fact]
    public async Task Given_A_Typed_Continuation_Over_A_Delegate_When_Invoked_Then_It_Forwards_The_Selected_Token()
    {
        using var inherited = new CancellationTokenSource();
        using var replacement = new CancellationTokenSource();
        CancellationToken seen = default;
        ValueContinuation<string> sut = ValueContinuation<string>.Over(token =>
        {
            seen = token;
            return new ValueTask<string>("done");
        }, inherited.Token);

        string result = await sut.InvokeAsync(replacement.Token);

        result.ShouldBe("done");
        seen.ShouldBe(replacement.Token);
    }

    [Fact]
    public async Task Given_A_Void_Continuation_Over_A_Delegate_When_Invoked_Twice_Then_The_Delegate_Runs_Twice()
    {
        int calls = 0;
        ValueContinuation sut = ValueContinuation.Over(token =>
        {
            calls++;
            return default;
        });

        await sut.InvokeAsync();
        await sut.InvokeAsync();

        calls.ShouldBe(2);
    }

    [Fact]
    public async Task Given_A_Typed_Continuation_Over_A_Delegate_When_Invoked_Twice_Then_The_Delegate_Runs_Twice()
    {
        int calls = 0;
        ValueContinuation<string> sut = ValueContinuation<string>.Over(_ => new ValueTask<string>($"{++calls}"));

        await sut.InvokeAsync();
        await sut.InvokeAsync();

        calls.ShouldBe(2);
    }

    [Fact]
    public async Task Given_A_Typed_Continuation_Over_A_Delegate_When_Invoked_Without_A_Token_Then_It_Forwards_Cancellation_Token_None()
    {
        CancellationToken seen = default;
        ValueContinuation<string> sut = ValueContinuation<string>.Over(token =>
        {
            seen = token;
            return new ValueTask<string>("done");
        });

        await sut.InvokeAsync();

        seen.ShouldBe(CancellationToken.None);
    }

    [Fact]
    public async Task Given_A_Void_Continuation_Over_A_Delegate_When_Invoked_Without_A_Token_Then_It_Forwards_The_Inherited_Token()
    {
        using var inherited = new CancellationTokenSource();
        CancellationToken seen = default;
        ValueContinuation sut = ValueContinuation.Over(token =>
        {
            seen = token;
            return default;
        }, inherited.Token);

        await sut.InvokeAsync();

        seen.ShouldBe(inherited.Token);
    }

    [Fact]
    public void Given_A_Typed_Continuation_Over_A_Delegate_That_Throws_When_Invoked_Then_The_Exception_Escapes_Synchronously()
    {
        ValueContinuation<string> sut = ValueContinuation<string>.Over(_ => throw new InvalidTimeZoneException("no task"));

        var thrown = Should.Throw<InvalidTimeZoneException>(() => { _ = sut.InvokeAsync(); });

        thrown.Message.ShouldBe("no task");
    }

    [Fact]
    public void Given_A_Void_Continuation_Over_A_Delegate_That_Throws_When_Invoked_Then_The_Exception_Escapes_Synchronously()
    {
        ValueContinuation sut = ValueContinuation.Over(_ => throw new InvalidTimeZoneException("no task"));

        var thrown = Should.Throw<InvalidTimeZoneException>(() => { _ = sut.InvokeAsync(); });

        thrown.Message.ShouldBe("no task");
    }

    [Fact]
    public void Given_No_Delegate_When_Building_A_Typed_Value_Continuation_Over_It_Then_It_Refuses()
    {
        Should.Throw<ArgumentNullException>(() => { _ = ValueContinuation<string>.Over(null!); });
    }

    [Fact]
    public void Given_No_Delegate_When_Building_A_Void_Value_Continuation_Over_It_Then_It_Refuses()
    {
        Should.Throw<ArgumentNullException>(() => { _ = ValueContinuation.Over(null!); });
    }

    [Fact]
    public void Given_A_Default_Typed_Value_Continuation_When_Invoked_Then_It_Says_It_Was_Never_Built()
    {
        ValueContinuation<string> sut = default;

        var thrown = Should.Throw<InvalidOperationException>(() => sut.InvokeAsync());

        thrown.Message.ShouldContain("ValueContinuation<TResponse>.Over(rest)");
    }

    [Fact]
    public void Given_A_Default_Void_Value_Continuation_When_Invoked_Then_It_Says_It_Was_Never_Built()
    {
        ValueContinuation sut = default;

        var thrown = Should.Throw<InvalidOperationException>(() => sut.InvokeAsync());

        thrown.Message.ShouldContain("ValueContinuation.Over(rest)");
    }
}
