using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Benchmarks.Core;

public enum DependencyResolution
{
    EnteredOnly,
    AllStages,
    EntireChain
}

public static class ShortCircuitVerification
{
    public static void Verify<TDispatcher, TMessage>(
        string library,
        Func<int, ConstructionTrace, ServiceProvider> build,
        Func<ShortCircuitOutcome, TMessage> createMessage,
        Func<TDispatcher, TMessage, CancellationToken, ValueTask<int>> send,
        DependencyResolution resolution,
        bool childScopePerSend = false,
        int authorizationDisposals = 1,
        ServiceLifetime dispatcherLifetime = ServiceLifetime.Scoped)
        where TDispatcher : notnull
        where TMessage : ShortCircuitMessage
    {
        foreach (int bufferBytes in new[] { 0, 4096 })
        foreach (ShortCircuitOutcome outcome in Enum.GetValues<ShortCircuitOutcome>())
        {
            var construction = new ConstructionTrace();
            using ServiceProvider provider = build(bufferBytes, construction);

            var prior = new List<DependencyComponent>();
            using var cancellation = new CancellationTokenSource();

            for (int scopeIndex = 0; scopeIndex < 2; scopeIndex++)
            {
                int scopeStart = construction.Constructed.Count;

                using (IServiceScope scope = provider.CreateScope())
                {
                    TDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<TDispatcher>();

                    Require(
                        ReferenceEquals(dispatcher, scope.ServiceProvider.GetRequiredService<TDispatcher>())
                            == ScopeLifetimeVerification.SharesWithinScope(dispatcherLifetime),
                        "dispatcher lifetime");

                    for (int sendIndex = 0; sendIndex < 3; sendIndex++)
                    {
                        ShortCircuitOutcome selected = sendIndex == 2 ? ShortCircuitOutcome.FullExecution : outcome;
                        var trace = new ExecutionTrace();
                        TMessage message = createMessage(selected);
                        message.Trace = trace;

                        int start = construction.Constructed.Count;
                        ValueTask<int> pending = send(dispatcher, message, cancellation.Token);

                        Require(pending.IsCompletedSuccessfully, "workload did not complete synchronously");

                        int result = pending.GetAwaiter().GetResult();
                        BenchmarkExpectation expectation = ShortCircuitWorkload.Expectation(selected);

                        Require(result == expectation.Result, "response");

                        Require(trace.Entries.Select(entry => entry.Name).SequenceEqual(expectation.Names), "execution order");
                        Require(trace.Entries.All(entry => entry.Token == cancellation.Token), "cancellation token");

                        var activated = construction.Constructed.Skip(childScopePerSend ? start : scopeStart).ToArray();
                        string[] expected = ExpectedRoles(selected, resolution);

                        Require(
                            activated.Select(component => component.Role).Order().SequenceEqual(expected.Order()),
                            "activation: " + string.Join(", ", activated.Select(component => component.Role)));

                        Require(
                            activated.Select(component => component.State).Distinct().Count() == 1,
                            "dependency scope identity");

                        Require(
                            trace.Entries.All(entry => activated.Contains(entry.Component)),
                            "executed untracked component");

                        Require(!activated.Any(prior.Contains), "component crossed a scope boundary");

                        Require(
                            !activated.Any(component =>
                                prior.Any(previous => ReferenceEquals(previous.State, component.State))),
                            "dependency state crossed a scope boundary");

                        if (!childScopePerSend && sendIndex == 1)
                            Require(construction.Constructed.Count == start, "scoped component was reconstructed");

                        Require(
                            activated.All(component => component.IsDisposed == childScopePerSend),
                            "premature or missing disposal");

                        if (childScopePerSend)
                            prior.AddRange(activated);

                        foreach (DependencyComponent component in activated)
                        {
                            if (component is ChangeBuffer change)
                                Require(change.Bytes.Length == bufferBytes, "transaction buffer size");

                            if (component is CommandBuffer command)
                                Require(command.Bytes.Length == bufferBytes, "handler buffer size");
                        }
                    }
                }

                DependencyComponent[] completed = construction.Constructed.Skip(scopeStart).ToArray();

                foreach (DependencyComponent component in completed)
                {
                    Require(component.IsDisposed && component.Scope.IsDisposed, "scope disposal");

                    int expectedCount = component.Role == "authorization" ? authorizationDisposals : 1;

                    Require(
                        construction.Disposed.Count(item => ReferenceEquals(item, component)) == expectedCount,
                        "disposal count: " + component.Role);
                }

                if (!childScopePerSend)
                    prior.AddRange(completed);
            }

            void Require(bool condition, string detail)
            {
                if (!condition)
                    throw new InvalidOperationException($"{library} short circuit ({outcome}, buffer={bufferBytes}): {detail}.");
            }
        }
    }

    private static string[] ExpectedRoles(ShortCircuitOutcome outcome, DependencyResolution resolution)
    {
        string[] authorization = ["access-policy", "authorization"];
        string[] cache = [.. authorization, "cache-policy", "cache"];
        string[] stages = [.. cache, "change-buffer", "unit-of-work", "transaction"];
        string[] full = [.. stages, "command-buffer", "data-session", "repository", TraceNames.Handler];

        if (outcome == ShortCircuitOutcome.FullExecution || resolution == DependencyResolution.EntireChain)
            return full;

        if (resolution == DependencyResolution.AllStages)
            return stages;

        return outcome == ShortCircuitOutcome.AuthorizationReject ? authorization : cache;
    }
}
