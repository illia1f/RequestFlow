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
| Method      | Outcome             | BufferBytes | Mean       | Error    | StdDev   | Median     | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------ |-------------------- |------------ |-----------:|---------:|---------:|-----------:|------:|--------:|---------------------:|-----------------:|-------:|-------:|----------:|------------:|
| RequestFlow | AuthorizationReject | 0           |   380.2 ns |  3.16 ns |  9.41 ns |   384.2 ns |  1.00 |    0.03 |                    - |                - | 0.1221 |      - |      1 KB |        1.00 |
| MediatR     | AuthorizationReject | 0           | 1,043.4 ns |  2.32 ns |  7.09 ns | 1,043.5 ns |  2.75 |    0.07 |                    - |                - | 0.3281 | 0.0019 |   2.68 KB |        2.68 |
|             |                     |             |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | AuthorizationReject | 4096        |   371.3 ns |  3.50 ns | 10.81 ns |   370.5 ns |  1.00 |    0.04 |                    - |                - | 0.1221 |      - |      1 KB |        1.00 |
| MediatR     | AuthorizationReject | 4096        | 1,184.8 ns | 10.70 ns | 34.13 ns | 1,187.6 ns |  3.19 |    0.13 |                    - |                - | 0.8202 | 0.0172 |    6.7 KB |        6.70 |
|             |                     |             |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | CacheHit            | 0           |   542.7 ns |  2.28 ns |  6.96 ns |   544.8 ns |  1.00 |    0.02 |                    - |                - | 0.1450 |      - |   1.19 KB |        1.00 |
| MediatR     | CacheHit            | 0           | 1,009.6 ns |  3.97 ns | 12.44 ns | 1,012.5 ns |  1.86 |    0.03 |                    - |                - | 0.3281 | 0.0019 |   2.68 KB |        2.26 |
|             |                     |             |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | CacheHit            | 4096        |   546.0 ns |  1.40 ns |  4.38 ns |   546.6 ns |  1.00 |    0.01 |                    - |                - | 0.1450 |      - |   1.19 KB |        1.00 |
| MediatR     | CacheHit            | 4096        | 1,168.8 ns |  7.87 ns | 24.86 ns | 1,162.4 ns |  2.14 |    0.05 |                    - |                - | 0.8202 | 0.0172 |    6.7 KB |        5.64 |
|             |                     |             |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | FullExecution       | 0           | 1,254.8 ns |  2.93 ns |  9.13 ns | 1,254.1 ns |  1.00 |    0.01 |                    - |                - | 0.3052 | 0.0019 |    2.5 KB |        1.00 |
| MediatR     | FullExecution       | 0           | 1,376.3 ns |  4.65 ns | 13.91 ns | 1,373.2 ns |  1.10 |    0.01 |                    - |                - | 0.3719 | 0.0019 |   3.05 KB |        1.22 |
|             |                     |             |            |          |          |            |       |         |                      |                  |        |        |           |             |
| RequestFlow | FullExecution       | 4096        | 1,535.7 ns |  5.17 ns | 15.58 ns | 1,534.5 ns |  1.00 |    0.01 |                    - |                - | 1.2894 | 0.0477 |  10.55 KB |        1.00 |
| MediatR     | FullExecution       | 4096        | 1,662.7 ns |  5.03 ns | 15.29 ns | 1,657.4 ns |  1.08 |    0.01 |                    - |                - | 1.3561 | 0.0515 |  11.09 KB |        1.05 |
