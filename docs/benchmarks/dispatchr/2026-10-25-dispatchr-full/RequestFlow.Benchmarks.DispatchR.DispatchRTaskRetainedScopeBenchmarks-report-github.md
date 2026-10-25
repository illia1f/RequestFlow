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
| Method      | Completion  | StageCount | Mean        | Error    | StdDev    | Median      | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------ |------------ |----------- |------------:|---------:|----------:|------------:|------:|--------:|-------:|---------------------:|-----------------:|----------:|------------:|
| RequestFlow | Cached      | 0          |    54.52 ns | 0.841 ns |  2.682 ns |    56.85 ns |  1.00 |    0.07 |      - |                    - |                - |         - |          NA |
| DispatchR   | Cached      | 0          |    37.87 ns | 0.343 ns |  1.099 ns |    37.07 ns |  0.70 |    0.04 |      - |                    - |                - |         - |          NA |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Cached      | 1          |    85.75 ns | 0.350 ns |  1.105 ns |    84.97 ns |  1.00 |    0.02 |      - |                    - |                - |         - |          NA |
| DispatchR   | Cached      | 1          |    36.81 ns | 0.278 ns |  0.866 ns |    37.50 ns |  0.43 |    0.01 |      - |                    - |                - |         - |          NA |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Cached      | 5          |   244.51 ns | 0.194 ns |  0.619 ns |   244.39 ns |  1.00 |    0.00 |      - |                    - |                - |         - |          NA |
| DispatchR   | Cached      | 5          |    61.03 ns | 2.693 ns |  8.628 ns |    69.05 ns |  0.25 |    0.04 |      - |                    - |                - |         - |          NA |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Synchronous | 0          |    54.92 ns | 0.268 ns |  0.826 ns |    55.10 ns |  1.00 |    0.02 | 0.0086 |                    - |                - |      72 B |        1.00 |
| DispatchR   | Synchronous | 0          |    39.49 ns | 0.203 ns |  0.660 ns |    39.68 ns |  0.72 |    0.02 | 0.0086 |                    - |                - |      72 B |        1.00 |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Synchronous | 1          |    88.53 ns | 0.343 ns |  1.103 ns |    88.71 ns |  1.00 |    0.02 | 0.0086 |                    - |                - |      72 B |        1.00 |
| DispatchR   | Synchronous | 1          |    42.05 ns | 0.575 ns |  1.867 ns |    42.56 ns |  0.48 |    0.02 | 0.0086 |                    - |                - |      72 B |        1.00 |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Synchronous | 5          |   243.09 ns | 0.803 ns |  2.584 ns |   242.99 ns |  1.00 |    0.01 | 0.0086 |                    - |                - |      72 B |        1.00 |
| DispatchR   | Synchronous | 5          |    57.28 ns | 0.964 ns |  2.988 ns |    59.75 ns |  0.24 |    0.01 | 0.0086 |                    - |                - |      72 B |        1.00 |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 0          |   695.72 ns | 1.514 ns |  4.892 ns |   695.99 ns |  1.00 |    0.01 | 0.0124 |               1.0000 |                - |     104 B |        1.00 |
| DispatchR   | Suspending  | 0          |   709.81 ns | 1.917 ns |  6.223 ns |   710.07 ns |  1.02 |    0.01 | 0.0124 |               1.0000 |                - |     104 B |        1.00 |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 1          |   681.34 ns | 1.417 ns |  4.600 ns |   681.83 ns |  1.00 |    0.01 | 0.0257 |               1.0151 |                - |     216 B |        1.00 |
| DispatchR   | Suspending  | 1          |   701.05 ns | 1.993 ns |  6.441 ns |   700.83 ns |  1.03 |    0.01 | 0.0248 |               1.0189 |                - |     215 B |        1.00 |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 5          | 1,034.85 ns | 7.450 ns | 22.542 ns | 1,027.72 ns |  1.00 |    0.03 | 0.0782 |               1.2412 |                - |     662 B |        1.00 |
| DispatchR   | Suspending  | 5          |   857.31 ns | 2.625 ns |  8.486 ns |   857.86 ns |  0.83 |    0.02 | 0.0782 |               1.1730 |                - |     660 B |        1.00 |
