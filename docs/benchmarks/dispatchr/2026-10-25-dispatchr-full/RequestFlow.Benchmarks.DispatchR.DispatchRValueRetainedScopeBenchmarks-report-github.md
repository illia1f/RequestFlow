```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
AMD Ryzen 7 5800H with Radeon Graphics 3.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.301
  [Host]    : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Job=.NET 10.0  MaxRelativeError=0.05  MinIterationTime=500ms  
Runtime=.NET 10.0  Toolchain=net10.0  LaunchCount=2  
MaxIterationCount=100  MaxWarmupIterationCount=50  MinIterationCount=60  
MinWarmupIterationCount=15  

```
| Method      | Completion  | StageCount | Mean        | Error    | StdDev    | Median      | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------ |------------ |----------- |------------:|---------:|----------:|------------:|------:|--------:|-------:|---------------------:|-----------------:|----------:|------------:|
| RequestFlow | Synchronous | 0          |    58.25 ns | 0.617 ns |  1.951 ns |    58.18 ns |  1.00 |    0.05 |      - |                    - |                - |         - |          NA |
| DispatchR   | Synchronous | 0          |    41.10 ns | 0.474 ns |  1.511 ns |    39.87 ns |  0.71 |    0.03 |      - |                    - |                - |         - |          NA |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Synchronous | 1          |    91.93 ns | 0.074 ns |  0.237 ns |    91.92 ns |  1.00 |    0.00 |      - |                    - |                - |         - |          NA |
| DispatchR   | Synchronous | 1          |    55.36 ns | 0.419 ns |  1.293 ns |    56.40 ns |  0.60 |    0.01 |      - |                    - |                - |         - |          NA |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Synchronous | 5          |   273.25 ns | 0.477 ns |  1.506 ns |   272.67 ns |  1.00 |    0.01 |      - |                    - |                - |         - |          NA |
| DispatchR   | Synchronous | 5          |    82.70 ns | 2.324 ns |  7.378 ns |    89.41 ns |  0.30 |    0.03 |      - |                    - |                - |         - |          NA |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 0          |   681.41 ns | 3.355 ns | 10.748 ns |   678.46 ns |  1.00 |    0.02 | 0.0134 |               1.0015 |                - |     112 B |        1.00 |
| DispatchR   | Suspending  | 0          |   702.74 ns | 1.436 ns |  4.661 ns |   702.99 ns |  1.03 |    0.02 | 0.0134 |               1.0018 |                - |     112 B |        1.00 |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 1          |   703.42 ns | 1.656 ns |  5.352 ns |   704.59 ns |  1.00 |    0.01 | 0.0277 |               1.0156 |                - |     239 B |        1.00 |
| DispatchR   | Suspending  | 1          |   688.75 ns | 2.662 ns |  8.644 ns |   688.66 ns |  0.98 |    0.01 | 0.0277 |               1.0186 |                - |     238 B |        1.00 |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 5          | 1,145.58 ns | 4.470 ns | 14.448 ns | 1,145.46 ns |  1.00 |    0.02 | 0.0877 |               1.2998 |                - |     736 B |        1.00 |
| DispatchR   | Suspending  | 5          |   952.04 ns | 3.678 ns | 11.783 ns |   951.92 ns |  0.83 |    0.01 | 0.0877 |               1.2499 |                - |     737 B |        1.00 |
