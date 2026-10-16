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
| RequestFlow | Cached      | 0          |   223.9 ns |  6.29 ns | 19.50 ns |   234.3 ns |  1.01 |    0.12 |                    - |                - | 0.0668 |      - |     560 B |        1.00 |
| MediatR     | Cached      | 0          |   260.3 ns |  1.26 ns |  3.99 ns |   261.1 ns |  1.17 |    0.10 |                    - |                - | 0.0792 |      - |     664 B |        1.19 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Cached      | 1          |   300.0 ns |  1.12 ns |  3.61 ns |   300.3 ns |  1.00 |    0.02 |                    - |                - | 0.0706 |      - |     592 B |        1.00 |
| MediatR     | Cached      | 1          |   509.5 ns |  4.85 ns | 15.33 ns |   511.9 ns |  1.70 |    0.05 |                    - |                - | 0.1516 |      - |    1272 B |        2.15 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Cached      | 5          |   776.8 ns |  2.43 ns |  7.50 ns |   776.2 ns |  1.00 |    0.01 |                    - |                - | 0.1392 |      - |    1168 B |        1.00 |
| MediatR     | Cached      | 5          |   977.8 ns | 14.16 ns | 44.96 ns | 1,003.1 ns |  1.26 |    0.06 |                    - |                - | 0.3300 | 0.0019 |    2768 B |        2.37 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Synchronous | 0          |   213.1 ns |  0.81 ns |  2.53 ns |   212.5 ns |  1.00 |    0.02 |                    - |                - | 0.0753 |      - |     632 B |        1.00 |
| MediatR     | Synchronous | 0          |   270.5 ns |  3.03 ns |  9.84 ns |   271.2 ns |  1.27 |    0.05 |                    - |                - | 0.0877 |      - |     736 B |        1.16 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Synchronous | 1          |   300.1 ns |  3.34 ns | 10.85 ns |   300.2 ns |  1.00 |    0.05 |                    - |                - | 0.0792 |      - |     664 B |        1.00 |
| MediatR     | Synchronous | 1          |   516.0 ns |  6.32 ns | 19.97 ns |   507.0 ns |  1.72 |    0.09 |                    - |                - | 0.1602 |      - |    1344 B |        2.02 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Synchronous | 5          |   774.4 ns |  3.33 ns | 10.27 ns |   777.7 ns |  1.00 |    0.02 |                    - |                - | 0.1478 |      - |    1240 B |        1.00 |
| MediatR     | Synchronous | 5          |   959.4 ns |  2.54 ns |  8.12 ns |   959.0 ns |  1.24 |    0.02 |                    - |                - | 0.3395 | 0.0019 |    2840 B |        2.29 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Suspending  | 0          |   852.4 ns |  5.41 ns | 17.24 ns |   855.8 ns |  1.00 |    0.03 |               1.0122 |                - | 0.0849 |      - |     712 B |        1.00 |
| MediatR     | Suspending  | 0          |   926.2 ns |  5.87 ns | 19.05 ns |   921.4 ns |  1.09 |    0.03 |               1.0119 |                - | 0.0973 |      - |     816 B |        1.15 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Suspending  | 1          | 1,022.0 ns |  2.86 ns |  9.23 ns | 1,021.6 ns |  1.00 |    0.01 |               1.0291 |                - | 0.1011 |      - |     856 B |        1.00 |
| MediatR     | Suspending  | 1          | 1,233.8 ns |  5.74 ns | 18.39 ns | 1,232.9 ns |  1.21 |    0.02 |               1.0231 |                - | 0.1831 |      - |    1536 B |        1.79 |
|             |             |            |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | Suspending  | 5          | 1,806.3 ns |  5.34 ns | 17.33 ns | 1,806.8 ns |  1.00 |    0.01 |               1.1335 |                - | 0.2232 |      - |    1878 B |        1.00 |
| MediatR     | Suspending  | 5          | 2,050.6 ns |  9.93 ns | 32.24 ns | 2,048.5 ns |  1.14 |    0.02 |               1.0953 |                - | 0.4158 |      - |    3479 B |        1.85 |
