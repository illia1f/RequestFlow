using Microsoft.Extensions.DependencyInjection;
using static RequestFlow.Benchmarks.Core.RegistrationReport;
using static RequestFlow.Benchmarks.Core.TraceAssertions;

namespace RequestFlow.Benchmarks.Core;

/// <summary>
/// Checks scoped component identities, execution traces and disposal.
/// </summary>
public static class ScopeLifetimeVerification
{
    /// <summary>
    /// Checks registrations and execution across two scopes.
    /// </summary>
    public static void VerifyScopes(
        string label,
        ServiceCollection registrations,
        Func<IServiceCollection, ServiceProvider> build,
        Type dispatcherType,
        Func<IServiceProvider, ExecutionTrace, CancellationToken, Task> execute,
        string[] expectedNames,
        bool report,
        bool ordered = true,
        IReadOnlyCollection<Type>? reportedServices = null,
        IReadOnlyCollection<Type>? reportedContracts = null,
        ServiceLifetime dispatcherLifetime = ServiceLifetime.Scoped)
    {
        ServiceDescriptor[] components = GetComponentDescriptors(registrations).ToArray();

        VerifyComponentRegistrations(label, components);

        Require(
            registrations.LastOrDefault(descriptor => descriptor.ServiceType == dispatcherType)?.Lifetime == dispatcherLifetime,
            label + " does not register its dispatcher as " + dispatcherLifetime + ".");

        if (report)
            ReportRegistrations(label, registrations, dispatcherType, reportedServices, reportedContracts);

        using var cancellation = new CancellationTokenSource();
        using ServiceProvider provider = build(registrations);
        using IServiceScope first = provider.CreateScope();
        using IServiceScope next = provider.CreateScope();
        object dispatcher = first.ServiceProvider.GetRequiredService(dispatcherType);

        Require(
            ReferenceEquals(dispatcher, first.ServiceProvider.GetRequiredService(dispatcherType)) == SharesWithinScope(dispatcherLifetime),
            label + " does not resolve a " + dispatcherLifetime + " dispatcher within one scope.");

        Require(
            !ReferenceEquals(dispatcher, next.ServiceProvider.GetRequiredService(dispatcherType)),
            label + " shares dispatcher across scopes.");

        var firstTrace = new ExecutionTrace();
        var repeatedTrace = new ExecutionTrace();
        var nextTrace = new ExecutionTrace();

        Execute(first.ServiceProvider, firstTrace);
        Execute(first.ServiceProvider, repeatedTrace);
        Execute(next.ServiceProvider, nextTrace);

        VerifyRecords(label, firstTrace.Entries, expectedNames, cancellation.Token, ordered);
        VerifyRecords(label + " (repeated call)", repeatedTrace.Entries, expectedNames, cancellation.Token, ordered);
        VerifyRecords(label + " (next scope)", nextTrace.Entries, expectedNames, cancellation.Token, ordered);

        ScopeWitness firstWitness = first.ServiceProvider.GetRequiredService<ScopeWitness>();
        ScopeWitness nextWitness = next.ServiceProvider.GetRequiredService<ScopeWitness>();

        VerifyComponentIdentities(label, firstTrace, repeatedTrace, nextTrace, firstWitness, nextWitness);

        first.Dispose();

        Require(
            firstTrace.Entries.All(entry => entry.Component.IsDisposed && entry.Scope.IsDisposed),
            label + " did not dispose the first scope.");

        Require(
            nextTrace.Entries.All(entry => !entry.Component.IsDisposed && !entry.Scope.IsDisposed),
            label + " disposed another scope's component.");

        next.Dispose();

        Require(
            nextTrace.Entries.All(entry => entry.Component.IsDisposed && entry.Scope.IsDisposed),
            label + " did not dispose the next scope.");

        if (report)
            Console.WriteLine("  Verified dispatcher, component and dependency identities, tokens, traversal and disposal.");

        void Execute(IServiceProvider services, ExecutionTrace trace)
        {
            try
            {
                execute(services, trace, cancellation.Token).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(label + " failed.", exception);
            }
        }
    }

