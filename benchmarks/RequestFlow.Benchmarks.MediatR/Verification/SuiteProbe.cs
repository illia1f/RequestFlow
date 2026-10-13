using MediatR;
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

namespace RequestFlow.Benchmarks.MediatR;

public static class SuiteProbe
{
    private static readonly Type[] MediatRServices =
    [
        typeof(IMediator),
        typeof(INotificationPublisher),
        typeof(IPipelineBehavior<,>),
        typeof(IStreamPipelineBehavior<,>),
    ];

    private static readonly ComparedLibrary MediatRLibrary = new("MediatR", MediatRSetup.Build, MediatRServices);

    public static BenchmarkProbeResult Run()
    {
        VerifyLifetimes(report: false);
        MediatRShortCircuitSetup.Verify();

        return VerifyBenchmarkMethods();
    }

    public static void ReportLifetimes()
    {
        VerifyLifetimes(report: true);

        ReportRegistrations(
            "RequestFlow short circuit",
            ShortCircuitServices.Services(bufferBytes: 0, dispatcherLifetime: MediatRSetup.DispatcherLifetime),
            typeof(IRequestDispatcher), RequestFlowSetup.ReportedServices, RequestFlowSetup.StageContracts);

        ReportRegistrations(
            "MediatR short circuit", MediatRShortCircuitSetup.Services(bufferBytes: 0),
            typeof(ISender), MediatRServices);
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
        foreach (CompletionKind completion in Params<CompletionKind>(typeof(MediatRTaskRetainedScopeBenchmarks), nameof(MediatRTaskRetainedScopeBenchmarks.Completion)))
        foreach (int stages in Params<int>(typeof(MediatRTaskRetainedScopeBenchmarks), nameof(MediatRTaskRetainedScopeBenchmarks.StageCount)))
        {
            VerifyPair<IRequestDispatcher, ISender>(
                $"Task, completion={completion}, stages={stages}", RequestNames(stages), report,
                RequestFlowSetup.Library, RequestFlowSetup.TaskServices(stages, MediatRSetup.DispatcherLifetime),
                (dispatcher, trace, token) => ExpectResult(dispatcher.SendAsync<int>(new TaskRequest(completion) { Trace = trace }, token)),
                MediatRLibrary, MediatRSetup.TaskServices(stages),
                (sender, trace, token) => ExpectResult(sender.Send(new MediatRTaskRequest(completion) { Trace = trace }, token)),
                dispatcherLifetime: MediatRSetup.DispatcherLifetime);
        }

        foreach (CompletionKind completion in Params<CompletionKind>(typeof(MediatRVoidRequestRetainedScopeBenchmarks), nameof(MediatRVoidRequestRetainedScopeBenchmarks.Completion)))
        foreach (int stages in Params<int>(typeof(MediatRVoidRequestRetainedScopeBenchmarks), nameof(MediatRVoidRequestRetainedScopeBenchmarks.StageCount)))
        {
            VerifyPair<IRequestDispatcher, ISender>(
                $"void request, completion={completion}, stages={stages}", RequestNames(stages), report,
                RequestFlowSetup.Library, RequestFlowSetup.TaskVoidRequestServices(stages, MediatRSetup.DispatcherLifetime),
                (dispatcher, trace, token) => dispatcher.SendAsync(new TaskVoidRequest(completion) { Trace = trace }, token),
                MediatRLibrary, MediatRSetup.VoidRequestServices(stages),
                (sender, trace, token) => sender.Send(new MediatRVoidRequest(completion) { Trace = trace }, token),
                dispatcherLifetime: MediatRSetup.DispatcherLifetime);
        }
    }

    private static void VerifyStreamLifetimes(bool report)
    {
        foreach (int stages in Params<int>(typeof(MediatRStreamRetainedScopeBenchmarks), nameof(MediatRStreamRetainedScopeBenchmarks.StageCount)))
        foreach (int count in Params<int>(typeof(MediatRStreamRetainedScopeBenchmarks), nameof(MediatRStreamRetainedScopeBenchmarks.ItemCount)))
        foreach (CompletionKind completion in Params<CompletionKind>(typeof(MediatRStreamRetainedScopeBenchmarks), nameof(MediatRStreamRetainedScopeBenchmarks.Completion)))
        foreach (bool early in new[] { false, true })
        {
            VerifyPair<IStreamDispatcher, ISender>(
                $"Stream, items={count}, completion={completion}, stages={stages}, early={early}",
                StreamNames(stages, count, early: early), report,
                RequestFlowSetup.Library, RequestFlowSetup.StreamServices(stages, MediatRSetup.DispatcherLifetime),
                (dispatcher, trace, token) => ReadStream(
                    dispatcher.Stream(new StreamRequest(count, completion) { Trace = trace }, token), count, early, token),
                MediatRLibrary, MediatRSetup.StreamServices(stages),
                (sender, trace, token) => ReadStream(
                    sender.CreateStream(new MediatRStreamRequest(count, completion) { Trace = trace }, token), count, early, token),
                dispatcherLifetime: MediatRSetup.DispatcherLifetime);
        }
    }

