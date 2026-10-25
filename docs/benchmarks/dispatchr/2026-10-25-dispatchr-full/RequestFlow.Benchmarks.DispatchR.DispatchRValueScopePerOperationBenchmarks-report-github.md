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
| RequestFlow | Synchronous | 0          |   259.9 ns |  5.66 ns | 18.22 ns |   262.0 ns |  1.00 |    0.10 | 0.0582 |                    - |                - |      - |     488 B |        1.00 |
| DispatchR   | Synchronous | 0          |   377.6 ns |  1.92 ns |  5.76 ns |   379.3 ns |  1.46 |    0.10 | 0.1040 |                    - |                - |      - |     872 B |        1.79 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Synchronous | 1          |   367.5 ns |  1.85 ns |  5.55 ns |   369.1 ns |  1.00 |    0.02 | 0.1049 |                    - |                - |      - |     880 B |        1.00 |
| DispatchR   | Synchronous | 1          |   464.9 ns |  2.66 ns |  8.34 ns |   462.4 ns |  1.27 |    0.03 | 0.1097 |                    - |                - |      - |     920 B |        1.05 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Synchronous | 5          |   875.0 ns |  5.87 ns | 18.37 ns |   884.0 ns |  1.00 |    0.03 | 0.2260 |                    - |                - | 0.0010 |    1896 B |        1.00 |
| DispatchR   | Synchronous | 5          |   905.3 ns |  2.42 ns |  7.55 ns |   905.4 ns |  1.03 |    0.02 | 0.2384 |                    - |                - | 0.0010 |    2000 B |        1.05 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 0          |   901.7 ns |  4.97 ns | 16.00 ns |   902.2 ns |  1.00 |    0.03 | 0.0868 |               1.0127 |                - |      - |     728 B |        1.00 |
| DispatchR   | Suspending  | 0          | 1,055.0 ns |  4.69 ns | 15.22 ns | 1,057.1 ns |  1.17 |    0.03 | 0.1316 |               1.0119 |                - |      - |    1112 B |        1.53 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 1          | 1,100.8 ns |  5.62 ns | 18.08 ns | 1,094.2 ns |  1.00 |    0.02 | 0.1488 |               1.0258 |                - |      - |    1248 B |        1.00 |
| DispatchR   | Suspending  | 1          | 1,227.8 ns |  4.36 ns | 13.78 ns | 1,226.6 ns |  1.12 |    0.02 | 0.1526 |               1.0249 |                - |      - |    1288 B |        1.03 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 5          | 2,013.1 ns |  6.26 ns | 20.24 ns | 2,011.2 ns |  1.00 |    0.01 | 0.3281 |               1.2174 |                - |      - |    2767 B |        1.00 |
| DispatchR   | Suspending  | 5          | 2,069.3 ns | 16.98 ns | 53.41 ns | 2,101.2 ns |  1.03 |    0.03 | 0.3433 |               1.1731 |                - |      - |    2873 B |        1.04 |
