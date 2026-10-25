# RequestFlow vs DispatchR 2.3.1

- Compares RequestFlow with `DispatchR.Mediator 2.3.1` on .NET 10.
- Results and behavior notes on this page describe DispatchR.Mediator 2.3.1 only, not other releases.
- RequestFlow is not affiliated with or endorsed by DispatchR or its maintainers.
- Each class pairs a RequestFlow baseline with the matching native DispatchR operation.
- [Published results](../../docs/benchmarks/dispatchr/README.md) come from the 2026-10-25 full run.
- Probes check behavior, not performance.

## Matrix

| Group | Completion | Work | Lifetime |
| --- | --- | --- | --- |
| Task response | Cached, newly completed, suspending | Returns 79; 0/1/5 stages | Retained scope and new scope per send |
| Task void request | Synchronous, suspending | 0/1/5 stages | Retained scope and new scope per send |
| ValueTask response | Synchronous, suspending | Returns 79; 0/1/5 stages | Retained scope and new scope per send |
| ValueTask void request | Synchronous, suspending | 0/1/5 stages | Retained scope and new scope per send |
| Stream | Synchronous, suspending | Fully consumes 1/10/100 items; 0/1/5 forwarding stages | Retained scope and new scope per enumeration |
| Sequential events | Synchronous, suspending | Awaits 1/5 handlers | Retained scope and new scope per publication |
| Direct Task handler reference | Cached, newly completed, suspending | One concrete handler call per row with a reused request | No dispatch or DI resolution in the method |
| Direct ValueTask handler reference | Synchronous, suspending | One concrete handler call per row with a reused request | No dispatch or DI resolution in the method |
| Catalog size | Cached, newly completed, suspending | One typed send within equal 4/16/64 handler catalogs | Retained scope |
| Mixed traffic | Cached, newly completed, suspending | 64 sends covering every request type of equal 4/16/64 handler catalogs; checksum 5,056 | Retained scope |
| Short circuit | Authorization reject, cache hit, full execution | Authorization, cache, transaction, handler | Retained scope and new scope per send |

- Synchronous stages forward their native awaitable. Suspending request stages await it. Stream stages forward the sequence without adding their own iterator.
- Catalog-size rows always send `RuntimeTag1`, so the lookup key and call target stay fixed while the catalog grows.
- Mixed traffic varies both. It sends every request type equally often, in one fixed shuffled order from `RuntimeCatalog.MixedTrafficOrder` that both libraries share.
- DispatchR resolves the handler from the compile-time request type, so setup binds each mixed-traffic send to a typed delegate, and both libraries call through one delegate per send. RequestFlow's delegate calls its `IRequest<int>` overload, which dispatches on the runtime type.
- This suite's RequestFlow mixed row is therefore not comparable with the MediatR suite's, which sends without a delegate.
- Direct-handler rows compare matching concrete handler calls. Their ratios describe workload cost and do not rank dispatchers.

## Reading a result

- `Mean` is the time of the whole operation the row describes. `Allocated` is all managed memory that operation allocates, including handler allocations such as a new Task or a stream iterator.
- `RetainedScope` setup builds providers, creates scopes, resolves dispatchers, constructs requests, and runs both methods once. Timed calls reuse those dispatchers, requests, and activated components, DispatchR's linked chain included.
- `ScopePerOperation` setup builds providers, constructs reusable requests and warms both methods. Each timed call creates a scope, resolves its dispatcher, awaits completion, and disposes the scope. `Allocated` includes the activated handlers, stages, shared disposable test dependency, and DI bookkeeping.
- Retained-scope request, void request, event, catalog-size and short-circuit methods return the dispatcher's awaitable directly, with no wrapper.
- New-scope Task response and short-circuit methods are `async Task<int>` wrappers. Their results fall outside the runtime's cached tasks, so each operation allocates one extra `Task<int>` on both sides.
- New-scope Task void request methods are `async Task` for RequestFlow and `async Task<DispatchRVoidRequestResult>` for DispatchR. Neither wrapper allocates unless the operation suspends, because on .NET 10 `Task.FromResult` returns a cached task for the default value of a 1-byte struct such as `DispatchRVoidRequestResult`.
- New-scope ValueTask methods are `async ValueTask` or `async ValueTask<T>` on both sides. New-scope event methods are `async Task` for RequestFlow and `async ValueTask` for DispatchR. These wrappers allocate only when the operation suspends.
- Mixed traffic sets `OperationsPerInvoke = 64`, so rows report cost per send. Each invocation also allocates one `Task<int>` for the uncached checksum, so a cached mixed row never shows 0 B.
- Timed code keeps the null checks for optional tracing. Only probes create trace records.

## Native API differences

