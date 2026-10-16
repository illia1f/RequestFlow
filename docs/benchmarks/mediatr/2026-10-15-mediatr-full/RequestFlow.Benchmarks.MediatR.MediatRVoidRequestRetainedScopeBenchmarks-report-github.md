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
| Method      | Completion  | StageCount | Mean        | Error    | StdDev    | Median      | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Allocated | Alloc Ratio |
|------------ |------------ |----------- |------------:|---------:|----------:|------------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| RequestFlow | Synchronous | 0          |    45.40 ns | 0.165 ns |  0.510 ns |    45.45 ns |  1.00 |    0.02 |                    - |                - |      - |         - |          NA |
| MediatR     | Synchronous | 0          |   103.83 ns | 2.263 ns |  7.218 ns |    99.24 ns |  2.29 |    0.16 |                    - |                - | 0.0124 |     104 B |          NA |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Synchronous | 1          |   107.75 ns | 1.128 ns |  3.581 ns |   105.03 ns |  1.00 |    0.05 |                    - |                - |      - |         - |          NA |
| MediatR     | Synchronous | 1          |   161.25 ns | 0.694 ns |  2.243 ns |   161.46 ns |  1.50 |    0.05 |                    - |                - | 0.0343 |     288 B |          NA |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Synchronous | 5          |   266.17 ns | 0.615 ns |  1.897 ns |   265.77 ns |  1.00 |    0.01 |                    - |                - |      - |         - |          NA |
| MediatR     | Synchronous | 5          |   248.60 ns | 2.141 ns |  6.859 ns |   248.14 ns |  0.93 |    0.03 |                    - |                - | 0.0877 |     736 B |          NA |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Suspending  | 0          |   718.35 ns | 1.653 ns |  5.367 ns |   718.69 ns |  1.00 |    0.01 |               1.0179 |                - | 0.0248 |     212 B |        1.00 |
| MediatR     | Suspending  | 0          |   720.10 ns | 1.203 ns |  3.854 ns |   721.00 ns |  1.00 |    0.01 |               1.0132 |                - | 0.0372 |     319 B |        1.50 |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Suspending  | 1          |   748.38 ns | 2.759 ns |  8.839 ns |   748.91 ns |  1.00 |    0.02 |               1.0745 |                - | 0.0515 |     438 B |        1.00 |
| MediatR     | Suspending  | 1          |   768.72 ns | 1.293 ns |  4.105 ns |   768.68 ns |  1.03 |    0.01 |               1.0290 |                - | 0.0734 |     616 B |        1.41 |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Suspending  | 5          | 1,544.97 ns | 4.208 ns | 13.236 ns | 1,545.95 ns |  1.00 |    0.01 |               1.7020 |                - | 0.1526 |    1278 B |        1.00 |
| MediatR     | Suspending  | 5          | 1,103.12 ns | 2.699 ns |  8.328 ns | 1,105.06 ns |  0.71 |    0.01 |               1.1915 |                - | 0.1793 |    1506 B |        1.18 |
