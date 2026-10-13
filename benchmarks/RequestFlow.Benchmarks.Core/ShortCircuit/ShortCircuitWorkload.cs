using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Benchmarks.Core;

public static class ShortCircuitWorkload
{
    public const int RejectedResult = 71;
    public const int CachedResult = 73;
    public const int HandledResult = 79;

    public static readonly Task<int> Rejected = Task.FromResult(RejectedResult);
    public static readonly Task<int> Cached = Task.FromResult(CachedResult);
    public static readonly Task<int> Handled = Task.FromResult(HandledResult);

    public static BenchmarkExpectation Expectation(IReadOnlyDictionary<string, object?> parameters)
        => Expectation((ShortCircuitOutcome)parameters["Outcome"]!);

    public static BenchmarkExpectation Expectation(ShortCircuitOutcome outcome) => outcome switch
    {
        ShortCircuitOutcome.AuthorizationReject => new(["authorization"], RejectedResult),
        ShortCircuitOutcome.CacheHit => new(["authorization", "cache"], CachedResult),
        ShortCircuitOutcome.FullExecution => new(["authorization", "cache", "transaction", TraceNames.Handler], HandledResult),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    public static void AddDependencies(
        IServiceCollection services,
        int bufferBytes = 0,
        ConstructionTrace? trace = null)
    {
        services.AddScoped<ScopeWitness>();
        services.AddSingleton(new ShortCircuitSettings(bufferBytes, trace));
        services.AddScoped<DependencyState>();

        services.AddScoped<AccessPolicy>();
        services.AddScoped<CachePolicy>();

        services.AddScoped<ChangeBuffer>();
        services.AddScoped<UnitOfWork>();

        services.AddScoped<CommandBuffer>();
        services.AddScoped<DataSession>();
        services.AddScoped<Repository>();
    }
}
