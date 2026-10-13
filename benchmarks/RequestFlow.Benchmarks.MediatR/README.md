# RequestFlow vs MediatR 12.5.0

- Compares RequestFlow with `MediatR 12.5.0`, the last Apache-2.0 release, on .NET 10.
- Results and behavior notes on this page describe MediatR 12.5.0 only, not later releases.
- RequestFlow is not affiliated with or endorsed by MediatR or its maintainers.
- Each class pairs a RequestFlow baseline with the matching native MediatR operation.
- No timing results are published yet. Probes check behavior, not performance.
- Build and run with the [quick start](../README.md#quick-start). This page covers what is specific to MediatR.

## Matrix

| Group | Completion | Work | Lifetime |
| --- | --- | --- | --- |
| Task response | Cached, newly completed, suspending | Returns 79; 0/1/5 stages | Retained scope and new scope per send |
| Task void request | Synchronous, suspending | 0/1/5 stages | Retained scope and new scope per send |
| Stream | Synchronous, suspending | Fully consumes 1/10/100 items; 0/1/5 forwarding stages | Retained scope and new scope per enumeration |
| Sequential events | Synchronous, suspending | Awaits 1/5 handlers | Retained scope and new scope per publication |
| Parallel events | Synchronous, suspending | Awaits 1/5 handlers | Retained scope and new scope per publication |
| Direct Task handler reference | Cached, newly completed, suspending | One concrete handler call per row with a reused request | No dispatch or DI resolution in the method |
| Catalog size | Cached, newly completed, suspending | One typed send within equal 4/16/64 handler catalogs | Retained scope |
| Mixed traffic | Cached, newly completed, suspending | 64 sends covering every request type of equal 4/16/64 handler catalogs; checksum 5,056 | Retained scope |

- Synchronous stages forward their native awaitable. Suspending request stages await it. Stream stages forward the sequence without adding their own iterator.
- Catalog-size rows always send `RuntimeTag1`, so the lookup key and call target stay fixed while the catalog grows.
- Mixed traffic varies both. It sends every request type equally often, in one fixed shuffled order from `RuntimeCatalog.MixedTrafficOrder` that both libraries share. Each send goes through the library's `IRequest<int>` overload, which dispatches on the runtime type.
- Direct-handler rows compare matching concrete handler calls. Their ratios describe workload cost and do not rank dispatchers.

## Reading a result

- `Mean` is the time of the whole operation the row describes. `Allocated` is all managed memory that operation allocates, including handler allocations such as a new Task or a stream iterator.
- `RetainedScope` setup builds providers, creates scopes, resolves dispatchers, constructs requests, and runs both methods once. Timed calls reuse those dispatchers, requests, and activated handlers.
- `ScopePerOperation` setup constructs reusable requests and warms both methods. Each timed call creates a scope, resolves its dispatcher, awaits completion, and disposes the scope. `Allocated` includes the activated handlers, stages, shared disposable test dependency, and DI bookkeeping.
- New-scope Task response and short-circuit methods are `async Task<int>` wrappers. Their results fall outside the runtime's cached tasks, so each operation allocates one extra `Task<int>` on both sides.
- Mixed traffic sets `OperationsPerInvoke = 64`, so rows report cost per send. Each invocation also allocates one `Task<int>` for the uncached checksum, so a cached mixed row never shows 0 B.
- Timed code keeps the null checks for optional tracing. Only probes create trace records.

## Native API differences

- MediatR 12.5.0 registers `IMediator`, `ISender` and `IPublisher` as transient by default, and the suite keeps that default. RequestFlow's dispatchers and event publisher are scoped by default, so the suite calls `WithTransientDispatcher()` to match.
- The suite registers the selected handlers and pipeline behaviors as scoped, like RequestFlow's handlers and stages, and leaves other MediatR services at their native lifetimes.
- MediatR 12.5.0 has Task request and notification handlers but no ValueTask request family, so ValueTask cells are excluded.
- Void handlers return plain Task. MediatR 12.5.0's void pipeline contract uses `Unit`; RequestFlow's void stage contract uses plain Task.
- Sequential events use MediatR 12.5.0's default publisher. Parallel events use its native `TaskWhenAllPublisher`, in separate classes.
- The catalog fixture registers public closed generic request-handler contracts through normal DI. No MediatR implementation source is used.
- Event rows cover successful publication of an exact event type. Failure, cancellation, and inheritance semantics differ between the libraries and are not compared.
- Timed stream rows always consume the full sequence. Probes also check early disposal.

## Short-circuit requests

The pipeline is authorization, cache, transaction, then handler. Authorization and cache can return without calling the next component. The transaction has a unit of work and change buffer; the handler has a repository, data session, and separate command buffer.

The constructor probe checks this on the first send in a fresh scope:

| Outcome | Components executed by both libraries | RequestFlow constructs | MediatR 12.5.0 constructs |
| --- | --- | --- | --- |
| Authorization rejects | Authorization | Authorization and its dependencies | All pipeline behaviors and their dependencies; no handler |
| Cache returns a result | Authorization and cache | Authorization, cache and their dependencies | All pipeline behaviors and their dependencies; no handler |
| Request reaches the handler | All three stages and handler | All stages, handler and their dependencies | All pipeline behaviors, handler and their dependencies |

- `MediatRShortCircuitRetainedScopeBenchmarks` has 6 cases: three outcomes and two libraries, with 0-byte buffers. Setup sends the selected outcome once, and timed rows reuse the instances it created.
- `MediatRShortCircuitScopePerOperationBenchmarks` has 12 cases: three outcomes, 0 or 4,096 bytes per dependency buffer, and two libraries. Rows include scope creation, dispatcher resolution, activation, send, and disposal. Provider construction stays outside timing.
- Read the two classes as separate scenarios.
- All components and dependencies are scoped. The 0-byte case keeps the dependency graph without allocating the buffers.
- With 4,096-byte buffers, RequestFlow skips constructing the transaction buffer on either early return. MediatR 12.5.0 constructs it even though the transaction never runs. Neither library constructs the handler's buffer on those paths.
- `probe` checks every case for constructor reachability, result, execution order, token propagation, scoped reuse, dependency sizes, and disposal.

## Commands

Run from the repository root after the [quick start](../README.md#quick-start) build.

```powershell
dotnet run --project benchmarks/RequestFlow.Benchmarks.MediatR -c Release -f net10.0 --no-build --no-restore -- lifetimes
dotnet run --project benchmarks/RequestFlow.Benchmarks.MediatR -c Release -f net10.0 --no-build --no-restore -- --list flat
dotnet run --project benchmarks/RequestFlow.Benchmarks.MediatR -c Release -f net10.0 --no-build --no-restore -- --smoke --filter '*ShortCircuit*'
```

- `probe` runs both methods of every parameter set twice. It validates results and checks stage order, tokens, completion shape, component and dispatcher identities within and across scopes, scope disposal, stream checksums and early disposal, and awaited event completion.
- `lifetimes` prints the DI descriptors for workload components, dispatchers, `IMediator`, `INotificationPublisher`, `IEventPublisher`, every `IPipelineBehavior<,>` and `IStreamPipelineBehavior<,>`, and RequestFlow stages, short-circuit graphs included. It then runs the identity and disposal checks, because descriptors alone do not prove lifetime parity.
- `--list flat` lists all 30 benchmark methods without timing.
- To review one group on its own, select it with `--filter`, for example `'*Event*'` or `'*ShortCircuit*'`.

## Where each row lives

Benchmark classes are in `Benchmarks/` and `ShortCircuit/`, one file per class. Class names below omit the `Benchmarks` suffix. RequestFlow fixtures live in `RequestFlow.Benchmarks.RequestFlowFixtures`.

| Benchmark classes | MediatR setup | MediatR workload | RequestFlow setup and workload |
| --- | --- | --- | --- |
| `MediatRTaskRetainedScope`, `MediatRTaskScopePerOperation` | `MediatRSetup.TaskServices` | `Workloads/TaskWorkloads.cs` | `RequestFlowSetup.TaskServices`, `Requests/TaskRequests.cs` |
| `MediatRTaskDirectHandler` | None. Constructs `MediatRTaskHandler` with `new`. | `Workloads/TaskWorkloads.cs` | No setup. Constructs `TaskRequestHandler` from `Requests/TaskRequests.cs` with `new`. |
| `MediatRVoidRequestRetainedScope`, `MediatRVoidRequestScopePerOperation` | `MediatRSetup.VoidRequestServices` | `Workloads/VoidRequestWorkloads.cs` | `RequestFlowSetup.TaskVoidRequestServices`, `Requests/TaskVoidRequests.cs` |
| `MediatRStreamRetainedScope`, `MediatRStreamScopePerOperation` | `MediatRSetup.StreamServices` | `Workloads/StreamWorkloads.cs` | `RequestFlowSetup.StreamServices`, `Streams/Streams.cs` |
| `MediatREventSequential*`, `MediatREventParallel*` | `MediatRSetup.EventServices` | `Workloads/EventWorkloads.cs` | `RequestFlowSetup.EventServices`, `Events/Events.cs` |
| `MediatRCatalogSize`, `MediatRMixedTraffic` | `MediatRSetup.CatalogServices` | `Workloads/CatalogWorkloads.cs` | `RequestFlowSetup.CatalogServices`, `Catalog/RuntimeTaskRequests.cs` |
| `MediatRShortCircuitRetainedScope`, `MediatRShortCircuitScopePerOperation` | `MediatRShortCircuitSetup.Services` | `ShortCircuit/ShortCircuitWorkloads.cs` | `ShortCircuitServices.Services`, `ShortCircuit/ShortCircuitTaskWorkloads.cs` |

- Request types are in `Workloads/Messages.cs`, except the catalog request in `Workloads/CatalogWorkloads.cs` and the short-circuit request in `ShortCircuit/ShortCircuitWorkloads.cs`.
