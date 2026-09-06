using Microsoft.Extensions.DependencyInjection;
using RequestFlow;
using RequestFlow.Cqrs;

namespace RequestFlow.Cqrs.Tests.Unit;

public sealed class CqrsPipelineInspectionTests
{
    [Theory]
    [InlineData(typeof(Price), typeof(IQueryHandler<,>), typeof(IRequestHandler<Price, int>), RequestPipelineFamily.Task)]
    [InlineData(typeof(CachedPrice), typeof(IValueQueryHandler<,>), typeof(IValueRequestHandler<CachedPrice, int>), RequestPipelineFamily.ValueTask)]
    [InlineData(typeof(Prices), typeof(IStreamQueryHandler<,>), typeof(IStreamRequestHandler<Prices, int>), RequestPipelineFamily.Stream)]
    public void Given_A_Cqrs_Handler_When_Inspecting_Then_Distinguishes_Its_Declared_Contract_From_The_Core_Service(
        Type requestType, Type declaredContract, Type serviceType, RequestPipelineFamily family)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options => options
            .AddHandler<PriceHandler>()
            .AddHandler<CachedPriceHandler>()
            .AddHandler<PricesHandler>()).AddCqrs();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestPipeline pipeline = provider.InspectRequestFlow(requestType);

        pipeline.DeclaredHandler.ContractType.ShouldBe(declaredContract);
        pipeline.HandlerServiceType.ShouldBe(serviceType);
        pipeline.Family.ShouldBe(family);
    }

    #region Helpers

    private sealed record Price : IQuery<int>;

    private sealed record CachedPrice : IValueQuery<int>;

    private sealed record Prices : IStreamQuery<int>;

    private sealed class PriceHandler : IQueryHandler<Price, int>
    {
        public Task<int> HandleAsync(Price request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Inspection must not invoke the handler.");
    }

    private sealed class CachedPriceHandler : IValueQueryHandler<CachedPrice, int>
    {
        public ValueTask<int> HandleAsync(CachedPrice request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Inspection must not invoke the handler.");
    }

    private sealed class PricesHandler : IStreamQueryHandler<Prices, int>
    {
        public IAsyncEnumerable<int> Handle(Prices request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Inspection must not invoke the handler.");
    }

    #endregion
}
