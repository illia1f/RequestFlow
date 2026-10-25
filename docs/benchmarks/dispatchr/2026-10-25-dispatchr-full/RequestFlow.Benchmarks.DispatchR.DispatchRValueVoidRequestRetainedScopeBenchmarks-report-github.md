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
| RequestFlow | Synchronous | 0          |    53.57 ns | 0.080 ns |  0.251 ns |    53.58 ns |  1.00 |    0.01 |      - |                    - |                - |         - |          NA |
| DispatchR   | Synchronous | 0          |    42.35 ns | 0.382 ns |  1.206 ns |    43.36 ns |  0.79 |    0.02 |      - |                    - |                - |         - |          NA |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Synchronous | 1          |   106.79 ns | 0.128 ns |  0.403 ns |   106.91 ns |  1.00 |    0.01 |      - |                    - |                - |         - |          NA |
| DispatchR   | Synchronous | 1          |    47.44 ns | 0.193 ns |  0.608 ns |    47.87 ns |  0.44 |    0.01 |      - |                    - |                - |         - |          NA |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Synchronous | 5          |   291.12 ns | 1.018 ns |  3.217 ns |   291.34 ns |  1.00 |    0.02 |      - |                    - |                - |         - |          NA |
| DispatchR   | Synchronous | 5          |    81.37 ns | 2.364 ns |  7.367 ns |    74.74 ns |  0.28 |    0.03 |      - |                    - |                - |         - |          NA |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 0          |   702.07 ns | 1.364 ns |  4.428 ns |   702.03 ns |  1.00 |    0.01 | 0.0124 |               1.0022 |                - |     104 B |        1.00 |
| DispatchR   | Suspending  | 0          |   701.29 ns | 1.720 ns |  5.585 ns |   701.63 ns |  1.00 |    0.01 | 0.0134 |               1.0020 |                - |     112 B |        1.08 |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 1          |   700.28 ns | 1.315 ns |  4.250 ns |   699.63 ns |  1.00 |    0.01 | 0.0257 |               1.0160 |                - |     223 B |        1.00 |
| DispatchR   | Suspending  | 1          |   712.38 ns | 0.879 ns |  2.842 ns |   712.44 ns |  1.02 |    0.01 | 0.0277 |               1.0184 |                - |     238 B |        1.07 |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 5          | 1,151.35 ns | 3.674 ns | 11.769 ns | 1,149.85 ns |  1.00 |    0.01 | 0.0820 |               1.2652 |                - |     695 B |        1.00 |
| DispatchR   | Suspending  | 5          |   967.00 ns | 3.591 ns | 11.347 ns |   966.17 ns |  0.84 |    0.01 | 0.0877 |               1.2687 |                - |     734 B |        1.06 |
