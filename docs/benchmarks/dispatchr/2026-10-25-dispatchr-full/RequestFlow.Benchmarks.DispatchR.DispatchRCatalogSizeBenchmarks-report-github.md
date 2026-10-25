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
| Method      | CatalogSize | Completion  | Mean      | Error    | StdDev    | Median    | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------ |------------ |------------ |----------:|---------:|----------:|----------:|------:|--------:|-------:|---------------------:|-----------------:|----------:|------------:|
| RequestFlow | 4           | Cached      |  51.62 ns | 0.673 ns |  2.046 ns |  53.29 ns |  1.00 |    0.06 |      - |                    - |                - |         - |          NA |
| DispatchR   | 4           | Cached      |  38.57 ns | 0.149 ns |  0.459 ns |  38.36 ns |  0.75 |    0.03 |      - |                    - |                - |         - |          NA |
|             |             |             |           |          |           |           |       |         |        |                      |                  |           |             |
| RequestFlow | 4           | Synchronous |  55.24 ns | 0.121 ns |  0.379 ns |  55.33 ns |  1.00 |    0.01 | 0.0086 |                    - |                - |      72 B |        1.00 |
| DispatchR   | 4           | Synchronous |  42.63 ns | 0.372 ns |  1.202 ns |  42.59 ns |  0.77 |    0.02 | 0.0086 |                    - |                - |      72 B |        1.00 |
|             |             |             |           |          |           |           |       |         |        |                      |                  |           |             |
| RequestFlow | 4           | Suspending  | 694.37 ns | 3.758 ns | 12.201 ns | 696.11 ns |  1.00 |    0.02 | 0.0124 |               1.0000 |                - |     104 B |        1.00 |
| DispatchR   | 4           | Suspending  | 705.43 ns | 1.696 ns |  5.481 ns | 705.46 ns |  1.02 |    0.02 | 0.0124 |               1.0000 |                - |     104 B |        1.00 |
|             |             |             |           |          |           |           |       |         |        |                      |                  |           |             |
| RequestFlow | 16          | Cached      |  56.88 ns | 0.145 ns |  0.451 ns |  56.89 ns |  1.00 |    0.01 |      - |                    - |                - |         - |          NA |
| DispatchR   | 16          | Cached      |  41.57 ns | 0.765 ns |  2.452 ns |  39.40 ns |  0.73 |    0.04 |      - |                    - |                - |         - |          NA |
|             |             |             |           |          |           |           |       |         |        |                      |                  |           |             |
| RequestFlow | 16          | Synchronous |  57.95 ns | 0.387 ns |  1.235 ns |  57.75 ns |  1.00 |    0.03 | 0.0086 |                    - |                - |      72 B |        1.00 |
| DispatchR   | 16          | Synchronous |  40.62 ns | 0.223 ns |  0.650 ns |  40.61 ns |  0.70 |    0.02 | 0.0086 |                    - |                - |      72 B |        1.00 |
|             |             |             |           |          |           |           |       |         |        |                      |                  |           |             |
| RequestFlow | 16          | Suspending  | 706.20 ns | 3.705 ns | 11.870 ns | 703.10 ns |  1.00 |    0.02 | 0.0124 |               1.0000 |                - |     104 B |        1.00 |
| DispatchR   | 16          | Suspending  | 705.19 ns | 1.696 ns |  5.505 ns | 705.30 ns |  1.00 |    0.02 | 0.0124 |               1.0000 |                - |     104 B |        1.00 |
|             |             |             |           |          |           |           |       |         |        |                      |                  |           |             |
| RequestFlow | 64          | Cached      |  54.53 ns | 0.484 ns |  1.508 ns |  55.70 ns |  1.00 |    0.04 |      - |                    - |                - |         - |          NA |
| DispatchR   | 64          | Cached      |  37.70 ns | 0.081 ns |  0.246 ns |  37.75 ns |  0.69 |    0.02 |      - |                    - |                - |         - |          NA |
|             |             |             |           |          |           |           |       |         |        |                      |                  |           |             |
| RequestFlow | 64          | Synchronous |  59.82 ns | 0.239 ns |  0.765 ns |  59.85 ns |  1.00 |    0.02 | 0.0086 |                    - |                - |      72 B |        1.00 |
| DispatchR   | 64          | Synchronous |  40.38 ns | 0.429 ns |  1.361 ns |  40.42 ns |  0.68 |    0.02 | 0.0086 |                    - |                - |      72 B |        1.00 |
|             |             |             |           |          |           |           |       |         |        |                      |                  |           |             |
| RequestFlow | 64          | Suspending  | 681.12 ns | 2.415 ns |  7.839 ns | 682.19 ns |  1.00 |    0.02 | 0.0124 |               1.0000 |                - |     104 B |        1.00 |
| DispatchR   | 64          | Suspending  | 696.27 ns | 3.213 ns | 10.431 ns | 696.18 ns |  1.02 |    0.02 | 0.0124 |               1.0000 |                - |     104 B |        1.00 |
