# Benchmarks vs MediatR 12.5.0

Time and managed allocations of RequestFlow and MediatR 12.5.0 on .NET 10, for the same workloads and service lifetimes.

## Results

- RequestFlow allocates less in all 109 dispatch comparisons.
- RequestFlow is faster in 94 comparisons and MediatR in 8. The other 7 are inconclusive.
- All 8 MediatR wins have five pipeline stages or five event handlers, and 6 of them reuse one scope across operations.
- With a new scope per operation, RequestFlow is faster in 45 of 47 comparisons. MediatR is faster in the 5-stage suspending void request and the 5-handler suspending parallel event.
- The largest absolute allocation gap is an early return before the transaction stage. MediatR 12.5.0 still constructs that stage and its 4,096 B buffer, and RequestFlow does not. When authorization rejects in a new scope with 4,096 B buffers, RequestFlow allocates 1,024 B and takes 371 ns, while MediatR allocates 6,864 B and takes 1,185 ns.
- A long synchronous stream through stages shows the largest time ratio in the run. In a reused scope, 100 items through 5 stages take 3,186 ns in RequestFlow and 10,840 ns in MediatR. BenchmarkDotNet flagged MediatR's side of that row as multimodal.
- The handlers do small in-memory work, and every case ran on one laptop. These results do not predict application throughput or endpoint latency.
- Results describe MediatR 12.5.0 only, not later releases. RequestFlow is not affiliated with or endorsed by MediatR or its maintainers.

## Run

