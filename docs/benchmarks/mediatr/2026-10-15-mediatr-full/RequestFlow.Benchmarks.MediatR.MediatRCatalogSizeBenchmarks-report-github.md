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
| Method      | CatalogSize | Completion  | Mean      | Error    | StdDev   | Median    | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Allocated | Alloc Ratio |
|------------ |------------ |------------ |----------:|---------:|---------:|----------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| RequestFlow | 4           | Cached      |  53.74 ns | 0.347 ns | 1.055 ns |  54.48 ns |  1.00 |    0.03 |                    - |                - |      - |         - |          NA |
| MediatR     | 4           | Cached      |  75.68 ns | 0.680 ns | 2.129 ns |  76.70 ns |  1.41 |    0.05 |                    - |                - | 0.0124 |     104 B |          NA |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 4           | Synchronous |  55.72 ns | 0.409 ns | 1.323 ns |  56.54 ns |  1.00 |    0.03 |                    - |                - | 0.0086 |      72 B |        1.00 |
| MediatR     | 4           | Synchronous |  80.05 ns | 0.386 ns | 1.225 ns |  79.86 ns |  1.44 |    0.04 |                    - |                - | 0.0210 |     176 B |        2.44 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 4           | Suspending  | 688.02 ns | 1.675 ns | 5.439 ns | 687.85 ns |  1.00 |    0.01 |               1.0000 |                - | 0.0124 |     104 B |        1.00 |
| MediatR     | 4           | Suspending  | 705.53 ns | 0.933 ns | 3.016 ns | 705.72 ns |  1.03 |    0.01 |               1.0000 |                - | 0.0248 |     208 B |        2.00 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 16          | Cached      |  58.18 ns | 0.193 ns | 0.587 ns |  58.01 ns |  1.00 |    0.01 |                    - |                - |      - |         - |          NA |
| MediatR     | 16          | Cached      |  78.17 ns | 0.130 ns | 0.408 ns |  78.18 ns |  1.34 |    0.02 |                    - |                - | 0.0124 |     104 B |          NA |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 16          | Synchronous |  58.63 ns | 0.108 ns | 0.346 ns |  58.55 ns |  1.00 |    0.01 |                    - |                - | 0.0086 |      72 B |        1.00 |
| MediatR     | 16          | Synchronous |  82.06 ns | 1.165 ns | 3.731 ns |  83.68 ns |  1.40 |    0.06 |                    - |                - | 0.0210 |     176 B |        2.44 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 16          | Suspending  | 679.57 ns | 1.919 ns | 6.121 ns | 680.77 ns |  1.00 |    0.01 |               1.0000 |                - | 0.0124 |     104 B |        1.00 |
| MediatR     | 16          | Suspending  | 707.36 ns | 1.758 ns | 5.682 ns | 707.04 ns |  1.04 |    0.01 |               1.0000 |                - | 0.0248 |     208 B |        2.00 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 64          | Cached      |  57.65 ns | 0.100 ns | 0.315 ns |  57.62 ns |  1.00 |    0.01 |                    - |                - |      - |         - |          NA |
| MediatR     | 64          | Cached      |  77.53 ns | 0.991 ns | 3.176 ns |  77.71 ns |  1.34 |    0.06 |                    - |                - | 0.0124 |     104 B |          NA |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 64          | Synchronous |  61.43 ns | 0.256 ns | 0.827 ns |  61.34 ns |  1.00 |    0.02 |                    - |                - | 0.0086 |      72 B |        1.00 |
| MediatR     | 64          | Synchronous |  81.27 ns | 0.378 ns | 1.222 ns |  81.19 ns |  1.32 |    0.03 |                    - |                - | 0.0210 |     176 B |        2.44 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 64          | Suspending  | 690.35 ns | 2.237 ns | 7.262 ns | 691.58 ns |  1.00 |    0.01 |               1.0000 |                - | 0.0124 |     104 B |        1.00 |
| MediatR     | 64          | Suspending  | 701.26 ns | 1.866 ns | 6.059 ns | 702.18 ns |  1.02 |    0.01 |               1.0000 |                - | 0.0248 |     208 B |        2.00 |
