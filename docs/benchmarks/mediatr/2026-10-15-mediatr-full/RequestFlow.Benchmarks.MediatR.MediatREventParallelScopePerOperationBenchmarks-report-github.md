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
| Method      | Completion  | HandlerCount | Mean       | Error    | StdDev   | Median     | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------ |------------ |------------- |-----------:|---------:|---------:|-----------:|------:|--------:|---------------------:|-----------------:|-------:|-------:|----------:|------------:|
| RequestFlow | Synchronous | 1            |   210.1 ns |  1.28 ns |  3.94 ns |   211.2 ns |  1.00 |    0.03 |                    - |                - | 0.0582 |      - |     488 B |        1.00 |
| MediatR     | Synchronous | 1            |   350.3 ns |  2.96 ns |  9.22 ns |   353.5 ns |  1.67 |    0.05 |                    - |                - | 0.1221 |      - |    1024 B |        2.10 |
|             |             |              |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Synchronous | 5            |   737.2 ns |  3.22 ns | 10.08 ns |   739.0 ns |  1.00 |    0.02 |                    - |                - | 0.1345 |      - |    1128 B |        1.00 |
| MediatR     | Synchronous | 5            |   856.8 ns |  5.20 ns | 15.82 ns |   862.1 ns |  1.16 |    0.03 |                    - |                - | 0.2642 | 0.0010 |    2216 B |        1.96 |
|             |             |              |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Suspending  | 1            |   913.8 ns |  3.06 ns |  9.90 ns |   915.3 ns |  1.00 |    0.02 |               1.0311 |                - | 0.1030 |      - |     864 B |        1.00 |
| MediatR     | Suspending  | 1            | 1,002.0 ns |  6.13 ns | 19.20 ns |   995.7 ns |  1.10 |    0.02 |               1.0130 |                - | 0.1488 |      - |    1248 B |        1.44 |
|             |             |              |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Suspending  | 5            | 2,926.6 ns | 19.85 ns | 64.15 ns | 2,924.9 ns |  1.00 |    0.03 |               5.1343 |                - | 0.2213 |      - |    1860 B |        1.00 |
| MediatR     | Suspending  | 5            | 2,396.7 ns |  6.05 ns | 19.11 ns | 2,394.5 ns |  0.82 |    0.02 |               5.0504 |                - | 0.3357 |      - |    2823 B |        1.52 |