    private static void VerifyEventLifetimes(bool report)
    {
        foreach ((Type benchmark, bool parallel) in new[]
            { (typeof(MediatREventSequentialRetainedScopeBenchmarks), false), (typeof(MediatREventParallelRetainedScopeBenchmarks), true) })
        foreach (CompletionKind completion in Params<CompletionKind>(benchmark, nameof(MediatREventSequentialRetainedScopeBenchmarks.Completion)))
        foreach (int handlers in Params<int>(benchmark, nameof(MediatREventSequentialRetainedScopeBenchmarks.HandlerCount)))
        {
            VerifyPair<IEventPublisher, IPublisher>(
                $"Event, completion={completion}, handlers={handlers}, parallel={parallel}", EventNames(handlers), report,
                RequestFlowSetup.Library, RequestFlowSetup.EventServices(handlers, parallel, MediatRSetup.DispatcherLifetime),
                (publisher, trace, token) => publisher.PublishAsync(new EventMessage(completion) { Trace = trace }, token),
                MediatRLibrary, MediatRSetup.EventServices(handlers, parallel: parallel),
                (publisher, trace, token) => publisher.Publish(new MediatREvent(completion) { Trace = trace }, token),
                ordered: !parallel,
                dispatcherLifetime: MediatRSetup.DispatcherLifetime);
        }
    }

    private static void VerifyCatalogs(bool report)
    {
        foreach (int count in Params<int>(typeof(MediatRCatalogSizeBenchmarks), nameof(MediatRCatalogSizeBenchmarks.CatalogSize)))
        {
            ServiceCollection requestFlowServices = RequestFlowSetup.CatalogServices(count, MediatRSetup.DispatcherLifetime);
            ServiceCollection mediatRServices = MediatRSetup.CatalogServices(count);

            Require(
                GetComponentDescriptors(requestFlowServices).Count() == count,
                $"RequestFlow catalog={count} registered the wrong number of handlers.");

            Require(
                GetComponentDescriptors(mediatRServices).Count() == count,
                $"MediatR catalog={count} registered the wrong number of handlers.");

            foreach (Type tag in RuntimeCatalog.Tags(count))
            {
                Type requestFlowRequest = typeof(RuntimeTaskRequest<>).MakeGenericType(tag);
                Type mediatRRequest = typeof(MediatRCatalogRequest<>).MakeGenericType(tag);

                VerifyPair<IRequestDispatcher, ISender>(
                    $"catalog request {tag.Name}, catalog={count}", [TraceNames.Handler], report,
                    RequestFlowSetup.Library, requestFlowServices,
                    (dispatcher, trace, token) => ExpectResult(
                        dispatcher.SendAsync(CatalogRequest<IRequest<int>>(requestFlowRequest, trace), token)),
                    MediatRLibrary, mediatRServices,
                    (sender, trace, token) => ExpectResult(
                        sender.Send(CatalogRequest<global::MediatR.IRequest<int>>(mediatRRequest, trace), token)),
                    dispatcherLifetime: MediatRSetup.DispatcherLifetime);
            }
        }
    }

