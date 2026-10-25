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
| Method                   | Completion  | Mean       | Error     | StdDev     | Median     | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------------------- |------------ |-----------:|----------:|-----------:|-----------:|------:|--------:|-------:|---------------------:|-----------------:|----------:|------------:|
| RequestFlowDirectHandler | Cached      |   1.589 ns | 0.0019 ns |  0.0059 ns |   1.589 ns |  1.00 |    0.01 |      - |                    - |                - |         - |          NA |
| DispatchRDirectHandler   | Cached      |   1.578 ns | 0.0021 ns |  0.0066 ns |   1.577 ns |  0.99 |    0.01 |      - |                    - |                - |         - |          NA |
|                          |             |            |           |            |            |       |         |        |                      |                  |           |             |
| RequestFlowDirectHandler | Synchronous |   4.606 ns | 0.0147 ns |  0.0442 ns |   4.612 ns |  1.00 |    0.01 | 0.0086 |                    - |                - |      72 B |        1.00 |
| DispatchRDirectHandler   | Synchronous |   4.748 ns | 0.0645 ns |  0.2083 ns |   4.646 ns |  1.03 |    0.05 | 0.0086 |                    - |                - |      72 B |        1.00 |
|                          |             |            |           |            |            |       |         |        |                      |                  |           |             |
| RequestFlowDirectHandler | Suspending  | 691.924 ns | 3.2820 ns | 10.6553 ns | 695.960 ns |  1.00 |    0.02 | 0.0124 |               1.0000 |           0.0000 |     104 B |        1.00 |
| DispatchRDirectHandler   | Suspending  | 695.938 ns | 0.7550 ns |  2.4511 ns | 696.115 ns |  1.01 |    0.02 | 0.0124 |               1.0000 |                - |     104 B |        1.00 |
