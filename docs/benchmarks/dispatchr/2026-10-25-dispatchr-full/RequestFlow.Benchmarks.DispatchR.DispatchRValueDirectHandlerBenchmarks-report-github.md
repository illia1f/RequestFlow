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
| Method                   | Completion  | Mean       | Error     | StdDev    | Ratio | Gen0   | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------------------- |------------ |-----------:|----------:|----------:|------:|-------:|---------------------:|-----------------:|----------:|------------:|
| RequestFlowDirectHandler | Synchronous |   1.994 ns | 0.0047 ns | 0.0148 ns |  1.00 |      - |                    - |                - |         - |          NA |
| DispatchRDirectHandler   | Synchronous |   2.000 ns | 0.0028 ns | 0.0087 ns |  1.00 |      - |                    - |                - |         - |          NA |
|                          |             |            |           |           |       |        |                      |                  |           |             |
| RequestFlowDirectHandler | Suspending  | 702.896 ns | 1.5972 ns | 5.1854 ns |  1.00 | 0.0134 |               1.0037 |                - |     112 B |        1.00 |
| DispatchRDirectHandler   | Suspending  | 700.543 ns | 2.5076 ns | 8.1411 ns |  1.00 | 0.0134 |               1.0039 |                - |     112 B |        1.00 |
