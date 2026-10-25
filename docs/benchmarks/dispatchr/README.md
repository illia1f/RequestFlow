# Benchmarks vs DispatchR 2.3.1

Time and managed allocations of RequestFlow and DispatchR.Mediator 2.3.1 on .NET 10, for the same workloads and service lifetimes.

DispatchR 2.3.1 links a scope's handler and pipeline behaviors on the first send and reuses that chain for later sends. RequestFlow builds each chain's shape once and resolves the handler and each stage only when dispatch reaches it, so an early return never resolves the handler or the stages it skips.

RequestFlow does not cache a resolved chain. It resolves each level from the container on every send, because a cached chain would change behavior that stages rely on:

- Handlers and stages are transient by default. A cached chain would keep them for the whole scope, so a retry would reuse the handler the failed attempt left behind instead of getting a fresh one. DispatchR 2.3.1 registers them as scoped.
- A later registration can change any component's lifetime. Resolving each level from the container respects the lifetime it ends up with, while a cache would have to know it in advance.

RequestFlow wraps each stream so that the handler and stages run inside the enumeration:

- `Stream` only finds the request's plan. The handler and stages are resolved and called on the first `MoveNextAsync`, like a C# `async` iterator, so a stream that is never enumerated never resolves them.
- The token passed to `WithCancellation` reaches the handler and every stage, joined with the token passed to `Stream`. Handlers and stages receive it as a `Handle` argument, and the enumeration token is only known once enumeration starts.
- The wrapper allocates its own iterator per stream, and more when the stream suspends or when `Stream` and `WithCancellation` get two different tokens that can be canceled. It adds one `MoveNextAsync` call per item.

## Results

- Across all 125 dispatch comparisons, DispatchR is faster in 70 and RequestFlow in 42, and the other 13 are inconclusive. RequestFlow allocates less in 45, the same in 40, and more in 40.
- With a new scope per operation, RequestFlow is faster in 33 of 37 request, event, and short-circuit comparisons. DispatchR is faster in the 5-stage suspending void request and the 5-handler synchronous event, and the other 2 are inconclusive.
- The widest ratio in RequestFlow's favor is an early return in a new scope. When authorization rejects with 4,096 B buffers, RequestFlow takes 381 ns and allocates 1,024 B, while DispatchR takes 1,706 ns and allocates 10,872 B.
- In a new scope, RequestFlow allocates less in 41 of 55 comparisons.
- In a reused scope, DispatchR is faster in 54 of 70 comparisons and RequestFlow in 8. Six of RequestFlow's 8 have suspending handlers, and the other 2 are 4-handler mixed traffic.
- The widest ratio in DispatchR's favor is a void request through 5 synchronous stages in a reused scope: 268 ns in RequestFlow and 51 ns in DispatchR. BenchmarkDotNet flagged RequestFlow's side of that row as multimodal.
- DispatchR is faster in 32 of 36 stream comparisons, RequestFlow in 1, and 3 are inconclusive.
- RequestFlow allocates more in 30 of the 36 streams and in 5 of the 6 Task void requests with suspending handlers. The widest gap is 629 B: 1,281 B against 652 B with 5 stages in a reused scope. The other 5 rows where it allocates more differ by 16 B or less.
- The handlers do small in-memory work, and every case ran on one laptop. These results do not predict application throughput or endpoint latency.
- Results describe DispatchR.Mediator 2.3.1 only, not later releases. RequestFlow is not affiliated with or endorsed by DispatchR or its maintainers.

## Run

