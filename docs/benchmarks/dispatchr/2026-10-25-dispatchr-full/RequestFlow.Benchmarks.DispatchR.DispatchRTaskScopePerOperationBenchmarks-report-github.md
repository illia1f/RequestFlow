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
| RequestFlow | Cached      | 0          |   235.3 ns |  1.41 ns |  4.49 ns |   236.0 ns |  1.00 |    0.03 | 0.0668 |                    - |                - |      - |     560 B |        1.00 |
| DispatchR   | Cached      | 0          |   371.8 ns |  2.93 ns |  9.08 ns |   374.1 ns |  1.58 |    0.05 | 0.1125 |                    - |                - |      - |     944 B |        1.69 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Cached      | 1          |   394.9 ns |  7.18 ns | 21.85 ns |   398.8 ns |  1.00 |    0.08 | 0.1135 |                    - |                - |      - |     952 B |        1.00 |
| DispatchR   | Cached      | 1          |   475.4 ns |  1.68 ns |  5.18 ns |   475.2 ns |  1.21 |    0.07 | 0.1183 |                    - |                - |      - |     992 B |        1.04 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Cached      | 5          |   844.3 ns |  1.61 ns |  4.85 ns |   844.4 ns |  1.00 |    0.01 | 0.2346 |                    - |                - | 0.0010 |    1968 B |        1.00 |
| DispatchR   | Cached      | 5          |   959.5 ns | 13.16 ns | 41.58 ns |   976.2 ns |  1.14 |    0.05 | 0.2460 |                    - |                - |      - |    2072 B |        1.05 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Synchronous | 0          |   240.9 ns |  1.26 ns |  4.07 ns |   241.1 ns |  1.00 |    0.02 | 0.0753 |                    - |                - |      - |     632 B |        1.00 |
| DispatchR   | Synchronous | 0          |   383.1 ns |  0.99 ns |  3.02 ns |   382.4 ns |  1.59 |    0.03 | 0.1211 |                    - |                - |      - |    1016 B |        1.61 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Synchronous | 1          |   383.7 ns |  4.78 ns | 14.61 ns |   392.8 ns |  1.00 |    0.05 | 0.1221 |                    - |                - |      - |    1024 B |        1.00 |
| DispatchR   | Synchronous | 1          |   469.3 ns |  2.47 ns |  7.90 ns |   468.2 ns |  1.22 |    0.05 | 0.1268 |                    - |                - |      - |    1064 B |        1.04 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Synchronous | 5          |   882.9 ns |  2.28 ns |  7.11 ns |   881.7 ns |  1.00 |    0.01 | 0.2432 |                    - |                - | 0.0010 |    2040 B |        1.00 |
| DispatchR   | Synchronous | 5          |   937.1 ns | 11.23 ns | 34.67 ns |   945.6 ns |  1.06 |    0.04 | 0.2556 |                    - |                - | 0.0010 |    2144 B |        1.05 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 0          |   842.4 ns |  5.61 ns | 18.22 ns |   847.0 ns |  1.00 |    0.03 | 0.0849 |               1.0113 |                - |      - |     712 B |        1.00 |
| DispatchR   | Suspending  | 0          | 1,029.0 ns |  3.14 ns | 10.14 ns | 1,029.8 ns |  1.22 |    0.03 | 0.1297 |               1.0101 |           0.0000 |      - |    1096 B |        1.54 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 1          | 1,087.7 ns |  6.69 ns | 21.44 ns | 1,083.3 ns |  1.00 |    0.03 | 0.1450 |               1.0220 |                - |      - |    1216 B |        1.00 |
| DispatchR   | Suspending  | 1          | 1,183.3 ns |  3.92 ns | 12.73 ns | 1,183.1 ns |  1.09 |    0.02 | 0.1488 |               1.0226 |                - |      - |    1256 B |        1.03 |
|             |             |            |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 5          | 1,876.7 ns |  5.36 ns | 17.40 ns | 1,874.6 ns |  1.00 |    0.01 | 0.3166 |               1.0773 |                - |      - |    2679 B |        1.00 |
| DispatchR   | Suspending  | 5          | 1,957.1 ns |  4.23 ns | 13.62 ns | 1,955.2 ns |  1.04 |    0.01 | 0.3319 |               1.0812 |                - |      - |    2783 B |        1.04 |