- Both dispatchers run scoped: DispatchR 2.3.1 registers `IMediator`, request handlers and pipeline behaviors as scoped, and RequestFlow's dispatchers are scoped by default. The probe checks both. The MediatR suite runs RequestFlow's dispatcher transient instead, to match MediatR.
- `IMediator.Send` resolves `IRequestHandler<TRequest, TResponse>` from the scope on every send. The native registration is a scoped factory that resolves the handler and its pipeline behaviors as keyed services and links them, so each scope builds its whole chain on its first send and reuses it afterwards.
- The outermost pipeline behavior is registered as a keyed `IRequestHandler` and also returned by the factory, so disposing the scope disposes it twice. The short-circuit probe expects two authorization disposals.
- `ExcludeHandlers` applies to handlers only, and the assembly scan adds every pipeline behavior declared for a request type to that request's chain. Setup removes the keyed descriptors of pipeline behaviors a row did not select.
- Catalog rows exclude `DispatchRCatalogHandler<>` from the scan and register each closed `IRequestHandler<DispatchRCatalogRequest<TTag>, Task<int>>` with plain `AddScoped`, so `Send` resolves the handler directly instead of through the keyed factory.
- DispatchR's package README says to use a generic `Task<T>` or `ValueTask<T>` result, because a bare `Task` or `ValueTask` may stop the handler from running when pipeline behaviors are present. DispatchR void request rows therefore return the empty benchmark-owned `DispatchRVoidRequestResult` struct. RequestFlow void handlers return plain `Task` or `ValueTask`.
- DispatchR void request handlers run the shared `BenchmarkWorkload.TaskUnitVoidRequest` or `ValueUnitVoidRequest`, the same work as RequestFlow's `TaskVoidRequest` and `ValueVoidRequest`.
- `Publish` resolves `IEnumerable<INotificationHandler<TEvent>>` on every call and runs the handlers in order, awaiting only those that did not complete synchronously. DispatchR has no parallel publisher, so this suite has no parallel event rows.
- The suite registers event handlers as scoped, in handler order, after native registration. DispatchR events return `ValueTask`; RequestFlow events return `Task`.
- Event rows cover successful publication of an exact event type. Failure, cancellation, and inheritance semantics are not compared.
- Timed stream rows always consume the full sequence. Probes also check early disposal.

## Short-circuit requests

The pipeline is authorization, cache, transaction, then handler. Authorization and cache can return without calling the next component. The transaction has a unit of work and change buffer; the handler has a repository, data session, and separate command buffer.

The constructor probe checks this on the first send in a fresh scope:

| Outcome | Components executed by both libraries | RequestFlow constructs | DispatchR 2.3.1 constructs |
| --- | --- | --- | --- |
| Authorization rejects | Authorization | Authorization and its dependencies | All pipeline behaviors, handler and their dependencies |
| Cache returns a result | Authorization and cache | Authorization, cache and their dependencies | All pipeline behaviors, handler and their dependencies |
| Request reaches the handler | All three stages and handler | All stages, handler and their dependencies | All pipeline behaviors, handler and their dependencies |

- `DispatchRShortCircuitRetainedScopeBenchmarks` has 6 cases: three outcomes and two libraries, with 0-byte buffers. Setup sends the selected outcome once, and timed rows reuse the instances it created.
- `DispatchRShortCircuitScopePerOperationBenchmarks` has 12 cases: three outcomes, 0 or 4,096 bytes per dependency buffer, and two libraries. Rows include scope creation, dispatcher resolution, activation, send, and disposal. Provider construction stays outside timing.
- Read the two classes as separate scenarios.
- All components and dependencies are scoped. The 0-byte case keeps the dependency graph without allocating the buffers.
- With 4,096-byte buffers, RequestFlow constructs neither the transaction buffer nor the handler's buffer on either early return. DispatchR 2.3.1 constructs both, even though neither the transaction nor the handler runs.
- `probe` checks every case for constructor reachability, result, execution order, token propagation, scoped reuse, dependency sizes, and disposal.

## Commands

Run from the repository root.

```powershell
dotnet restore benchmarks/RequestFlow.Benchmarks.DispatchR -p:RestoreLockedMode=true
dotnet build benchmarks/RequestFlow.Benchmarks.DispatchR -c Release --no-restore

dotnet run --project benchmarks/RequestFlow.Benchmarks.DispatchR -c Release -f net10.0 --no-build --no-restore -- probe
dotnet run --project benchmarks/RequestFlow.Benchmarks.DispatchR -c Release -f net10.0 --no-build --no-restore -- lifetimes
dotnet run --project benchmarks/RequestFlow.Benchmarks.DispatchR -c Release -f net10.0 --no-build --no-restore -- --list flat
dotnet run --project benchmarks/RequestFlow.Benchmarks.DispatchR -c Release -f net10.0 --no-build --no-restore -- --smoke --filter '*'
dotnet run --project benchmarks/RequestFlow.Benchmarks.DispatchR -c Release -f net10.0 --no-build --no-restore -- --quick --filter '*DispatchRTaskRetainedScopeBenchmarks*'

# Full run of all 260 cases into an unused directory.
dotnet run --project benchmarks/RequestFlow.Benchmarks.DispatchR -c Release -f net10.0 --no-build --no-restore -- --runtimes net10.0 --filter '*' --artifacts benchmarks/artifacts/YYYY-MM-DD-dispatchr-full
```