- Measured on 2026-10-25 from commit [`fccc60c`](https://github.com/illia1f/RequestFlow/commit/fccc60c032f6dfa33c475d1bc119c94054a135e9) (RequestFlow 1.0.0) with a clean working tree.
- DispatchR.Mediator 2.3.1, Microsoft.Extensions.DependencyInjection 10.0.9, and BenchmarkDotNet 0.15.8, pinned by package lock files.
- .NET SDK 10.0.301, .NET 10.0.9, X64 RyuJIT x86-64-v3, workstation GC, Release build.
- AMD Ryzen 7 5800H with 8 cores and 16 logical processors, all available to the run (affinity mask `0xFFFF`). Suspending times depend on how many cores are available.
- Windows 11 23H2 build 22631.6199, AC power, High performance power plan.
- Each case ran in two launches of 60 measured iterations. The job allowed 15 to 50 warm-up and 60 to 100 measured iterations.
- The relative error target was 5% at 99.9% confidence. All 260 cases met it; the worst reached 4.41%.
- BenchmarkDotNet flagged 42 of the 260 cases, from both libraries, as multimodal. `run-manifest.json` lists them under `warnings`.
- BenchmarkDotNet set each case's invocation count so a pilot iteration took at least 500 ms. Measured iterations can run shorter: 42 of 520 launches have a median iteration below 500 ms, the lowest at 193 ms.
- The correctness probe passed for all 260 cases before timing. The run took 8 hours 4 minutes.
- [Raw BenchmarkDotNet reports and the run manifest](2026-10-25-dispatchr-full/). The reports round values.
- Tables round the means and intervals from BenchmarkDotNet's JSON export, which is not published. [paired-results.csv](2026-10-25-dispatchr-full/paired-results.csv) carries the exact values for every row, and [launch-results.csv](2026-10-25-dispatchr-full/launch-results.csv) lists each launch's mean and median iteration time.

## What each row measures

- Both libraries run the same handler work. Handlers and pipeline stages are scoped on both sides. RequestFlow's handlers and stages are transient by default, so the suite registers them with `WithScopedHandlers()` and `AsScoped()`.
- Dispatchers are scoped on both sides. DispatchR 2.3.1 registers `IMediator` as scoped, and RequestFlow's dispatchers are scoped by default.
- DispatchR is resolved through `IMediator`.
- Reused scope: the scope, dispatcher, handlers, and stages exist before timing, and so does DispatchR's linked chain. A row measures dispatch, stages, and handler.
- New scope: each operation creates a scope, resolves the dispatcher, activates handlers and stages, runs them, and disposes the scope.
- Cached handlers return a stored completed task. Newly completed handlers create a completed task.
- Synchronous ValueTask, void, and event handlers return an already completed awaitable, and synchronous streams yield each item without awaiting.
- Suspending handlers await `Task.Yield()`, so their times include thread-pool scheduling but no I/O.
- Stages in request rows return the next step's awaitable, except in suspending rows, where each stage awaits it. Stream and short-circuit stages never await.
- DispatchR's package README asks for a generic `Task<T>` or `ValueTask<T>` result, because a bare `Task` or `ValueTask` may stop the handler from running when pipeline behaviors are present. DispatchR void handlers therefore return a `Task` or `ValueTask` of `DispatchRVoidRequestResult`, an empty struct the suite defines. RequestFlow's void handlers return plain `Task` or `ValueTask`.
- Streams are fully consumed and events are awaited inside the operation. DispatchR event handlers return `ValueTask` and RequestFlow's return `Task`. Event rows cover successful delivery, not failure or inheritance semantics.
- Allocated bytes include every managed allocation in the operation: handler, stages, DI, and async state.
- The [DispatchR suite guide](../../../benchmarks/RequestFlow.Benchmarks.DispatchR/README.md) lists native API differences and fixture costs.

## Reading the tables

- Times are nanoseconds per operation, shown as the mean ± the 99.9% confidence interval half-width. Bytes are managed bytes per operation.
- Each row pairs the two libraries on one parameter set, so the 260 cases form 130 rows. The 5 direct handler rows are references, which leaves 125 dispatch comparisons.
- Faster names the library with the lower mean when the two intervals do not overlap and both of its launch means are below both of the other library's. Otherwise the row is Inconclusive. `paired-results.csv` splits this into two columns: `Verdict` for the intervals and `LaunchesAgree` for the launches.
- Raw reports and CSVs use the benchmark names: Reused scope is `RetainedScope`, New scope per operation is `ScopePerOperation`, Newly completed is `Completion=Synchronous`, and Reaches handler is `Outcome=FullExecution`.
- Compare rows within one table. Workloads differ between tables.
- Don't compare RequestFlow rows with the [MediatR page](../mediatr/README.md). That suite runs RequestFlow's dispatcher transient to match MediatR, and its mixed-traffic rows send without a delegate.

## Task requests

The handler returns `Task<int>` through 0, 1, or 5 pipeline stages.

### Reused scope

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Cached, 0 stages | 54.52 ± 0.84 | 37.87 ± 0.34 | 0 | 0 | DispatchR |
| Cached, 1 stage | 85.75 ± 0.35 | 36.81 ± 0.28 | 0 | 0 | DispatchR |
| Cached, 5 stages | 244.51 ± 0.19 | 61.03 ± 2.69 | 0 | 0 | DispatchR |
| Newly completed, 0 stages | 54.92 ± 0.27 | 39.49 ± 0.20 | 72 | 72 | DispatchR |
| Newly completed, 1 stage | 88.53 ± 0.34 | 42.05 ± 0.58 | 72 | 72 | DispatchR |
| Newly completed, 5 stages | 243.09 ± 0.80 | 57.28 ± 0.96 | 72 | 72 | DispatchR |
| Suspending, 0 stages | 695.72 ± 1.51 | 709.81 ± 1.92 | 104 | 104 | RequestFlow |
| Suspending, 1 stage | 681.34 ± 1.42 | 701.05 ± 1.99 | 216 | 215 | RequestFlow |
| Suspending, 5 stages | 1,034.85 ± 7.45 | 857.31 ± 2.63 | 662 | 660 | DispatchR |

### New scope per operation

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Cached, 0 stages | 235.28 ± 1.41 | 371.78 ± 2.93 | 560 | 944 | RequestFlow |
| Cached, 1 stage | 394.91 ± 7.18 | 475.44 ± 1.68 | 952 | 992 | RequestFlow |
| Cached, 5 stages | 844.28 ± 1.61 | 959.48 ± 13.16 | 1,968 | 2,072 | RequestFlow |
| Newly completed, 0 stages | 240.85 ± 1.26 | 383.14 ± 0.99 | 632 | 1,016 | RequestFlow |
| Newly completed, 1 stage | 383.74 ± 4.78 | 469.33 ± 2.47 | 1,024 | 1,064 | RequestFlow |
| Newly completed, 5 stages | 882.85 ± 2.28 | 937.07 ± 11.23 | 2,040 | 2,144 | RequestFlow |
| Suspending, 0 stages | 842.43 ± 5.61 | 1,029.00 ± 3.14 | 712 | 1,096 | RequestFlow |
| Suspending, 1 stage | 1,087.74 ± 6.69 | 1,183.33 ± 3.92 | 1,216 | 1,256 | RequestFlow |
| Suspending, 5 stages | 1,876.72 ± 5.36 | 1,957.12 ± 4.23 | 2,679 | 2,783 | RequestFlow |

## Void requests

RequestFlow's handler returns plain `Task` and DispatchR's returns `Task` of an empty struct, through 0, 1, or 5 pipeline stages.

### Reused scope

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 0 stages | 45.65 ± 0.33 | 37.43 ± 0.26 | 0 | 0 | DispatchR |
| Synchronous, 1 stage | 101.16 ± 0.63 | 35.97 ± 0.11 | 0 | 0 | DispatchR |
| Synchronous, 5 stages | 267.51 ± 9.00 | 50.94 ± 0.09 | 0 | 0 | DispatchR |
| Suspending, 0 stages | 698.66 ± 3.85 | 703.93 ± 2.36 | 213 | 104 | Inconclusive |
| Suspending, 1 stage | 738.94 ± 1.98 | 710.93 ± 1.65 | 438 | 212 | DispatchR |
| Suspending, 5 stages | 1,533.67 ± 3.38 | 856.63 ± 2.44 | 1,281 | 652 | DispatchR |

### New scope per operation

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 0 stages | 221.83 ± 0.67 | 373.21 ± 1.51 | 488 | 872 | RequestFlow |
| Synchronous, 1 stage | 365.64 ± 2.00 | 470.60 ± 2.39 | 880 | 920 | RequestFlow |
| Synchronous, 5 stages | 840.33 ± 3.50 | 934.83 ± 14.10 | 1,896 | 2,000 | RequestFlow |
| Suspending, 0 stages | 894.67 ± 5.28 | 1,057.56 ± 4.72 | 824 | 1,096 | RequestFlow |
| Suspending, 1 stage | 1,203.29 ± 2.96 | 1,217.98 ± 7.59 | 1,439 | 1,256 | RequestFlow |
| Suspending, 5 stages | 2,321.44 ± 8.04 | 1,919.73 ± 10.88 | 3,312 | 2,782 | DispatchR |

## ValueTask requests

The handler returns `ValueTask<int>` through 0, 1, or 5 pipeline stages.

### Reused scope

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 0 stages | 58.25 ± 0.62 | 41.10 ± 0.47 | 0 | 0 | DispatchR |
| Synchronous, 1 stage | 91.93 ± 0.07 | 55.36 ± 0.42 | 0 | 0 | DispatchR |
| Synchronous, 5 stages | 273.25 ± 0.48 | 82.70 ± 2.32 | 0 | 0 | DispatchR |
| Suspending, 0 stages | 681.41 ± 3.36 | 702.74 ± 1.44 | 112 | 112 | RequestFlow |
| Suspending, 1 stage | 703.42 ± 1.66 | 688.75 ± 2.66 | 239 | 238 | DispatchR |
| Suspending, 5 stages | 1,145.58 ± 4.47 | 952.04 ± 3.68 | 736 | 737 | DispatchR |

### New scope per operation

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 0 stages | 259.89 ± 5.66 | 377.65 ± 1.92 | 488 | 872 | RequestFlow |
| Synchronous, 1 stage | 367.47 ± 1.85 | 464.86 ± 2.66 | 880 | 920 | RequestFlow |
| Synchronous, 5 stages | 875.05 ± 5.87 | 905.25 ± 2.42 | 1,896 | 2,000 | RequestFlow |
| Suspending, 0 stages | 901.66 ± 4.97 | 1,055.01 ± 4.69 | 728 | 1,112 | RequestFlow |
| Suspending, 1 stage | 1,100.84 ± 5.62 | 1,227.77 ± 4.36 | 1,248 | 1,288 | RequestFlow |
| Suspending, 5 stages | 2,013.08 ± 6.26 | 2,069.33 ± 16.98 | 2,767 | 2,873 | Inconclusive |

## ValueTask void requests

RequestFlow's handler returns plain `ValueTask` and DispatchR's returns `ValueTask` of an empty struct, through 0, 1, or 5 pipeline stages.

### Reused scope

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 0 stages | 53.57 ± 0.08 | 42.35 ± 0.38 | 0 | 0 | DispatchR |
| Synchronous, 1 stage | 106.79 ± 0.13 | 47.44 ± 0.19 | 0 | 0 | DispatchR |
| Synchronous, 5 stages | 291.12 ± 1.02 | 81.37 ± 2.36 | 0 | 0 | DispatchR |
| Suspending, 0 stages | 702.07 ± 1.36 | 701.29 ± 1.72 | 104 | 112 | Inconclusive |
| Suspending, 1 stage | 700.28 ± 1.31 | 712.38 ± 0.88 | 223 | 238 | RequestFlow |
| Suspending, 5 stages | 1,151.35 ± 3.67 | 967.00 ± 3.59 | 695 | 734 | DispatchR |

### New scope per operation

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 0 stages | 246.50 ± 4.24 | 376.45 ± 3.32 | 488 | 872 | RequestFlow |
| Synchronous, 1 stage | 372.01 ± 3.57 | 482.41 ± 2.61 | 880 | 920 | RequestFlow |
| Synchronous, 5 stages | 899.62 ± 2.49 | 972.14 ± 6.46 | 1,896 | 2,000 | RequestFlow |
| Suspending, 0 stages | 886.49 ± 2.91 | 1,066.35 ± 7.66 | 712 | 1,112 | RequestFlow |
| Suspending, 1 stage | 1,107.71 ± 6.09 | 1,239.06 ± 5.01 | 1,224 | 1,288 | RequestFlow |
| Suspending, 5 stages | 2,044.94 ± 14.70 | 2,048.07 ± 3.83 | 2,710 | 2,872 | Inconclusive |

## Streams

Each operation fully consumes 1, 10, or 100 items through 0, 1, or 5 forwarding stages.

### Reused scope

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| 1 item, synchronous, 0 stages | 165.25 ± 0.55 | 89.00 ± 0.19 | 376 | 160 | DispatchR |
| 1 item, synchronous, 1 stage | 206.94 ± 1.05 | 91.97 ± 0.67 | 376 | 160 | DispatchR |
| 1 item, synchronous, 5 stages | 370.93 ± 2.50 | 107.64 ± 1.95 | 376 | 160 | DispatchR |
| 1 item, suspending, 0 stages | 843.69 ± 5.08 | 698.19 ± 3.02 | 696 | 399 | DispatchR |
| 1 item, suspending, 1 stage | 863.31 ± 4.55 | 717.40 ± 1.84 | 696 | 399 | DispatchR |
| 1 item, suspending, 5 stages | 1,061.36 ± 7.53 | 725.74 ± 5.92 | 696 | 400 | DispatchR |
| 10 items, synchronous, 0 stages | 428.32 ± 1.44 | 228.86 ± 1.45 | 448 | 232 | DispatchR |
| 10 items, synchronous, 1 stage | 455.60 ± 1.56 | 225.12 ± 0.87 | 448 | 232 | DispatchR |
| 10 items, synchronous, 5 stages | 619.50 ± 1.84 | 238.55 ± 0.40 | 448 | 232 | DispatchR |
| 10 items, suspending, 0 stages | 6,636.47 ± 68.51 | 5,581.94 ± 10.39 | 696 | 400 | DispatchR |
| 10 items, suspending, 1 stage | 6,571.86 ± 21.22 | 5,640.64 ± 11.45 | 696 | 400 | DispatchR |
| 10 items, suspending, 5 stages | 6,751.98 ± 38.14 | 5,670.55 ± 12.28 | 696 | 400 | DispatchR |
| 100 items, synchronous, 0 stages | 3,054.37 ± 4.52 | 1,361.80 ± 1.64 | 448 | 232 | DispatchR |
| 100 items, synchronous, 1 stage | 3,181.98 ± 19.85 | 1,372.66 ± 1.74 | 448 | 232 | DispatchR |
| 100 items, synchronous, 5 stages | 3,142.48 ± 5.33 | 1,377.73 ± 1.50 | 448 | 232 | DispatchR |
| 100 items, suspending, 0 stages | 67,745.88 ± 233.42 | 62,074.76 ± 137.41 | 760 | 464 | DispatchR |
| 100 items, suspending, 1 stage | 68,765.53 ± 97.33 | 62,243.76 ± 120.02 | 760 | 464 | DispatchR |
| 100 items, suspending, 5 stages | 71,064.42 ± 1,193.40 | 62,793.77 ± 107.16 | 760 | 464 | DispatchR |

### New scope per operation

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| 1 item, synchronous, 0 stages | 388.17 ± 7.65 | 441.98 ± 1.21 | 864 | 1,032 | RequestFlow |
| 1 item, synchronous, 1 stage | 585.14 ± 11.96 | 563.12 ± 3.95 | 1,256 | 1,080 | Inconclusive |
| 1 item, synchronous, 5 stages | 1,023.67 ± 3.77 | 1,002.45 ± 8.03 | 2,272 | 2,160 | Inconclusive |
| 1 item, suspending, 0 stages | 1,159.99 ± 7.32 | 1,172.41 ± 4.83 | 1,304 | 1,392 | Inconclusive |
| 1 item, suspending, 1 stage | 1,334.12 ± 4.81 | 1,295.15 ± 3.03 | 1,696 | 1,440 | DispatchR |
| 1 item, suspending, 5 stages | 1,948.26 ± 6.74 | 1,829.43 ± 9.49 | 2,712 | 2,520 | DispatchR |
| 10 items, synchronous, 0 stages | 665.50 ± 2.40 | 590.47 ± 3.42 | 1,008 | 1,176 | DispatchR |
| 10 items, synchronous, 1 stage | 791.16 ± 1.75 | 704.78 ± 3.47 | 1,400 | 1,224 | DispatchR |
| 10 items, synchronous, 5 stages | 1,378.50 ± 7.19 | 1,160.72 ± 8.30 | 2,416 | 2,304 | DispatchR |
| 10 items, suspending, 0 stages | 6,818.03 ± 13.69 | 6,214.32 ± 19.16 | 1,304 | 1,392 | DispatchR |
| 10 items, suspending, 1 stage | 7,172.21 ± 17.22 | 6,365.19 ± 40.26 | 1,696 | 1,440 | DispatchR |
| 10 items, suspending, 5 stages | 7,960.85 ± 28.12 | 7,025.70 ± 25.91 | 2,712 | 2,520 | DispatchR |
| 100 items, synchronous, 0 stages | 3,174.32 ± 6.47 | 1,747.91 ± 5.71 | 1,008 | 1,176 | DispatchR |
| 100 items, synchronous, 1 stage | 3,446.64 ± 15.25 | 1,868.64 ± 13.51 | 1,400 | 1,224 | DispatchR |
| 100 items, synchronous, 5 stages | 3,950.87 ± 16.94 | 2,292.59 ± 12.01 | 2,416 | 2,304 | DispatchR |
| 100 items, suspending, 0 stages | 68,260.10 ± 54.69 | 63,528.08 ± 270.24 | 1,368 | 1,456 | DispatchR |
| 100 items, suspending, 1 stage | 68,946.62 ± 384.31 | 63,507.87 ± 162.94 | 1,760 | 1,504 | DispatchR |
| 100 items, suspending, 5 stages | 67,506.59 ± 609.47 | 63,776.45 ± 81.30 | 2,776 | 2,584 | DispatchR |

## Sequential events

Publishing awaits 1 or 5 handlers in order. DispatchR has no parallel publisher, so there are no parallel event rows.

### Reused scope

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 1 handler | 64.67 ± 0.95 | 58.24 ± 0.87 | 0 | 0 | DispatchR |
| Synchronous, 5 handlers | 253.08 ± 0.24 | 83.08 ± 0.58 | 0 | 0 | DispatchR |
| Suspending, 1 handler | 713.94 ± 3.64 | 702.09 ± 2.24 | 251 | 238 | Inconclusive |
| Suspending, 5 handlers | 2,507.02 ± 8.11 | 2,748.51 ± 5.62 | 672 | 656 | RequestFlow |

### New scope per operation

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 1 handler | 241.57 ± 1.20 | 321.92 ± 2.36 | 488 | 872 | RequestFlow |
| Synchronous, 5 handlers | 757.53 ± 5.42 | 722.69 ± 2.36 | 1,064 | 1,920 | DispatchR |
| Suspending, 1 handler | 903.03 ± 4.54 | 1,064.14 ± 5.85 | 864 | 1,232 | RequestFlow |
| Suspending, 5 handlers | 2,973.73 ± 10.99 | 3,571.16 ± 13.08 | 1,856 | 2,696 | RequestFlow |

## Short-circuit pipeline

Authorization, cache, and transaction stages run before the handler. Authorization can reject, the cache can return a stored result, or the request reaches the handler. The transaction's change buffer and the handler's command buffer hold 0 or 4,096 bytes in a new scope, and 0 bytes in a reused scope.

RequestFlow constructs a stage and its dependencies when the dispatch reaches it. DispatchR 2.3.1 constructs every pipeline behavior, the handler and their dependencies on the first send in a scope, including the ones an early return skips.

### Reused scope

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Authorization rejects | 44.73 ± 0.05 | 37.07 ± 0.14 | 0 | 0 | DispatchR |
| Cache hit | 83.32 ± 0.16 | 40.85 ± 1.35 | 0 | 0 | DispatchR |
| Reaches handler | 165.53 ± 1.50 | 40.06 ± 0.25 | 0 | 0 | DispatchR |

### New scope per operation

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Authorization rejects, 0 B buffers | 402.64 ± 1.79 | 1,343.84 ± 8.01 | 1,024 | 2,632 | RequestFlow |
| Authorization rejects, 4,096 B buffers | 381.45 ± 2.38 | 1,706.18 ± 32.39 | 1,024 | 10,872 | RequestFlow |
| Cache hit, 0 B buffers | 601.89 ± 3.60 | 1,320.85 ± 4.98 | 1,216 | 2,632 | RequestFlow |
| Cache hit, 4,096 B buffers | 605.04 ± 1.37 | 1,733.03 ± 20.01 | 1,216 | 10,872 | RequestFlow |
| Reaches handler, 0 B buffers | 1,288.43 ± 8.36 | 1,340.17 ± 6.01 | 2,560 | 2,632 | RequestFlow |
| Reaches handler, 4,096 B buffers | 1,558.13 ± 4.80 | 1,614.66 ± 9.91 | 10,800 | 10,872 | RequestFlow |

## Catalog size

One request type is sent within a catalog of 4, 16, or 64 request handlers, in a reused scope.

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| 4 handlers, cached | 51.62 ± 0.67 | 38.57 ± 0.15 | 0 | 0 | DispatchR |
| 4 handlers, newly completed | 55.24 ± 0.12 | 42.63 ± 0.37 | 72 | 72 | DispatchR |
| 4 handlers, suspending | 694.37 ± 3.76 | 705.43 ± 1.70 | 104 | 104 | Inconclusive |
| 16 handlers, cached | 56.88 ± 0.14 | 41.57 ± 0.77 | 0 | 0 | DispatchR |
| 16 handlers, newly completed | 57.95 ± 0.39 | 40.62 ± 0.22 | 72 | 72 | DispatchR |
| 16 handlers, suspending | 706.20 ± 3.71 | 705.19 ± 1.70 | 104 | 104 | Inconclusive |
| 64 handlers, cached | 54.53 ± 0.48 | 37.70 ± 0.08 | 0 | 0 | DispatchR |
| 64 handlers, newly completed | 59.82 ± 0.24 | 40.38 ± 0.43 | 72 | 72 | DispatchR |
| 64 handlers, suspending | 681.12 ± 2.41 | 696.27 ± 3.21 | 104 | 104 | RequestFlow |

## Mixed traffic

Every request type in a 4, 16, or 64 handler catalog is sent in one fixed shuffled order, in a reused scope. Times and bytes are per send. DispatchR picks the handler from the compile-time request type, so both libraries send through one delegate per request. RequestFlow's delegate calls its `IRequest<int>` overload, which dispatches on the runtime type. Each batch of 64 sends also allocates one `Task<int>` for its checksum on both sides, which adds about 1 B per send, or 3 B in suspending rows.

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| 4 handlers, cached | 57.62 ± 0.05 | 61.54 ± 0.35 | 1 | 1 | RequestFlow |
| 4 handlers, newly completed | 60.94 ± 0.52 | 64.90 ± 0.39 | 73 | 73 | RequestFlow |
| 4 handlers, suspending | 495.86 ± 1.97 | 499.21 ± 1.38 | 107 | 107 | Inconclusive |
| 16 handlers, cached | 79.14 ± 0.16 | 68.61 ± 1.09 | 1 | 1 | DispatchR |
| 16 handlers, newly completed | 87.11 ± 0.26 | 78.88 ± 0.48 | 73 | 73 | DispatchR |
| 16 handlers, suspending | 514.63 ± 0.88 | 512.45 ± 0.79 | 107 | 107 | Inconclusive |
| 64 handlers, cached | 107.35 ± 0.56 | 87.30 ± 0.67 | 1 | 1 | DispatchR |
| 64 handlers, newly completed | 115.64 ± 0.29 | 102.77 ± 0.51 | 73 | 73 | DispatchR |
| 64 handlers, suspending | 538.24 ± 1.32 | 537.55 ± 1.19 | 107 | 107 | Inconclusive |

## Direct Task handler reference

Each library's Task handler is called directly, without dispatch or DI. These rows show the cost of the handler work itself, so they rank neither dispatcher.

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Cached | 1.589 ± 0.002 | 1.578 ± 0.002 | 0 | 0 | Reference |
| Newly completed | 4.61 ± 0.01 | 4.75 ± 0.06 | 72 | 72 | Reference |
| Suspending | 691.92 ± 3.28 | 695.94 ± 0.75 | 104 | 104 | Reference |

## Direct ValueTask handler reference

Each library's ValueTask handler is called directly, without dispatch or DI. These rows rank neither dispatcher.

| Case | RequestFlow (ns) | DispatchR (ns) | RequestFlow (B) | DispatchR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous | 1.994 ± 0.005 | 2.000 ± 0.003 | 0 | 0 | Reference |
| Suspending | 702.90 ± 1.60 | 700.54 ± 2.51 | 112 | 112 | Reference |

## Reproducing

Run from the repository root at the measured commit, on an otherwise idle machine:

```powershell
dotnet restore benchmarks/RequestFlow.Benchmarks.DispatchR -p:RestoreLockedMode=true
dotnet build benchmarks/RequestFlow.Benchmarks.DispatchR -c Release --no-restore
dotnet run --project benchmarks/RequestFlow.Benchmarks.DispatchR -c Release -f net10.0 --no-build --no-restore -- --runtimes net10.0 --filter '*' --artifacts benchmarks/artifacts/YYYY-MM-DD-dispatchr-full
```

- The run checks correctness before timing and writes `run-manifest.json` to the artifact folder.
- The [benchmark guide](../../../benchmarks/README.md) covers quick runs, filters, and the publication checks.
