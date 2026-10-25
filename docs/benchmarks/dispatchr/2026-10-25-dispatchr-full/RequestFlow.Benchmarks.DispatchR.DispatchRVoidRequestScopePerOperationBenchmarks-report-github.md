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
| RequestFlow | Synchronous | 0          |   221.8 ns |  0.67 ns |  2.09 ns |   222.2 ns |  1.00 |    0.01 | 0.0582 |                    - |                - |      - |     488 B |        1.00 |
| DispatchR   | Synchronous | 0          |   373.2 ns |  1.51 ns |  4.54 ns |   374.2 ns |  1.68 |    0.03 | 0.1040 |                    - |                - |      - |     872 B |        1.79 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Synchronous | 1          |   365.6 ns |  2.00 ns |  5.95 ns |   368.5 ns |  1.00 |    0.02 | 0.1049 |                    - |                - |      - |     880 B |        1.00 |
| DispatchR   | Synchronous | 1          |   470.6 ns |  2.39 ns |  7.75 ns |   470.9 ns |  1.29 |    0.03 | 0.1097 |                    - |                - |      - |     920 B |        1.05 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Synchronous | 5          |   840.3 ns |  3.50 ns | 10.42 ns |   844.7 ns |  1.00 |    0.02 | 0.2260 |                    - |                - | 0.0010 |    1896 B |        1.00 |
| DispatchR   | Synchronous | 5          |   934.8 ns | 14.10 ns | 44.35 ns |   945.8 ns |  1.11 |    0.05 | 0.2384 |                    - |                - |      - |    2000 B |        1.05 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 0          |   894.7 ns |  5.28 ns | 17.00 ns |   890.9 ns |  1.00 |    0.03 | 0.0982 |               1.0289 |                - |      - |     824 B |        1.00 |
| DispatchR   | Suspending  | 0          | 1,057.6 ns |  4.72 ns | 15.04 ns | 1,059.1 ns |  1.18 |    0.03 | 0.1297 |               1.0106 |                - |      - |    1096 B |        1.33 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 1          | 1,203.3 ns |  2.96 ns |  9.43 ns | 1,203.3 ns |  1.00 |    0.01 | 0.1717 |               1.0658 |                - |      - |    1439 B |        1.00 |
| DispatchR   | Suspending  | 1          | 1,218.0 ns |  7.59 ns | 23.43 ns | 1,213.3 ns |  1.01 |    0.02 | 0.1488 |               1.0205 |                - |      - |    1256 B |        0.87 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 5          | 2,321.4 ns |  8.04 ns | 25.65 ns | 2,318.9 ns |  1.00 |    0.02 | 0.3929 |               1.5970 |                - |      - |    3312 B |        1.00 |
| DispatchR   | Suspending  | 5          | 1,919.7 ns | 10.88 ns | 34.53 ns | 1,922.3 ns |  0.83 |    0.02 | 0.3319 |               1.0826 |                - |      - |    2782 B |        0.84 |
