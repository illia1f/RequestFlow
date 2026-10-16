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
| RequestFlow | Cached      | 0          |    52.37 ns | 0.040 ns |  0.124 ns |    52.34 ns |  1.00 |    0.00 |                    - |                - |      - |         - |          NA |
| MediatR     | Cached      | 0          |    77.72 ns | 0.155 ns |  0.497 ns |    77.69 ns |  1.48 |    0.01 |                    - |                - | 0.0124 |     104 B |          NA |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Cached      | 1          |    87.28 ns | 0.120 ns |  0.371 ns |    87.40 ns |  1.00 |    0.01 |                    - |                - |      - |         - |          NA |
| MediatR     | Cached      | 1          |   145.40 ns | 0.547 ns |  1.730 ns |   144.92 ns |  1.67 |    0.02 |                    - |                - | 0.0343 |     288 B |          NA |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Cached      | 5          |   248.58 ns | 0.686 ns |  2.129 ns |   248.95 ns |  1.00 |    0.01 |                    - |                - |      - |         - |          NA |
| MediatR     | Cached      | 5          |   240.76 ns | 3.017 ns |  9.664 ns |   237.90 ns |  0.97 |    0.04 |                    - |                - | 0.0877 |     736 B |          NA |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Synchronous | 0          |    58.17 ns | 0.838 ns |  2.683 ns |    56.65 ns |  1.00 |    0.06 |                    - |                - | 0.0086 |      72 B |        1.00 |
| MediatR     | Synchronous | 0          |    82.18 ns | 1.080 ns |  3.429 ns |    81.20 ns |  1.42 |    0.09 |                    - |                - | 0.0210 |     176 B |        2.44 |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Synchronous | 1          |    90.75 ns | 0.277 ns |  0.850 ns |    90.48 ns |  1.00 |    0.01 |                    - |                - | 0.0086 |      72 B |        1.00 |
| MediatR     | Synchronous | 1          |   149.70 ns | 0.956 ns |  3.007 ns |   149.96 ns |  1.65 |    0.04 |                    - |                - | 0.0429 |     360 B |        5.00 |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Synchronous | 5          |   253.66 ns | 0.782 ns |  2.528 ns |   254.31 ns |  1.00 |    0.01 |                    - |                - | 0.0086 |      72 B |        1.00 |
| MediatR     | Synchronous | 5          |   247.16 ns | 3.002 ns |  9.748 ns |   245.15 ns |  0.97 |    0.04 |                    - |                - | 0.0963 |     808 B |       11.22 |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Suspending  | 0          |   680.94 ns | 2.230 ns |  7.146 ns |   680.95 ns |  1.00 |    0.01 |               1.0000 |                - | 0.0124 |     104 B |        1.00 |
| MediatR     | Suspending  | 0          |   694.23 ns | 3.893 ns | 12.583 ns |   692.65 ns |  1.02 |    0.02 |               1.0000 |                - | 0.0248 |     208 B |        2.00 |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Suspending  | 1          |   699.30 ns | 4.823 ns | 15.380 ns |   693.43 ns |  1.00 |    0.03 |               1.0181 |                - | 0.0257 |     216 B |        1.00 |
| MediatR     | Suspending  | 1          |   730.67 ns | 4.903 ns | 15.495 ns |   731.56 ns |  1.05 |    0.03 |               1.0102 |                - | 0.0601 |     504 B |        2.33 |
|             |             |            |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Suspending  | 5          | 1,029.57 ns | 2.998 ns |  9.604 ns | 1,029.61 ns |  1.00 |    0.01 |               1.2109 |                - | 0.0782 |     662 B |        1.00 |
| MediatR     | Suspending  | 5          |   995.21 ns | 3.694 ns | 11.889 ns |   992.74 ns |  0.97 |    0.01 |               1.1314 |                - | 0.1659 |    1399 B |        2.11 |
