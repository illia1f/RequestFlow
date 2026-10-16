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
| Method      | Completion  | StageCount | Mean       | Error    | StdDev   | Median     | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------ |------------ |----------- |-----------:|---------:|---------:|-----------:|------:|--------:|---------------------:|-----------------:|-------:|-------:|----------:|------------:|
| RequestFlow | Synchronous | 0          |   220.8 ns |  2.18 ns |  6.96 ns |   220.0 ns |  1.00 |    0.04 |                    - |                - | 0.0582 |      - |     488 B |        1.00 |
| MediatR     | Synchronous | 0          |   267.8 ns |  1.50 ns |  4.84 ns |   268.8 ns |  1.21 |    0.04 |                    - |                - | 0.0706 |      - |     592 B |        1.21 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Synchronous | 1          |   288.5 ns |  1.22 ns |  3.89 ns |   288.5 ns |  1.00 |    0.02 |                    - |                - | 0.0620 |      - |     520 B |        1.00 |
| MediatR     | Synchronous | 1          |   520.8 ns |  7.37 ns | 23.70 ns |   531.5 ns |  1.81 |    0.09 |                    - |                - | 0.1431 |      - |    1200 B |        2.31 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Synchronous | 5          |   764.3 ns |  2.52 ns |  7.95 ns |   763.1 ns |  1.00 |    0.01 |                    - |                - | 0.1307 |      - |    1096 B |        1.00 |
| MediatR     | Synchronous | 5          |   969.2 ns |  6.57 ns | 20.77 ns |   968.6 ns |  1.27 |    0.03 |                    - |                - | 0.3204 | 0.0019 |    2696 B |        2.46 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Suspending  | 0          |   893.9 ns |  2.99 ns |  9.59 ns |   893.1 ns |  1.00 |    0.02 |               1.0301 |                - | 0.0982 |      - |     824 B |        1.00 |
| MediatR     | Suspending  | 0          |   980.3 ns |  3.12 ns |  9.94 ns |   980.7 ns |  1.10 |    0.02 |               1.0294 |           0.0000 | 0.1106 |      - |     928 B |        1.13 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Suspending  | 1          | 1,130.9 ns |  2.77 ns |  8.58 ns | 1,131.4 ns |  1.00 |    0.01 |               1.0725 |                - | 0.1278 |      - |    1079 B |        1.00 |
| MediatR     | Suspending  | 1          | 1,337.7 ns |  3.53 ns | 11.36 ns | 1,339.6 ns |  1.18 |    0.01 |               1.0380 |                - | 0.1965 |      - |    1648 B |        1.53 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Suspending  | 5          | 2,280.0 ns |  6.29 ns | 20.33 ns | 2,279.3 ns |  1.00 |    0.01 |               1.5874 |                - | 0.2975 |      - |    2501 B |        1.00 |
| MediatR     | Suspending  | 5          | 2,152.2 ns | 10.78 ns | 34.70 ns | 2,164.8 ns |  0.94 |    0.02 |               1.1095 |                - | 0.4272 |      - |    3589 B |        1.44 |
