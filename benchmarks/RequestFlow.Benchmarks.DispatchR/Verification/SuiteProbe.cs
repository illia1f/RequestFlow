using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.Harness;
using RequestFlow.Benchmarks.RequestFlowFixtures;
using RequestFlow.Benchmarks.RequestFlowFixtures.Catalog;
using RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;
using static RequestFlow.Benchmarks.Core.BenchmarkVerification;
using static RequestFlow.Benchmarks.Core.RegistrationReport;
using static RequestFlow.Benchmarks.Core.ScopeLifetimeVerification;
using static RequestFlow.Benchmarks.Core.TraceAssertions;
using IMediator = global::DispatchR.IMediator;

namespace RequestFlow.Benchmarks.DispatchR;

public static class SuiteProbe
{
    private static readonly Type[] DispatchRServices =
    [
        typeof(global::DispatchR.Abstractions.Send.IRequestHandler<,>),
        typeof(global::DispatchR.Abstractions.Stream.IStreamRequestHandler<,>),
        typeof(global::DispatchR.Abstractions.Notification.INotificationHandler<>),
    ];

    private static readonly ComparedLibrary DispatchRLibrary = new("DispatchR", DispatchRSetup.Build, DispatchRServices);

    private static readonly MethodInfo VerifyCatalogRequestDefinition =
        typeof(SuiteProbe).GetMethod(nameof(VerifyCatalogRequest), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static BenchmarkProbeResult Run()
    {
        VerifyLifetimes(report: false);
        DispatchRShortCircuitSetup.Verify();

        return VerifyBenchmarkMethods();
    }

    public static void ReportLifetimes()
    {
        VerifyLifetimes(report: true);

        ReportRegistrations(
            "RequestFlow short circuit", ShortCircuitServices.Services(bufferBytes: 0),
            typeof(global::RequestFlow.IRequestDispatcher), RequestFlowSetup.ReportedServices, RequestFlowSetup.StageContracts);

        ReportRegistrations(
            "DispatchR short circuit", WithStableKeys(DispatchRShortCircuitSetup.Services(bufferBytes: 0)),
            typeof(IMediator), DispatchRServices);
    }

    private static void VerifyLifetimes(bool report)
    {
        VerifyRequestLifetimes(report);
        VerifyStreamLifetimes(report);
        VerifyEventLifetimes(report);
        VerifyCatalogs(report);
    }

    private static void VerifyRequestLifetimes(bool report)
    {
        foreach (int stages in Params<int>(typeof(DispatchRTaskRetainedScopeBenchmarks), nameof(DispatchRTaskRetainedScopeBenchmarks.StageCount)))
        foreach (CompletionKind completion in Params<CompletionKind>(
            typeof(DispatchRTaskRetainedScopeBenchmarks), nameof(DispatchRTaskRetainedScopeBenchmarks.Completion)))
        {
            VerifyDispatchRPair<global::RequestFlow.IRequestDispatcher>(
                $"Task, completion={completion}, stages={stages}", RequestNames(stages), report,
                RequestFlowSetup.TaskServices(stages),
                (dispatcher, trace, token) => ExpectResult(dispatcher.SendAsync<int>(new TaskRequest(completion) { Trace = trace }, token)),
                DispatchRSetup.TaskServices(stages),
                (mediator, trace, token) => ExpectResult(mediator.Send(new DispatchRTaskRequest(completion) { Trace = trace }, token)));
        }

        foreach (int stages in Params<int>(typeof(DispatchRVoidRequestRetainedScopeBenchmarks), nameof(DispatchRVoidRequestRetainedScopeBenchmarks.StageCount)))
        foreach (CompletionKind completion in Params<CompletionKind>(
            typeof(DispatchRVoidRequestRetainedScopeBenchmarks), nameof(DispatchRVoidRequestRetainedScopeBenchmarks.Completion)))
        {
            VerifyDispatchRPair<global::RequestFlow.IRequestDispatcher>(
                $"Void request, completion={completion}, stages={stages}", RequestNames(stages), report,
                RequestFlowSetup.TaskVoidRequestServices(stages),
                (dispatcher, trace, token) => dispatcher.SendAsync(new TaskVoidRequest(completion) { Trace = trace }, token),
                DispatchRSetup.VoidRequestServices(stages),
                (mediator, trace, token) => mediator.Send(new DispatchRVoidRequest(completion) { Trace = trace }, token));
        }

        foreach (int stages in Params<int>(typeof(DispatchRValueRetainedScopeBenchmarks), nameof(DispatchRValueRetainedScopeBenchmarks.StageCount)))
        foreach (CompletionKind completion in Params<CompletionKind>(
            typeof(DispatchRValueRetainedScopeBenchmarks), nameof(DispatchRValueRetainedScopeBenchmarks.Completion)))
        {
            VerifyDispatchRPair<global::RequestFlow.IValueRequestDispatcher>(
                $"Value, completion={completion}, stages={stages}", RequestNames(stages), report,
                RequestFlowSetup.ValueServices(stages),
                (dispatcher, trace, token) => ExpectResult(dispatcher.SendAsync<int>(new ValueRequest(completion) { Trace = trace }, token)),
                DispatchRSetup.ValueServices(stages),
                (mediator, trace, token) => ExpectResult(mediator.Send(new DispatchRValueRequest(completion) { Trace = trace }, token)));
        }

        foreach (int stages in Params<int>(typeof(DispatchRValueVoidRequestRetainedScopeBenchmarks), nameof(DispatchRValueVoidRequestRetainedScopeBenchmarks.StageCount)))
        foreach (CompletionKind completion in Params<CompletionKind>(
            typeof(DispatchRValueVoidRequestRetainedScopeBenchmarks), nameof(DispatchRValueVoidRequestRetainedScopeBenchmarks.Completion)))
        {
            VerifyDispatchRPair<global::RequestFlow.IValueRequestDispatcher>(
                $"Value void request, completion={completion}, stages={stages}", RequestNames(stages), report,
                RequestFlowSetup.ValueVoidRequestServices(stages),
                (dispatcher, trace, token) => dispatcher.SendAsync(new ValueVoidRequest(completion) { Trace = trace }, token).AsTask(),
                DispatchRSetup.ValueVoidRequestServices(stages),
                (mediator, trace, token) => mediator.Send(new DispatchRValueVoidRequest(completion) { Trace = trace }, token).AsTask());
        }
    }

    private static void VerifyStreamLifetimes(bool report)
    {
        foreach (int stages in Params<int>(typeof(DispatchRStreamRetainedScopeBenchmarks), nameof(DispatchRStreamRetainedScopeBenchmarks.StageCount)))
        foreach (int count in Params<int>(typeof(DispatchRStreamRetainedScopeBenchmarks), nameof(DispatchRStreamRetainedScopeBenchmarks.ItemCount)))
        foreach (CompletionKind completion in Params<CompletionKind>(typeof(DispatchRStreamRetainedScopeBenchmarks), nameof(DispatchRStreamRetainedScopeBenchmarks.Completion)))
        foreach (bool early in new[] { false, true })
        {
            VerifyDispatchRPair<global::RequestFlow.IStreamDispatcher>(
                $"Stream, items={count}, completion={completion}, stages={stages}, early={early}",
                StreamNames(stages, count, early: early), report,
                RequestFlowSetup.StreamServices(stages),
                (dispatcher, trace, token) => ReadStream(
                    dispatcher.Stream(new StreamRequest(count, completion) { Trace = trace }, token), count, early, token),
                DispatchRSetup.StreamServices(stages),
                (mediator, trace, token) => ReadStream(
                    mediator.CreateStream(new DispatchRStreamRequest(count, completion) { Trace = trace }, token), count, early, token));
        }
    }

    private static void VerifyEventLifetimes(bool report)
    {
        foreach (CompletionKind completion in Params<CompletionKind>(
            typeof(DispatchREventSequentialRetainedScopeBenchmarks), nameof(DispatchREventSequentialRetainedScopeBenchmarks.Completion)))
        foreach (int handlers in Params<int>(
            typeof(DispatchREventSequentialRetainedScopeBenchmarks), nameof(DispatchREventSequentialRetainedScopeBenchmarks.HandlerCount)))
        {
            VerifyDispatchRPair<global::RequestFlow.IEventPublisher>(
                $"Event, completion={completion}, handlers={handlers}, parallel=False", EventNames(handlers), report,
                RequestFlowSetup.EventServices(handlers, parallel: false),
                (publisher, trace, token) => publisher.PublishAsync(new EventMessage(completion) { Trace = trace }, token),
                DispatchRSetup.EventServices(handlers),
                (mediator, trace, token) => mediator.Publish(new DispatchREvent(completion) { Trace = trace }, token).AsTask());
        }
    }

    private static void VerifyCatalogs(bool report)
    {
        foreach (int count in Params<int>(typeof(DispatchRCatalogSizeBenchmarks), nameof(DispatchRCatalogSizeBenchmarks.CatalogSize)))
        {
            ServiceCollection requestFlowServices = RequestFlowSetup.CatalogServices(count);
            ServiceCollection dispatchRServices = DispatchRSetup.CatalogServices(count);

            Require(
                GetComponentDescriptors(requestFlowServices).Count() == count,
                $"RequestFlow catalog={count} registered the wrong number of handlers.");

            Require(
                GetComponentDescriptors(dispatchRServices).Count() == count,
                $"DispatchR catalog={count} registered the wrong number of handlers.");

            foreach (Type tag in RuntimeCatalog.Tags(count))
            {
                VerifyCatalogRequestDefinition.MakeGenericMethod(tag).Invoke(
                    null, BindingFlags.DoNotWrapExceptions, binder: null,
                    [count, report, requestFlowServices, dispatchRServices], culture: null);
            }
        }
    }

    // DispatchR resolves the handler from the compile-time request type, so each tag is bound here.
    private static void VerifyCatalogRequest<TTag>(
        int count, bool report, ServiceCollection requestFlowServices, ServiceCollection dispatchRServices)
        => VerifyDispatchRPair<global::RequestFlow.IRequestDispatcher>(
            $"catalog request {typeof(TTag).Name}, catalog={count}", [TraceNames.Handler], report,
            requestFlowServices,
            (dispatcher, trace, token) => ExpectResult(dispatcher.SendAsync(new RuntimeTaskRequest<TTag> { Trace = trace }, token)),
            dispatchRServices,
            (mediator, trace, token) => ExpectResult(mediator.Send(new DispatchRCatalogRequest<TTag> { Trace = trace }, token)));

    // Runs the shared pair check; the report prints DispatchR's random chain keys as stable labels.
    private static void VerifyDispatchRPair<TRequestFlow>(
        string label,
        string[] expectedNames,
        bool report,
        ServiceCollection requestFlowServices,
        Func<TRequestFlow, ExecutionTrace, CancellationToken, Task> requestFlow,
        ServiceCollection dispatchRServices,
        Func<IMediator, ExecutionTrace, CancellationToken, Task> dispatchR)
        where TRequestFlow : notnull
    {
        if (report)
        {
            ReportRegistrations(
                "RequestFlow " + label, requestFlowServices, typeof(TRequestFlow),
                RequestFlowSetup.ReportedServices, RequestFlowSetup.StageContracts);
            ReportRegistrations("DispatchR " + label, WithStableKeys(dispatchRServices), typeof(IMediator), DispatchRServices);
        }

        VerifyPair(
            label, expectedNames, report: false,
            RequestFlowSetup.Library, requestFlowServices, requestFlow,
            DispatchRLibrary, dispatchRServices, dispatchR);

        if (report)
            Console.WriteLine("  Verified dispatcher, component and dependency identities, tokens, traversal and disposal.");
    }

    // DispatchR keys each handler chain with a new Guid, so the report numbers chains in registration order instead.
    private static IServiceCollection WithStableKeys(IServiceCollection services)
    {
        var keys = new Dictionary<object, string>();
        IServiceCollection report = new ServiceCollection();

        foreach (ServiceDescriptor descriptor in services)
        {
            if (descriptor.IsKeyedService && descriptor.ServiceKey is Guid key && descriptor.KeyedImplementationType is Type implementation)
            {
                if (!keys.TryGetValue(key, out string? label))
                {
                    label = "chain " + (keys.Count + 1);
                    keys.Add(key, label);
                }

                report.Add(new ServiceDescriptor(descriptor.ServiceType, label, implementation, descriptor.Lifetime));
            }
            else
            {
                report.Add(descriptor);
            }
        }

        return report;
    }

    private static BenchmarkProbeResult VerifyBenchmarkMethods()
    {
        var expectations = new Dictionary<Type, Func<IReadOnlyDictionary<string, object?>, BenchmarkExpectation>>
        {
            [typeof(DispatchRShortCircuitRetainedScopeBenchmarks)] = ShortCircuitWorkload.Expectation,
            [typeof(DispatchRShortCircuitScopePerOperationBenchmarks)] = ShortCircuitWorkload.Expectation,
            [typeof(DispatchRTaskRetainedScopeBenchmarks)] = RequestWithStages,
            [typeof(DispatchRTaskScopePerOperationBenchmarks)] = RequestWithStages,
            [typeof(DispatchRValueRetainedScopeBenchmarks)] = ValueRequestWithStages,
            [typeof(DispatchRValueScopePerOperationBenchmarks)] = ValueRequestWithStages,
            [typeof(DispatchRVoidRequestRetainedScopeBenchmarks)] = VoidRequestWithStages,
            [typeof(DispatchRVoidRequestScopePerOperationBenchmarks)] = VoidRequestWithStages,
            [typeof(DispatchRValueVoidRequestRetainedScopeBenchmarks)] = ValueVoidRequestWithStages,
            [typeof(DispatchRValueVoidRequestScopePerOperationBenchmarks)] = ValueVoidRequestWithStages,
            [typeof(DispatchRStreamRetainedScopeBenchmarks)] = StreamWithStages,
            [typeof(DispatchRStreamScopePerOperationBenchmarks)] = StreamWithStages,
            [typeof(DispatchREventSequentialRetainedScopeBenchmarks)] = SequentialEvent,
            [typeof(DispatchREventSequentialScopePerOperationBenchmarks)] = SequentialEvent,
            [typeof(DispatchRTaskDirectHandlerBenchmarks)] = _ => BenchmarkExpectation.Request(),
            [typeof(DispatchRValueDirectHandlerBenchmarks)] = _ => BenchmarkExpectation.Request(),
            [typeof(DispatchRCatalogSizeBenchmarks)] = _ => BenchmarkExpectation.Request(),
            [typeof(DispatchRMixedTrafficBenchmarks)] = MixedTrafficExpectation,
        };

        return BenchmarkVerification.Run(typeof(Program).Assembly, expectations);

        static BenchmarkExpectation RequestWithStages(IReadOnlyDictionary<string, object?> parameters)
            => BenchmarkExpectation.Request(stages: (int)parameters[nameof(DispatchRTaskRetainedScopeBenchmarks.StageCount)]!);

        static BenchmarkExpectation ValueRequestWithStages(IReadOnlyDictionary<string, object?> parameters)
            => BenchmarkExpectation.Request(stages: (int)parameters[nameof(DispatchRValueRetainedScopeBenchmarks.StageCount)]!);

        static BenchmarkExpectation VoidRequestWithStages(IReadOnlyDictionary<string, object?> parameters)
            => BenchmarkExpectation.VoidRequest(stages: (int)parameters[nameof(DispatchRVoidRequestRetainedScopeBenchmarks.StageCount)]!);

        static BenchmarkExpectation ValueVoidRequestWithStages(IReadOnlyDictionary<string, object?> parameters)
            => BenchmarkExpectation.VoidRequest(stages: (int)parameters[nameof(DispatchRValueVoidRequestRetainedScopeBenchmarks.StageCount)]!);

        static BenchmarkExpectation StreamWithStages(IReadOnlyDictionary<string, object?> parameters)
            => BenchmarkExpectation.Stream(
                stages: (int)parameters[nameof(DispatchRStreamRetainedScopeBenchmarks.StageCount)]!,
                count: (int)parameters[nameof(DispatchRStreamRetainedScopeBenchmarks.ItemCount)]!);

        static BenchmarkExpectation SequentialEvent(IReadOnlyDictionary<string, object?> parameters)
            => BenchmarkExpectation.Event(handlers: (int)parameters[nameof(DispatchREventSequentialRetainedScopeBenchmarks.HandlerCount)]!);
    }

    private static BenchmarkExpectation MixedTrafficExpectation(IReadOnlyDictionary<string, object?> parameters)
    {
        IReadOnlyList<Type> order = RuntimeCatalog.MixedTrafficOrder((int)parameters[nameof(DispatchRMixedTrafficBenchmarks.CatalogSize)]!);

        return new(
            Names: [.. Enumerable.Repeat(TraceNames.Handler, RuntimeCatalog.MixedTrafficBatch)],
            Result: RuntimeCatalog.MixedTrafficBatch * BenchmarkWorkload.Expected,
            HandlerTypes: new Dictionary<string, Type[]>
            {
                [nameof(DispatchRMixedTrafficBenchmarks.RequestFlow)] =
                    [.. order.Select(tag => typeof(RuntimeTaskHandler<>).MakeGenericType(tag))],
                [nameof(DispatchRMixedTrafficBenchmarks.DispatchR)] =
                    [.. order.Select(tag => typeof(DispatchRCatalogHandler<>).MakeGenericType(tag))],
            });
    }
}