    /// <summary>
    /// Runs <see cref="VerifyScopes"/> on two libraries' registrations of the same workload.
    /// </summary>
    /// <param name="label">The workload description that follows each library name.</param>
    /// <param name="dispatcherLifetime">Required of both libraries, so a lifetime mismatch fails the pair.</param>
    public static void VerifyPair<TFirst, TSecond>(
        string label,
        string[] expectedNames,
        bool report,
        ComparedLibrary first,
        ServiceCollection firstServices,
        Func<TFirst, ExecutionTrace, CancellationToken, Task> firstExecute,
        ComparedLibrary second,
        ServiceCollection secondServices,
        Func<TSecond, ExecutionTrace, CancellationToken, Task> secondExecute,
        bool ordered = true,
        ServiceLifetime dispatcherLifetime = ServiceLifetime.Scoped)
        where TFirst : notnull
        where TSecond : notnull
    {
        Verify(first, firstServices, firstExecute);
        Verify(second, secondServices, secondExecute);

        void Verify<TDispatcher>(
            ComparedLibrary library,
            ServiceCollection services,
            Func<TDispatcher, ExecutionTrace, CancellationToken, Task> execute)
            where TDispatcher : notnull
            => VerifyScopes(
                label: library.Name + " " + label,
                registrations: services,
                build: library.Build,
                dispatcherType: typeof(TDispatcher),
                execute: (provider, trace, token) => execute(provider.GetRequiredService<TDispatcher>(), trace, token),
                expectedNames: expectedNames,
                report: report,
                ordered: ordered,
                reportedServices: library.ReportedServices,
                reportedContracts: library.ReportedContracts,
                dispatcherLifetime: dispatcherLifetime);
    }

    internal static bool SharesWithinScope(ServiceLifetime dispatcherLifetime) => dispatcherLifetime switch
    {
        ServiceLifetime.Scoped => true,
        ServiceLifetime.Transient => false,
        _ => throw new ArgumentOutOfRangeException(nameof(dispatcherLifetime), dispatcherLifetime, "Dispatchers are scoped or transient."),
    };

    /// <summary>
    /// Requires a request to return the shared workload result.
    /// </summary>
    public static async Task ExpectResult(Task<int> operation)
    {
        int result = await operation.ConfigureAwait(false);

        Require(result == BenchmarkWorkload.Expected, $"Request returned {result}, expected {BenchmarkWorkload.Expected}.");
    }

    /// <inheritdoc cref="ExpectResult(Task{int})"/>
    public static Task ExpectResult(ValueTask<int> operation) => ExpectResult(operation.AsTask());

    /// <summary>
    /// Consumes a stream and checks its checksum, or reads only its first item when <paramref name="early"/> is set.
    /// </summary>
    public static async Task ReadStream(IAsyncEnumerable<int> sequence, int count, bool early, CancellationToken token)
    {
        if (early)
        {
            await using IAsyncEnumerator<int> enumerator = sequence.GetAsyncEnumerator(token);

            Require(await enumerator.MoveNextAsync().ConfigureAwait(false), "Stream ended before its first item.");
            Require(enumerator.Current == 0, "Stream returned the wrong first item.");

            return;
        }

        int sum = await BenchmarkWorkload.Consume(sequence).ConfigureAwait(false);

        Require(sum == count * (count - 1) / 2, "Stream returned the wrong checksum.");
    }

    private static void VerifyComponentRegistrations(string label, ServiceDescriptor[] components)
    {
        Require(
            components.All(descriptor => descriptor.Lifetime == ServiceLifetime.Scoped),
            label + " has a non-scoped component.");

        bool hasUniqueRegistrations = components.GroupBy(descriptor => (descriptor.ServiceType,
            descriptor.IsKeyedService ? descriptor.ServiceKey : null,
            GetImplementationType(descriptor))).All(group => group.Count() == 1);

        Require(hasUniqueRegistrations, label + " has duplicate component registrations.");
    }

    private static void VerifyComponentIdentities(
        string label,
        ExecutionTrace firstTrace,
        ExecutionTrace repeatedTrace,
        ExecutionTrace nextTrace,
        ScopeWitness firstWitness,
        ScopeWitness nextWitness)
    {
        foreach (ExecutionRecord entry in firstTrace.Entries)
        {
            ExecutionRecord repeated = repeatedTrace.Entries.Single(candidate => candidate.Name == entry.Name);
            ExecutionRecord fresh = nextTrace.Entries.Single(candidate => candidate.Name == entry.Name);

            Require(
                ReferenceEquals(entry.Component, repeated.Component),
                label + " changes " + entry.Name + " within a scope.");

            Require(
                !ReferenceEquals(entry.Component, fresh.Component),
                label + " shares " + entry.Name + " across scopes.");

            Require(
                ReferenceEquals(entry.Scope, firstWitness) && ReferenceEquals(repeated.Scope, firstWitness),
                label + " changes its scoped dependency.");

            Require(
                ReferenceEquals(fresh.Scope, nextWitness) && !ReferenceEquals(firstWitness, nextWitness),
                label + " shares its scoped dependency across scopes.");

            Require(!entry.Component.IsDisposed && !fresh.Component.IsDisposed, label + " disposed a live component.");
        }

        Require(!firstWitness.IsDisposed && !nextWitness.IsDisposed, label + " disposed a live dependency.");
    }
}
