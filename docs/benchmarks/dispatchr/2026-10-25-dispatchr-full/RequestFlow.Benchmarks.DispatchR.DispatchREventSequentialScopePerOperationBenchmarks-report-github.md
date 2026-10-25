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
| Method      | Completion  | HandlerCount | Mean       | Error    | StdDev   | Median     | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Gen1   | Allocated | Alloc Ratio |
|------------ |------------ |------------- |-----------:|---------:|---------:|-----------:|------:|--------:|-------:|---------------------:|-----------------:|-------:|----------:|------------:|
| RequestFlow | Synchronous | 1            |   241.6 ns |  1.20 ns |  3.76 ns |   241.8 ns |  1.00 |    0.02 | 0.0582 |                    - |                - |      - |     488 B |        1.00 |
| DispatchR   | Synchronous | 1            |   321.9 ns |  2.36 ns |  7.51 ns |   318.4 ns |  1.33 |    0.04 | 0.1040 |                    - |                - |      - |     872 B |        1.79 |
|             |             |              |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Synchronous | 5            |   757.5 ns |  5.42 ns | 17.36 ns |   765.0 ns |  1.00 |    0.03 | 0.1268 |                    - |                - |      - |    1064 B |        1.00 |
| DispatchR   | Synchronous | 5            |   722.7 ns |  2.36 ns |  7.32 ns |   720.9 ns |  0.95 |    0.02 | 0.2289 |                    - |                - | 0.0010 |    1920 B |        1.80 |
|             |             |              |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 1            |   903.0 ns |  4.54 ns | 14.54 ns |   905.4 ns |  1.00 |    0.02 | 0.1030 |               1.0312 |                - |      - |     864 B |        1.00 |
| DispatchR   | Suspending  | 1            | 1,064.1 ns |  5.85 ns | 18.98 ns | 1,068.1 ns |  1.18 |    0.03 | 0.1469 |               1.0255 |                - |      - |    1232 B |        1.43 |
|             |             |              |            |          |          |            |       |         |        |                      |                  |        |           |             |
| RequestFlow | Suspending  | 5            | 2,973.7 ns | 10.99 ns | 34.88 ns | 2,974.5 ns |  1.00 |    0.02 | 0.2136 |               5.0142 |                - |      - |    1856 B |        1.00 |
| DispatchR   | Suspending  | 5            | 3,571.2 ns | 13.08 ns | 42.28 ns | 3,566.4 ns |  1.20 |    0.02 | 0.3204 |               5.0489 |                - |      - |    2696 B |        1.45 |