- Measured on 2026-10-15 from commit [`5d34f53`](https://github.com/illia1f/RequestFlow/commit/5d34f53d38e1fc6fb3cf66559fcef7f9a00f7ec5) (RequestFlow 1.0.0) with a clean working tree.
- MediatR 12.5.0, Microsoft.Extensions.DependencyInjection 10.0.9, and BenchmarkDotNet 0.15.8, pinned by package lock files.
- .NET SDK 10.0.301, .NET 10.0.9, X64 RyuJIT x86-64-v3, workstation GC, Release build.
- AMD Ryzen 7 5800H with 8 cores and 16 logical processors, all available to the run (affinity mask `0xFFFF`). Suspending and parallel event times depend on how many cores are available.
- Windows 11 23H2 build 22631.6199, AC power, High performance power plan.
- Each case ran in two launches of 60 measured iterations. The job allowed 15 to 50 warm-up and 60 to 100 measured iterations.
- The relative error target was 5% at 99.9% confidence. All 224 cases met it; the worst reached 2.81%.
- BenchmarkDotNet flagged 42 of the 224 cases, from both libraries, as multimodal. `run-manifest.json` lists them under `warnings`.
- BenchmarkDotNet set each case's invocation count so a pilot iteration took at least 500 ms. Measured iterations can run shorter: 37 of 448 launches have a median iteration below 500 ms, the lowest at 166 ms.
- The correctness probe passed for all 224 cases before timing. The run took 6 hours 48 minutes.
- [Raw BenchmarkDotNet reports and the run manifest](2026-10-15-mediatr-full/). The reports round values.
- Tables round the means and intervals from BenchmarkDotNet's JSON export, which is not published. [paired-results.csv](2026-10-15-mediatr-full/paired-results.csv) carries the exact values for every row, and [launch-results.csv](2026-10-15-mediatr-full/launch-results.csv) lists each launch's mean and median iteration time.

## What each row measures

- Both libraries run the same handler work. Handlers and pipeline stages are scoped on both sides.
- Dispatchers are transient on both sides. That is MediatR 12.5.0's default, and RequestFlow matches it with `WithTransientDispatcher()`.
- MediatR is resolved through `ISender` and `IPublisher`.
- Reused scope: the scope, dispatcher, handlers, and stages exist before timing. A row measures dispatch, stages, and handler.
- New scope: each operation creates a scope, resolves the dispatcher, activates handlers and stages, runs them, and disposes the scope.
- Cached handlers return a stored completed task. Newly completed handlers create a completed task.
- Synchronous void and event handlers return `Task.CompletedTask`, and synchronous streams yield each item without awaiting.
- Suspending handlers await `Task.Yield()`, so their times include thread-pool scheduling but no I/O.
- Stages in Task and void request rows return the next step's task, except in suspending rows, where each stage awaits it. Stream and short-circuit stages never await.
- Streams are fully consumed and events are awaited inside the operation. Event rows cover successful delivery, not failure or inheritance semantics.
- Allocated bytes include every managed allocation in the operation: handler, stages, DI, and async state.
- The [MediatR suite guide](../../../benchmarks/RequestFlow.Benchmarks.MediatR/README.md) lists native API differences and fixture costs.

## Reading the tables

- Times are nanoseconds per operation, shown as the mean ± the 99.9% confidence interval half-width. Bytes are managed bytes per operation.
- Each row pairs the two libraries on one parameter set, so the 224 cases form 112 rows. The 3 direct handler rows are references, which leaves 109 dispatch comparisons.
- Faster names the library with the lower mean when the two intervals do not overlap and both of its launch means are below both of the other library's. Otherwise the row is Inconclusive. `paired-results.csv` splits this into two columns: `Verdict` for the intervals and `LaunchesAgree` for the launches.
- Raw reports and CSVs use the benchmark names: Reused scope is `RetainedScope`, New scope per operation is `ScopePerOperation`, Newly completed is `Completion=Synchronous`, and Reaches handler is `Outcome=FullExecution`.
- Compare rows within one table. Workloads differ between tables.

## Task requests

The handler returns `Task<int>` through 0, 1, or 5 pipeline stages.

### Reused scope

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Cached, 0 stages | 52.37 ± 0.04 | 77.72 ± 0.16 | 0 | 104 | RequestFlow |
| Cached, 1 stage | 87.28 ± 0.12 | 145.40 ± 0.55 | 0 | 288 | RequestFlow |
| Cached, 5 stages | 248.58 ± 0.69 | 240.76 ± 3.02 | 0 | 736 | Inconclusive |
| Newly completed, 0 stages | 58.17 ± 0.84 | 82.18 ± 1.08 | 72 | 176 | RequestFlow |
| Newly completed, 1 stage | 90.75 ± 0.28 | 149.70 ± 0.96 | 72 | 360 | RequestFlow |
| Newly completed, 5 stages | 253.66 ± 0.78 | 247.16 ± 3.00 | 72 | 808 | Inconclusive |
| Suspending, 0 stages | 680.94 ± 2.23 | 694.23 ± 3.89 | 104 | 208 | Inconclusive |
| Suspending, 1 stage | 699.30 ± 4.82 | 730.67 ± 4.90 | 216 | 504 | RequestFlow |
| Suspending, 5 stages | 1,029.57 ± 3.00 | 995.21 ± 3.69 | 662 | 1,399 | MediatR |

### New scope per operation

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Cached, 0 stages | 223.92 ± 6.29 | 260.35 ± 1.26 | 560 | 664 | RequestFlow |
| Cached, 1 stage | 300.03 ± 1.12 | 509.51 ± 4.85 | 592 | 1,272 | RequestFlow |
| Cached, 5 stages | 776.80 ± 2.43 | 977.75 ± 14.16 | 1,168 | 2,768 | RequestFlow |
| Newly completed, 0 stages | 213.05 ± 0.81 | 270.48 ± 3.03 | 632 | 736 | RequestFlow |
| Newly completed, 1 stage | 300.13 ± 3.34 | 516.04 ± 6.32 | 664 | 1,344 | RequestFlow |
| Newly completed, 5 stages | 774.40 ± 3.33 | 959.40 ± 2.54 | 1,240 | 2,840 | RequestFlow |
| Suspending, 0 stages | 852.41 ± 5.41 | 926.19 ± 5.87 | 712 | 816 | RequestFlow |
| Suspending, 1 stage | 1,021.95 ± 2.86 | 1,233.84 ± 5.74 | 856 | 1,536 | RequestFlow |
| Suspending, 5 stages | 1,806.30 ± 5.34 | 2,050.63 ± 9.93 | 1,878 | 3,479 | RequestFlow |

## Void requests

The handler returns plain `Task` through 0, 1, or 5 pipeline stages.

### Reused scope

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 0 stages | 45.40 ± 0.16 | 103.83 ± 2.26 | 0 | 104 | RequestFlow |
| Synchronous, 1 stage | 107.75 ± 1.13 | 161.25 ± 0.69 | 0 | 288 | RequestFlow |
| Synchronous, 5 stages | 266.17 ± 0.61 | 248.60 ± 2.14 | 0 | 736 | MediatR |
| Suspending, 0 stages | 718.35 ± 1.65 | 720.10 ± 1.20 | 212 | 319 | Inconclusive |
| Suspending, 1 stage | 748.38 ± 2.76 | 768.72 ± 1.29 | 438 | 616 | RequestFlow |
| Suspending, 5 stages | 1,544.97 ± 4.21 | 1,103.12 ± 2.70 | 1,278 | 1,506 | MediatR |

### New scope per operation

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 0 stages | 220.76 ± 2.18 | 267.80 ± 1.50 | 488 | 592 | RequestFlow |
| Synchronous, 1 stage | 288.52 ± 1.22 | 520.82 ± 7.37 | 520 | 1,200 | RequestFlow |
| Synchronous, 5 stages | 764.28 ± 2.52 | 969.24 ± 6.57 | 1,096 | 2,696 | RequestFlow |
| Suspending, 0 stages | 893.94 ± 2.99 | 980.32 ± 3.12 | 824 | 928 | RequestFlow |
| Suspending, 1 stage | 1,130.95 ± 2.77 | 1,337.66 ± 3.53 | 1,079 | 1,648 | RequestFlow |
| Suspending, 5 stages | 2,279.98 ± 6.29 | 2,152.20 ± 10.78 | 2,501 | 3,589 | MediatR |

## Streams

Each operation fully consumes 1, 10, or 100 items through 0, 1, or 5 forwarding stages.

### Reused scope

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| 1 item, synchronous, 0 stages | 164.38 ± 0.86 | 208.99 ± 2.64 | 376 | 528 | RequestFlow |
| 1 item, synchronous, 1 stage | 216.81 ± 1.54 | 381.10 ± 5.16 | 376 | 960 | RequestFlow |
| 1 item, synchronous, 5 stages | 359.81 ± 0.60 | 670.91 ± 3.27 | 376 | 2,400 | RequestFlow |
| 1 item, suspending, 0 stages | 870.31 ± 6.04 | 885.35 ± 5.00 | 696 | 848 | Inconclusive |
| 1 item, suspending, 1 stage | 876.62 ± 5.24 | 1,258.06 ± 11.82 | 696 | 1,360 | RequestFlow |
| 1 item, suspending, 5 stages | 1,070.23 ± 5.67 | 1,844.14 ± 5.36 | 696 | 3,117 | RequestFlow |
| 10 items, synchronous, 0 stages | 422.10 ± 1.59 | 494.72 ± 1.92 | 448 | 600 | RequestFlow |
| 10 items, synchronous, 1 stage | 461.62 ± 4.51 | 770.44 ± 1.80 | 448 | 1,032 | RequestFlow |
| 10 items, synchronous, 5 stages | 637.80 ± 3.17 | 1,587.40 ± 3.93 | 448 | 2,472 | RequestFlow |
| 10 items, suspending, 0 stages | 6,458.47 ± 67.15 | 6,930.50 ± 34.74 | 696 | 848 | RequestFlow |
| 10 items, suspending, 1 stage | 6,497.37 ± 24.02 | 7,791.47 ± 63.54 | 696 | 1,360 | RequestFlow |
| 10 items, suspending, 5 stages | 6,855.65 ± 45.94 | 9,579.61 ± 77.61 | 696 | 3,120 | RequestFlow |
| 100 items, synchronous, 0 stages | 2,999.33 ± 6.86 | 3,134.32 ± 33.68 | 448 | 600 | RequestFlow |
| 100 items, synchronous, 1 stage | 3,030.46 ± 10.43 | 4,631.05 ± 12.65 | 448 | 1,032 | RequestFlow |
| 100 items, synchronous, 5 stages | 3,186.39 ± 5.16 | 10,840.05 ± 113.29 | 448 | 2,472 | RequestFlow |
| 100 items, suspending, 0 stages | 73,998.13 ± 819.09 | 74,402.27 ± 1,090.41 | 760 | 912 | Inconclusive |
| 100 items, suspending, 1 stage | 70,628.12 ± 965.17 | 79,915.11 ± 251.17 | 760 | 1,424 | RequestFlow |
| 100 items, suspending, 5 stages | 68,691.10 ± 494.26 | 80,843.31 ± 485.08 | 760 | 3,184 | RequestFlow |

### New scope per operation

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| 1 item, synchronous, 0 stages | 369.17 ± 4.40 | 428.21 ± 1.03 | 864 | 1,016 | RequestFlow |
| 1 item, synchronous, 1 stage | 452.79 ± 3.14 | 781.61 ± 3.27 | 896 | 1,872 | RequestFlow |
| 1 item, synchronous, 5 stages | 956.27 ± 6.67 | 1,551.76 ± 8.49 | 1,472 | 4,360 | RequestFlow |
| 1 item, suspending, 0 stages | 1,176.17 ± 4.52 | 1,249.62 ± 6.59 | 1,304 | 1,456 | RequestFlow |
| 1 item, suspending, 1 stage | 1,322.47 ± 9.96 | 1,829.62 ± 4.59 | 1,336 | 2,392 | RequestFlow |
| 1 item, suspending, 5 stages | 1,851.09 ± 11.50 | 2,937.01 ± 9.05 | 1,912 | 5,195 | RequestFlow |
| 10 items, synchronous, 0 stages | 655.66 ± 4.25 | 765.12 ± 6.12 | 1,008 | 1,160 | RequestFlow |
| 10 items, synchronous, 1 stage | 751.90 ± 3.62 | 1,216.12 ± 5.38 | 1,040 | 2,016 | RequestFlow |
| 10 items, synchronous, 5 stages | 1,257.14 ± 18.09 | 2,496.63 ± 11.95 | 1,616 | 4,504 | RequestFlow |
| 10 items, suspending, 0 stages | 6,877.93 ± 20.30 | 7,131.62 ± 17.14 | 1,304 | 1,456 | RequestFlow |
| 10 items, suspending, 1 stage | 7,335.32 ± 32.89 | 8,965.41 ± 34.99 | 1,336 | 2,392 | RequestFlow |
| 10 items, suspending, 5 stages | 8,130.66 ± 36.93 | 10,555.43 ± 66.28 | 1,912 | 5,200 | RequestFlow |
| 100 items, synchronous, 0 stages | 3,352.90 ± 20.72 | 3,424.64 ± 4.48 | 1,008 | 1,160 | RequestFlow |
| 100 items, synchronous, 1 stage | 3,395.15 ± 27.90 | 5,114.34 ± 29.35 | 1,040 | 2,016 | RequestFlow |
| 100 items, synchronous, 5 stages | 3,968.01 ± 8.67 | 10,820.20 ± 69.23 | 1,616 | 4,504 | RequestFlow |
| 100 items, suspending, 0 stages | 70,240.07 ± 313.13 | 71,706.37 ± 378.46 | 1,368 | 1,520 | RequestFlow |
| 100 items, suspending, 1 stage | 68,194.23 ± 311.49 | 78,037.61 ± 653.45 | 1,400 | 2,456 | RequestFlow |
| 100 items, suspending, 5 stages | 70,887.11 ± 372.90 | 83,487.66 ± 201.61 | 1,976 | 5,264 | RequestFlow |

## Sequential events

Publishing awaits 1 or 5 handlers in order. MediatR uses its default publisher.

### Reused scope

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 1 handler | 62.66 ± 0.28 | 99.20 ± 0.50 | 0 | 232 | RequestFlow |
| Synchronous, 5 handlers | 253.65 ± 0.41 | 196.36 ± 1.61 | 0 | 712 | MediatR |
| Suspending, 1 handler | 699.15 ± 1.62 | 716.73 ± 5.17 | 252 | 471 | Inconclusive |
| Suspending, 5 handlers | 2,570.69 ± 5.27 | 2,619.50 ± 4.75 | 672 | 1,368 | RequestFlow |

### New scope per operation

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 1 handler | 216.16 ± 1.49 | 336.06 ± 3.77 | 488 | 752 | RequestFlow |
| Synchronous, 5 handlers | 716.29 ± 3.03 | 824.07 ± 10.23 | 1,064 | 1,840 | RequestFlow |
| Suspending, 1 handler | 919.64 ± 10.62 | 1,055.74 ± 3.36 | 864 | 1,112 | RequestFlow |
| Suspending, 5 handlers | 2,949.39 ± 11.18 | 3,380.97 ± 9.09 | 1,856 | 2,616 | RequestFlow |

## Parallel events

Publishing awaits 1 or 5 handlers concurrently. MediatR uses its `TaskWhenAllPublisher`.

### Reused scope

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 1 handler | 64.26 ± 0.79 | 135.64 ± 3.61 | 0 | 504 | RequestFlow |
| Synchronous, 5 handlers | 292.88 ± 1.26 | 258.23 ± 1.64 | 64 | 1,088 | MediatR |
| Suspending, 1 handler | 691.62 ± 2.35 | 723.67 ± 1.12 | 253 | 608 | RequestFlow |
| Suspending, 5 handlers | 2,391.93 ± 7.50 | 1,972.91 ± 10.40 | 695 | 1,608 | MediatR |

### New scope per operation

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Synchronous, 1 handler | 210.11 ± 1.28 | 350.31 ± 2.96 | 488 | 1,024 | RequestFlow |
| Synchronous, 5 handlers | 737.25 ± 3.22 | 856.80 ± 5.20 | 1,128 | 2,216 | RequestFlow |
| Suspending, 1 handler | 913.80 ± 3.06 | 1,001.97 ± 6.13 | 864 | 1,248 | RequestFlow |
| Suspending, 5 handlers | 2,926.61 ± 19.85 | 2,396.65 ± 6.05 | 1,860 | 2,823 | MediatR |

## Short-circuit pipeline

Authorization, cache, and transaction stages run before the handler. Authorization can reject, the cache can return a stored result, or the request reaches the handler. The transaction's change buffer and the handler's command buffer hold 0 or 4,096 bytes in a new scope, and 0 bytes in a reused scope.

RequestFlow constructs a stage and its dependencies when the dispatch reaches it. MediatR 12.5.0 constructs every pipeline behavior and its dependencies, including the ones an early return skips.

### Reused scope

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Authorization rejects | 43.21 ± 0.05 | 124.99 ± 1.43 | 0 | 512 | RequestFlow |
| Cache hit | 84.88 ± 0.29 | 129.10 ± 0.75 | 0 | 512 | RequestFlow |
| Reaches handler | 177.38 ± 2.83 | 188.79 ± 0.98 | 0 | 512 | RequestFlow |

### New scope per operation

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Authorization rejects, 0 B buffers | 380.20 ± 3.16 | 1,043.40 ± 2.32 | 1,024 | 2,744 | RequestFlow |
| Authorization rejects, 4,096 B buffers | 371.34 ± 3.50 | 1,184.80 ± 10.70 | 1,024 | 6,864 | RequestFlow |
| Cache hit, 0 B buffers | 542.69 ± 2.28 | 1,009.64 ± 3.97 | 1,216 | 2,744 | RequestFlow |
| Cache hit, 4,096 B buffers | 546.03 ± 1.40 | 1,168.80 ± 7.87 | 1,216 | 6,864 | RequestFlow |
| Reaches handler, 0 B buffers | 1,254.79 ± 2.93 | 1,376.32 ± 4.65 | 2,560 | 3,120 | RequestFlow |
| Reaches handler, 4,096 B buffers | 1,535.72 ± 5.17 | 1,662.74 ± 5.03 | 10,800 | 11,360 | RequestFlow |

## Catalog size

One request type is sent within a catalog of 4, 16, or 64 request handlers, in a reused scope.

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| 4 handlers, cached | 53.74 ± 0.35 | 75.68 ± 0.68 | 0 | 104 | RequestFlow |
| 4 handlers, newly completed | 55.72 ± 0.41 | 80.05 ± 0.39 | 72 | 176 | RequestFlow |
| 4 handlers, suspending | 688.02 ± 1.68 | 705.53 ± 0.93 | 104 | 208 | RequestFlow |
| 16 handlers, cached | 58.18 ± 0.19 | 78.17 ± 0.13 | 0 | 104 | RequestFlow |
| 16 handlers, newly completed | 58.63 ± 0.11 | 82.06 ± 1.16 | 72 | 176 | RequestFlow |
| 16 handlers, suspending | 679.57 ± 1.92 | 707.36 ± 1.76 | 104 | 208 | RequestFlow |
| 64 handlers, cached | 57.65 ± 0.10 | 77.53 ± 0.99 | 0 | 104 | RequestFlow |
| 64 handlers, newly completed | 61.43 ± 0.26 | 81.27 ± 0.38 | 72 | 176 | RequestFlow |
| 64 handlers, suspending | 690.35 ± 2.24 | 701.26 ± 1.87 | 104 | 208 | RequestFlow |

## Mixed traffic

Every request type in a 4, 16, or 64 handler catalog is sent in one fixed shuffled order, in a reused scope. Times and bytes are per send. Each batch of 64 sends also allocates one `Task<int>` for its checksum on both sides, which adds about 1 B per send, or 3 B in suspending rows.

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| 4 handlers, cached | 61.20 ± 0.89 | 122.08 ± 2.31 | 1 | 169 | RequestFlow |
| 4 handlers, newly completed | 61.68 ± 0.64 | 124.14 ± 2.33 | 73 | 241 | RequestFlow |
| 4 handlers, suspending | 505.89 ± 1.98 | 547.30 ± 0.93 | 107 | 275 | RequestFlow |
| 16 handlers, cached | 76.59 ± 0.30 | 164.47 ± 0.76 | 1 | 165 | RequestFlow |
| 16 handlers, newly completed | 84.44 ± 0.27 | 168.20 ± 0.66 | 73 | 237 | RequestFlow |
| 16 handlers, suspending | 518.82 ± 0.80 | 578.70 ± 1.19 | 107 | 271 | RequestFlow |
| 64 handlers, cached | 103.16 ± 0.27 | 199.71 ± 0.98 | 1 | 168 | RequestFlow |
| 64 handlers, newly completed | 109.81 ± 0.33 | 204.20 ± 1.09 | 73 | 240 | RequestFlow |
| 64 handlers, suspending | 546.64 ± 2.08 | 640.03 ± 1.97 | 107 | 274 | RequestFlow |

## Direct handler reference

Each library's Task handler is called directly, without dispatch or DI. These rows show the cost of the handler work itself, so they rank neither dispatcher.

| Case | RequestFlow (ns) | MediatR (ns) | RequestFlow (B) | MediatR (B) | Faster |
| --- | ---: | ---: | ---: | ---: | --- |
| Cached | 1.608 ± 0.004 | 1.589 ± 0.003 | 0 | 0 | Reference |
| Newly completed | 4.91 ± 0.06 | 4.81 ± 0.04 | 72 | 72 | Reference |
| Suspending | 696.64 ± 2.79 | 709.41 ± 2.10 | 104 | 104 | Reference |

## Reproducing

Run from the repository root at the measured commit, on an otherwise idle machine:

```powershell
dotnet restore benchmarks/RequestFlow.Benchmarks.MediatR -p:RestoreLockedMode=true
dotnet build benchmarks/RequestFlow.Benchmarks.MediatR -c Release --no-restore
dotnet run --project benchmarks/RequestFlow.Benchmarks.MediatR -c Release -f net10.0 --no-build --no-restore -- --runtimes net10.0 --filter '*' --artifacts benchmarks/artifacts/YYYY-MM-DD-mediatr-full
```

- The run checks correctness before timing and writes `run-manifest.json` to the artifact folder.
- The [benchmark guide](../../../benchmarks/README.md) covers quick runs, filters, and the publication checks.
