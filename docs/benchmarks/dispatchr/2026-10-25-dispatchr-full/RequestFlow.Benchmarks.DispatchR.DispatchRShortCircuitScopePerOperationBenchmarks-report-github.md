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
| Method      | Outcome             | BufferBytes | Mean       | Error    | StdDev    | Median     | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Gen1   | Allocated | Alloc Ratio |
|------------ |-------------------- |------------ |-----------:|---------:|----------:|-----------:|------:|--------:|-------:|---------------------:|-----------------:|-------:|----------:|------------:|
| RequestFlow | AuthorizationReject | 0           |   402.6 ns |  1.79 ns |   5.62 ns |   403.1 ns |  1.00 |    0.02 | 0.1221 |                    - |                - |      - |      1 KB |        1.00 |
| DispatchR   | AuthorizationReject | 0           | 1,343.8 ns |  8.01 ns |  25.77 ns | 1,343.5 ns |  3.34 |    0.08 | 0.3128 |                    - |                - | 0.0019 |   2.57 KB |        2.57 |
|             |                     |             |            |          |           |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | AuthorizationReject | 4096        |   381.4 ns |  2.38 ns |   7.04 ns |   385.2 ns |  1.00 |    0.03 | 0.1221 |                    - |                - |      - |      1 KB |        1.00 |
| DispatchR   | AuthorizationReject | 4096        | 1,706.2 ns | 32.39 ns | 100.44 ns | 1,646.3 ns |  4.47 |    0.27 | 1.2989 |                    - |                - | 0.0496 |  10.62 KB |       10.62 |
|             |                     |             |            |          |           |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | CacheHit            | 0           |   601.9 ns |  3.60 ns |  11.17 ns |   601.9 ns |  1.00 |    0.03 | 0.1450 |                    - |                - |      - |   1.19 KB |        1.00 |
| DispatchR   | CacheHit            | 0           | 1,320.8 ns |  4.98 ns |  15.74 ns | 1,313.9 ns |  2.20 |    0.05 | 0.3128 |                    - |                - | 0.0019 |   2.57 KB |        2.16 |
|             |                     |             |            |          |           |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | CacheHit            | 4096        |   605.0 ns |  1.37 ns |   4.26 ns |   604.5 ns |  1.00 |    0.01 | 0.1450 |                    - |                - |      - |   1.19 KB |        1.00 |
| DispatchR   | CacheHit            | 4096        | 1,733.0 ns | 20.01 ns |  59.93 ns | 1,727.7 ns |  2.86 |    0.10 | 1.2989 |                    - |                - | 0.0496 |  10.62 KB |        8.94 |
|             |                     |             |            |          |           |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | FullExecution       | 0           | 1,288.4 ns |  8.36 ns |  26.30 ns | 1,283.4 ns |  1.00 |    0.03 | 0.3052 |                    - |                - | 0.0019 |    2.5 KB |        1.00 |
| DispatchR   | FullExecution       | 0           | 1,340.2 ns |  6.01 ns |  19.50 ns | 1,338.4 ns |  1.04 |    0.03 | 0.3128 |                    - |                - | 0.0019 |   2.57 KB |        1.03 |
|             |                     |             |            |          |           |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | FullExecution       | 4096        | 1,558.1 ns |  4.80 ns |  14.31 ns | 1,559.9 ns |  1.00 |    0.01 | 1.2894 |                    - |                - | 0.0477 |  10.55 KB |        1.00 |
| DispatchR   | FullExecution       | 4096        | 1,614.7 ns |  9.91 ns |  30.13 ns | 1,617.4 ns |  1.04 |    0.02 | 1.2989 |                    - |                - | 0.0496 |  10.62 KB |        1.01 |
