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
| Method      | CatalogSize | Completion  | Mean      | Error    | StdDev   | Median    | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------ |------------ |------------ |----------:|---------:|---------:|----------:|------:|--------:|-------:|---------------------:|-----------------:|----------:|------------:|
| RequestFlow | 4           | Cached      |  57.62 ns | 0.054 ns | 0.174 ns |  57.66 ns |  1.00 |    0.00 | 0.0001 |                    - |                - |       1 B |        1.00 |
| DispatchR   | 4           | Cached      |  61.54 ns | 0.347 ns | 1.117 ns |  61.19 ns |  1.07 |    0.02 | 0.0001 |                    - |                - |       1 B |        1.00 |
|             |             |             |           |          |          |           |       |         |        |                      |                  |           |             |
| RequestFlow | 4           | Synchronous |  60.94 ns | 0.522 ns | 1.595 ns |  61.03 ns |  1.00 |    0.04 | 0.0087 |                    - |                - |      73 B |        1.00 |
| DispatchR   | 4           | Synchronous |  64.90 ns | 0.394 ns | 1.275 ns |  65.01 ns |  1.07 |    0.03 | 0.0087 |                    - |                - |      73 B |        1.00 |
|             |             |             |           |          |          |           |       |         |        |                      |                  |           |             |
| RequestFlow | 4           | Suspending  | 495.86 ns | 1.970 ns | 6.225 ns | 493.99 ns |  1.00 |    0.02 | 0.0124 |               1.0013 |           0.0000 |     107 B |        1.00 |
| DispatchR   | 4           | Suspending  | 499.21 ns | 1.383 ns | 4.432 ns | 498.26 ns |  1.01 |    0.02 | 0.0114 |               1.0012 |                - |     107 B |        1.00 |
|             |             |             |           |          |          |           |       |         |        |                      |                  |           |             |
| RequestFlow | 16          | Cached      |  79.14 ns | 0.156 ns | 0.507 ns |  79.15 ns |  1.00 |    0.01 | 0.0001 |                    - |                - |       1 B |        1.00 |
| DispatchR   | 16          | Cached      |  68.61 ns | 1.092 ns | 3.544 ns |  67.94 ns |  0.87 |    0.04 | 0.0001 |                    - |                - |       1 B |        1.00 |
|             |             |             |           |          |          |           |       |         |        |                      |                  |           |             |
| RequestFlow | 16          | Synchronous |  87.11 ns | 0.265 ns | 0.781 ns |  87.13 ns |  1.00 |    0.01 | 0.0087 |                    - |                - |      73 B |        1.00 |
| DispatchR   | 16          | Synchronous |  78.88 ns | 0.476 ns | 1.540 ns |  78.72 ns |  0.91 |    0.02 | 0.0087 |                    - |                - |      73 B |        1.00 |
|             |             |             |           |          |          |           |       |         |        |                      |                  |           |             |
| RequestFlow | 16          | Suspending  | 514.63 ns | 0.880 ns | 2.807 ns | 515.13 ns |  1.00 |    0.01 | 0.0124 |               1.0008 |           0.0000 |     107 B |        1.00 |
| DispatchR   | 16          | Suspending  | 512.45 ns | 0.790 ns | 2.495 ns | 512.47 ns |  1.00 |    0.01 | 0.0114 |               1.0009 |                - |     107 B |        1.00 |
|             |             |             |           |          |          |           |       |         |        |                      |                  |           |             |
| RequestFlow | 64          | Cached      | 107.35 ns | 0.559 ns | 1.800 ns | 106.95 ns |  1.00 |    0.02 | 0.0001 |                    - |                - |       1 B |        1.00 |
| DispatchR   | 64          | Cached      |  87.30 ns | 0.673 ns | 2.167 ns |  87.24 ns |  0.81 |    0.02 | 0.0001 |                    - |                - |       1 B |        1.00 |
|             |             |             |           |          |          |           |       |         |        |                      |                  |           |             |
| RequestFlow | 64          | Synchronous | 115.64 ns | 0.291 ns | 0.937 ns | 115.45 ns |  1.00 |    0.01 | 0.0087 |                    - |                - |      73 B |        1.00 |
| DispatchR   | 64          | Synchronous | 102.77 ns | 0.509 ns | 1.601 ns | 103.24 ns |  0.89 |    0.02 | 0.0087 |                    - |                - |      73 B |        1.00 |
|             |             |             |           |          |          |           |       |         |        |                      |                  |           |             |
| RequestFlow | 64          | Suspending  | 538.24 ns | 1.325 ns | 4.282 ns | 538.12 ns |  1.00 |    0.01 | 0.0114 |               1.0009 |                - |     107 B |        1.00 |
| DispatchR   | 64          | Suspending  | 537.55 ns | 1.188 ns | 3.806 ns | 538.21 ns |  1.00 |    0.01 | 0.0114 |               1.0008 |           0.0000 |     107 B |        1.00 |
