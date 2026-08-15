using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class TokenLinkTests
{
    // The common shape dispatcher.Stream(request, ct).WithCancellation(ct) hands the same token to
    // both sides, so joining them buys nothing and must not allocate.
    [Fact]
    public void Given_The_Same_Token_Twice_When_Combining_Then_No_Source_Is_Allocated()
    {
        using var source = new CancellationTokenSource();

        CancellationTokenSource? result = TokenLink.Combine(source.Token, source.Token, out CancellationToken linked);

        result.ShouldBeNull();
        linked.ShouldBe(source.Token);
    }
}