    private static BenchmarkProbeResult VerifyBenchmarkMethods()
    {
        var expectations = new Dictionary<Type, Func<IReadOnlyDictionary<string, object?>, BenchmarkExpectation>>
        {
            [typeof(MediatRShortCircuitRetainedScopeBenchmarks)] = ShortCircuitWorkload.Expectation,
            [typeof(MediatRShortCircuitScopePerOperationBenchmarks)] = ShortCircuitWorkload.Expectation,
            [typeof(MediatRTaskRetainedScopeBenchmarks)] = RequestWithStages,
            [typeof(MediatRTaskScopePerOperationBenchmarks)] = RequestWithStages,
            [typeof(MediatRVoidRequestRetainedScopeBenchmarks)] = VoidRequestWithStages,
            [typeof(MediatRVoidRequestScopePerOperationBenchmarks)] = VoidRequestWithStages,
            [typeof(MediatRStreamRetainedScopeBenchmarks)] = StreamWithStages,
            [typeof(MediatRStreamScopePerOperationBenchmarks)] = StreamWithStages,
            [typeof(MediatREventSequentialRetainedScopeBenchmarks)] = SequentialEvent,
            [typeof(MediatREventSequentialScopePerOperationBenchmarks)] = SequentialEvent,
            [typeof(MediatREventParallelRetainedScopeBenchmarks)] = ParallelEvent,
            [typeof(MediatREventParallelScopePerOperationBenchmarks)] = ParallelEvent,
            [typeof(MediatRTaskDirectHandlerBenchmarks)] = _ => BenchmarkExpectation.Request(),
            [typeof(MediatRCatalogSizeBenchmarks)] = _ => BenchmarkExpectation.Request(),
            [typeof(MediatRMixedTrafficBenchmarks)] = MixedTrafficExpectation,
        };

        return BenchmarkVerification.Run(typeof(Program).Assembly, expectations);

        static BenchmarkExpectation RequestWithStages(IReadOnlyDictionary<string, object?> parameters)
            => BenchmarkExpectation.Request(stages: (int)parameters[nameof(MediatRTaskRetainedScopeBenchmarks.StageCount)]!);

        static BenchmarkExpectation VoidRequestWithStages(IReadOnlyDictionary<string, object?> parameters)
            => BenchmarkExpectation.VoidRequest(stages: (int)parameters[nameof(MediatRTaskRetainedScopeBenchmarks.StageCount)]!);

        static BenchmarkExpectation StreamWithStages(IReadOnlyDictionary<string, object?> parameters)
            => BenchmarkExpectation.Stream(
                stages: (int)parameters[nameof(MediatRTaskRetainedScopeBenchmarks.StageCount)]!,
                count: (int)parameters[nameof(MediatRStreamRetainedScopeBenchmarks.ItemCount)]!);

        static BenchmarkExpectation SequentialEvent(IReadOnlyDictionary<string, object?> parameters)
            => BenchmarkExpectation.Event(handlers: (int)parameters[nameof(MediatREventSequentialRetainedScopeBenchmarks.HandlerCount)]!);

        static BenchmarkExpectation ParallelEvent(IReadOnlyDictionary<string, object?> parameters)
        {
            int handlers = (int)parameters[nameof(MediatREventSequentialRetainedScopeBenchmarks.HandlerCount)]!;
            bool verifyFanOut = (CompletionKind)parameters[nameof(MediatREventParallelRetainedScopeBenchmarks.Completion)]! == CompletionKind.Suspending;

            return BenchmarkExpectation.Event(handlers, ordered: false,
                parallelHandlers: verifyFanOut ? handlers : 0);
        }
    }

    private static BenchmarkExpectation MixedTrafficExpectation(IReadOnlyDictionary<string, object?> parameters)
    {
        IReadOnlyList<Type> order = RuntimeCatalog.MixedTrafficOrder((int)parameters[nameof(MediatRMixedTrafficBenchmarks.CatalogSize)]!);

        return new(
            Names: [.. Enumerable.Repeat(TraceNames.Handler, RuntimeCatalog.MixedTrafficBatch)],
            Result: RuntimeCatalog.MixedTrafficBatch * BenchmarkWorkload.Expected,
            HandlerTypes: new Dictionary<string, Type[]>
            {
                [nameof(MediatRMixedTrafficBenchmarks.RequestFlow)] =
                    [.. order.Select(tag => typeof(RuntimeTaskHandler<>).MakeGenericType(tag))],
                [nameof(MediatRMixedTrafficBenchmarks.MediatR)] =
                    [.. order.Select(tag => typeof(MediatRCatalogHandler<>).MakeGenericType(tag))],
            });
    }

    private static TRequest CatalogRequest<TRequest>(Type requestType, ExecutionTrace trace)
    {
        var request = (WorkloadMessage)Activator.CreateInstance(requestType, CompletionKind.Cached)!;

        request.Trace = trace;

        return (TRequest)(object)request;
    }
}
