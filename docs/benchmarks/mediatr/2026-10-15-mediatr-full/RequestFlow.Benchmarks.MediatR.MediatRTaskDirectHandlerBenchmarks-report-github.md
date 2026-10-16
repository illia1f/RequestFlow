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
| Method                   | Completion  | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Allocated | Alloc Ratio |
|------------------------- |------------ |-----------:|----------:|----------:|-----------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| RequestFlowDirectHandler | Cached      |   1.608 ns | 0.0043 ns | 0.0134 ns |   1.605 ns |  1.00 |    0.01 |                    - |                - |      - |         - |          NA |
| MediatRDirectHandler     | Cached      |   1.589 ns | 0.0026 ns | 0.0081 ns |   1.587 ns |  0.99 |    0.01 |                    - |                - |      - |         - |          NA |
|                          |             |            |           |           |            |       |         |                      |                  |        |           |             |
| RequestFlowDirectHandler | Synchronous |   4.907 ns | 0.0650 ns | 0.2110 ns |   4.790 ns |  1.00 |    0.06 |                    - |                - | 0.0086 |      72 B |        1.00 |
| MediatRDirectHandler     | Synchronous |   4.810 ns | 0.0437 ns | 0.1369 ns |   4.882 ns |  0.98 |    0.05 |                    - |                - | 0.0086 |      72 B |        1.00 |
|                          |             |            |           |           |            |       |         |                      |                  |        |           |             |
| RequestFlowDirectHandler | Suspending  | 696.644 ns | 2.7918 ns | 8.9840 ns | 697.414 ns |  1.00 |    0.02 |               1.0000 |                - | 0.0124 |     104 B |        1.00 |
| MediatRDirectHandler     | Suspending  | 709.408 ns | 2.0973 ns | 6.8092 ns | 710.056 ns |  1.02 |    0.02 |               1.0000 |                - | 0.0124 |     104 B |        1.00 |
