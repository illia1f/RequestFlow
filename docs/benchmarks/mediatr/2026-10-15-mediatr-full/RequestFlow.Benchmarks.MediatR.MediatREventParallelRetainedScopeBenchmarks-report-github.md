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
| Method      | Completion  | HandlerCount | Mean        | Error     | StdDev    | Median      | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Allocated | Alloc Ratio |
|------------ |------------ |------------- |------------:|----------:|----------:|------------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| RequestFlow | Synchronous | 1            |    64.26 ns |  0.789 ns |  2.517 ns |    66.27 ns |  1.00 |    0.06 |                    - |                - |      - |         - |          NA |
| MediatR     | Synchronous | 1            |   135.64 ns |  3.611 ns | 11.567 ns |   128.60 ns |  2.11 |    0.20 |                    - |                - | 0.0601 |     504 B |          NA |
|             |             |              |             |           |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Synchronous | 5            |   292.88 ns |  1.260 ns |  4.001 ns |   292.81 ns |  1.00 |    0.02 |                    - |                - | 0.0076 |      64 B |        1.00 |
| MediatR     | Synchronous | 5            |   258.23 ns |  1.641 ns |  5.329 ns |   257.67 ns |  0.88 |    0.02 |                    - |                - | 0.1297 |    1088 B |       17.00 |
|             |             |              |             |           |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Suspending  | 1            |   691.62 ns |  2.348 ns |  7.453 ns |   691.53 ns |  1.00 |    0.02 |               1.0228 |                - | 0.0296 |     253 B |        1.00 |
| MediatR     | Suspending  | 1            |   723.67 ns |  1.115 ns |  3.525 ns |   723.70 ns |  1.05 |    0.01 |               1.0000 |                - | 0.0725 |     608 B |        2.40 |
|             |             |              |             |           |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Suspending  | 5            | 2,391.93 ns |  7.500 ns | 24.349 ns | 2,388.10 ns |  1.00 |    0.01 |               5.1650 |                - | 0.0801 |     695 B |        1.00 |
| MediatR     | Suspending  | 5            | 1,972.91 ns | 10.396 ns | 33.456 ns | 1,970.47 ns |  0.82 |    0.02 |               5.0000 |                - | 0.1907 |    1608 B |        2.31 |
