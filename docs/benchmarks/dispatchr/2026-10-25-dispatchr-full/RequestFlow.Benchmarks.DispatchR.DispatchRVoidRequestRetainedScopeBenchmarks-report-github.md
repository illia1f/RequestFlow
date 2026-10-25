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
| RequestFlow | Synchronous | 0          |    45.65 ns | 0.333 ns |  1.077 ns |    46.45 ns |  1.00 |    0.03 |      - |                    - |                - |         - |          NA |
| DispatchR   | Synchronous | 0          |    37.43 ns | 0.255 ns |  0.814 ns |    36.89 ns |  0.82 |    0.03 |      - |                    - |                - |         - |          NA |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Synchronous | 1          |   101.16 ns | 0.630 ns |  1.972 ns |    99.77 ns |  1.00 |    0.03 |      - |                    - |                - |         - |          NA |
| DispatchR   | Synchronous | 1          |    35.97 ns | 0.109 ns |  0.334 ns |    35.97 ns |  0.36 |    0.01 |      - |                    - |                - |         - |          NA |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Synchronous | 5          |   267.51 ns | 9.001 ns | 27.373 ns |   242.80 ns |  1.01 |    0.15 |      - |                    - |                - |         - |          NA |
| DispatchR   | Synchronous | 5          |    50.94 ns | 0.087 ns |  0.279 ns |    51.01 ns |  0.19 |    0.02 |      - |                    - |                - |         - |          NA |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 0          |   698.66 ns | 3.852 ns | 12.507 ns |   697.35 ns |  1.00 |    0.03 | 0.0248 |               1.0185 |                - |     213 B |        1.00 |
| DispatchR   | Suspending  | 0          |   703.93 ns | 2.362 ns |  7.668 ns |   703.16 ns |  1.01 |    0.02 | 0.0124 |               1.0000 |                - |     104 B |        0.49 |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 1          |   738.94 ns | 1.984 ns |  6.356 ns |   738.88 ns |  1.00 |    0.01 | 0.0515 |               1.0771 |                - |     438 B |        1.00 |
| DispatchR   | Suspending  | 1          |   710.93 ns | 1.650 ns |  5.334 ns |   709.88 ns |  0.96 |    0.01 | 0.0248 |               1.0183 |                - |     212 B |        0.48 |
|             |             |            |             |          |           |             |       |         |        |                      |                  |           |             |
| RequestFlow | Suspending  | 5          | 1,533.67 ns | 3.376 ns | 10.667 ns | 1,533.15 ns |  1.00 |    0.01 | 0.1526 |               1.6867 |                - |    1281 B |        1.00 |
| DispatchR   | Suspending  | 5          |   856.63 ns | 2.443 ns |  7.755 ns |   856.15 ns |  0.56 |    0.01 | 0.0772 |               1.1835 |                - |     652 B |        0.51 |