- `probe` runs both methods of every parameter set twice. It validates results and checks stage order, tokens, completion shape, component and dispatcher identities within and across scopes, scope reuse and disposal, stream checksums and early disposal, awaited event completion, and short-circuit construction.
- A passing probe ends with `Probe passed: checked 18 benchmark classes, 130 parameter sets and 260 cases.`
- `lifetimes` prints the DI descriptors for workload components, dispatchers, `IEventPublisher`, DispatchR's `IRequestHandler<,>`, `IStreamRequestHandler<,>` and `INotificationHandler<>` registrations, and RequestFlow stages, short-circuit graphs included. For each workload pair it also runs the identity and disposal checks, because descriptors alone do not prove lifetime parity.
- DispatchR keys each handler chain with a new `Guid`, so the `lifetimes` report numbers chains in registration order instead.
- `--list flat` lists all 36 benchmark methods without timing.
- `--smoke` runs each case once and `--quick` gives rough timings. Neither is publishable.
- To review one group on its own, select it with `--filter`, for example `'*Event*'` or `'*ShortCircuit*'`.

## Publishing a run

- Follow [Publishing a run](../README.md#publishing-a-run) in the benchmarks README before and after a full run.

## Where each row lives

Benchmark classes are in `Benchmarks/` and `ShortCircuit/`, one file per class. Class names below omit the `Benchmarks` suffix. RequestFlow fixtures live in `RequestFlow.Benchmarks.RequestFlowFixtures`.

| Benchmark classes | DispatchR setup | DispatchR workload | RequestFlow setup and workload |
| --- | --- | --- | --- |
| `DispatchRTaskRetainedScope`, `DispatchRTaskScopePerOperation` | `DispatchRSetup.TaskServices` | `Workloads/TaskWorkloads.cs` | `RequestFlowSetup.TaskServices`, `Requests/TaskRequests.cs` |
| `DispatchRTaskDirectHandler` | None. Constructs `DispatchRTaskHandler` with `new`. | `Workloads/TaskWorkloads.cs` | None. Constructs `TaskRequestHandler` from `Requests/TaskRequests.cs` with `new`. |
| `DispatchRVoidRequestRetainedScope`, `DispatchRVoidRequestScopePerOperation` | `DispatchRSetup.VoidRequestServices` | `Workloads/VoidRequestWorkloads.cs` | `RequestFlowSetup.TaskVoidRequestServices`, `Requests/TaskVoidRequests.cs` |
| `DispatchRValueRetainedScope`, `DispatchRValueScopePerOperation` | `DispatchRSetup.ValueServices` | `Workloads/ValueWorkloads.cs` | `RequestFlowSetup.ValueServices`, `Requests/ValueRequests.cs` |
| `DispatchRValueDirectHandler` | None. Constructs `DispatchRValueHandler` with `new`. | `Workloads/ValueWorkloads.cs` | None. Constructs `ValueRequestHandler` from `Requests/ValueRequests.cs` with `new`. |
| `DispatchRValueVoidRequestRetainedScope`, `DispatchRValueVoidRequestScopePerOperation` | `DispatchRSetup.ValueVoidRequestServices` | `Workloads/ValueVoidRequestWorkloads.cs` | `RequestFlowSetup.ValueVoidRequestServices`, `Requests/ValueVoidRequests.cs` |
| `DispatchRStreamRetainedScope`, `DispatchRStreamScopePerOperation` | `DispatchRSetup.StreamServices` | `Workloads/StreamWorkloads.cs` | `RequestFlowSetup.StreamServices`, `Streams/Streams.cs` |
| `DispatchREventSequentialRetainedScope`, `DispatchREventSequentialScopePerOperation` | `DispatchRSetup.EventServices` | `Workloads/EventWorkloads.cs` | `RequestFlowSetup.EventServices`, `Events/Events.cs` |
| `DispatchRCatalogSize`, `DispatchRMixedTraffic` | `DispatchRSetup.CatalogServices` | `Workloads/CatalogWorkloads.cs` | `RequestFlowSetup.CatalogServices`, `Catalog/RuntimeTaskRequests.cs` |
| `DispatchRShortCircuitRetainedScope`, `DispatchRShortCircuitScopePerOperation` | `DispatchRShortCircuitSetup.Build` | `ShortCircuit/ShortCircuitWorkloads.cs` | `ShortCircuitServices.Build`, `ShortCircuit/ShortCircuitTaskWorkloads.cs` |

- DispatchR requests, the event and `DispatchRVoidRequestResult` are in `Workloads/Messages.cs`, except the catalog request in `Workloads/CatalogWorkloads.cs` and the short-circuit request in `ShortCircuit/ShortCircuitWorkloads.cs`.
- `Verification/SuiteProbe.cs` holds the `probe` and `lifetimes` checks.
