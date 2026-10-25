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
| Method      | Completion  | StageCount | Mean       | Error    | StdDev   | Median     | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Gen1   | Allocated | Alloc Ratio |
|------------ |------------ |----------- |-----------:|---------:|---------:|-----------:|------:|--------:|-------:|---------------------:|-----------------:|-------:|----------:|------------:|
| RequestFlow | Synchronous | 0          |   246.5 ns |  4.24 ns | 13.14 ns |   237.8 ns |  1.00 |    0.08 | 0.0582 |                    - |                - |      - |     488 B |        1.00 |
| DispatchR   | Synchronous | 0          |   376.4 ns |  3.32 ns | 10.33 ns |   383.0 ns |  1.53 |    0.09 | 0.1040 |                    - |                - |      - |     872 B |        1.79 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Synchronous | 1          |   372.0 ns |  3.57 ns | 10.69 ns |   371.9 ns |  1.00 |    0.04 | 0.1049 |                    - |                - |      - |     880 B |        1.00 |
| DispatchR   | Synchronous | 1          |   482.4 ns |  2.61 ns |  8.45 ns |   482.6 ns |  1.30 |    0.04 | 0.1097 |                    - |                - |      - |     920 B |        1.05 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Synchronous | 5          |   899.6 ns |  2.49 ns |  7.65 ns |   900.6 ns |  1.00 |    0.01 | 0.2260 |                    - |                - | 0.0010 |    1896 B |        1.00 |
| DispatchR   | Synchronous | 5          |   972.1 ns |  6.46 ns | 20.97 ns |   971.2 ns |  1.08 |    0.02 | 0.2384 |                    - |                - |      - |    2000 B |        1.05 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 0          |   886.5 ns |  2.91 ns |  9.11 ns |   886.8 ns |  1.00 |    0.01 | 0.0849 |               1.0127 |                - |      - |     712 B |        1.00 |
| DispatchR   | Suspending  | 0          | 1,066.4 ns |  7.66 ns | 24.75 ns | 1,065.6 ns |  1.20 |    0.03 | 0.1316 |               1.0148 |                - |      - |    1112 B |        1.56 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 1          | 1,107.7 ns |  6.09 ns | 19.77 ns | 1,111.1 ns |  1.00 |    0.03 | 0.1450 |               1.0257 |                - |      - |    1224 B |        1.00 |
| DispatchR   | Suspending  | 1          | 1,239.1 ns |  5.01 ns | 16.19 ns | 1,235.5 ns |  1.12 |    0.02 | 0.1526 |               1.0272 |                - |      - |    1288 B |        1.05 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 5          | 2,044.9 ns | 14.70 ns | 47.74 ns | 2,052.5 ns |  1.00 |    0.03 | 0.3204 |               1.1941 |                - |      - |    2710 B |        1.00 |
| DispatchR   | Suspending  | 5          | 2,048.1 ns |  3.83 ns | 12.28 ns | 2,049.2 ns |  1.00 |    0.02 | 0.3433 |               1.1820 |                - |      - |    2872 B |        1.06 |
