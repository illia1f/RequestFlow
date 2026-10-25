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
| Method      | Completion  | HandlerCount | Mean        | Error    | StdDev    | Median      | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------ |------------ |------------- |------------:|---------:|----------:|------------:|------:|--------:|-------:|---------------------:|-----------------:|----------:|------------:|
| RequestFlow | Synchronous | 1            |    64.67 ns | 0.948 ns |  2.968 ns |    67.11 ns |  1.00 |    0.06 |      - |                    - |                - |         - |          NA |
| DispatchR   | Synchronous | 1            |    58.24 ns | 0.874 ns |  2.774 ns |    60.75 ns |  0.90 |    0.06 |      - |                    - |                - |         - |          NA |
|             |             |              |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Synchronous | 5            |   253.08 ns | 0.238 ns |  0.751 ns |   253.03 ns |  1.00 |    0.00 |      - |                    - |                - |         - |          NA |
| DispatchR   | Synchronous | 5            |    83.08 ns | 0.579 ns |  1.813 ns |    84.49 ns |  0.33 |    0.01 |      - |                    - |                - |         - |          NA |
|             |             |              |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 1            |   713.94 ns | 3.640 ns | 11.714 ns |   712.59 ns |  1.00 |    0.02 | 0.0296 |               1.0232 |                - |     251 B |        1.00 |
| DispatchR   | Suspending  | 1            |   702.09 ns | 2.240 ns |  7.240 ns |   701.68 ns |  0.98 |    0.02 | 0.0277 |               1.0210 |                - |     238 B |        0.95 |
|             |             |              |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 5            | 2,507.02 ns | 8.115 ns | 26.346 ns | 2,508.06 ns |  1.00 |    0.01 | 0.0801 |               5.0262 |                - |     672 B |        1.00 |
| DispatchR   | Suspending  | 5            | 2,748.51 ns | 5.624 ns | 18.016 ns | 2,746.16 ns |  1.10 |    0.01 | 0.0763 |               5.0654 |                - |     656 B |        0.98 |
