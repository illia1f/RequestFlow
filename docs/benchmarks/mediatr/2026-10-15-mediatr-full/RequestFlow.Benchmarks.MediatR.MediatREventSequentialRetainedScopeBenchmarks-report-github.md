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
| Method      | Completion  | HandlerCount | Mean        | Error    | StdDev    | Median      | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Allocated | Alloc Ratio |
|------------ |------------ |------------- |------------:|---------:|----------:|------------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| RequestFlow | Synchronous | 1            |    62.66 ns | 0.277 ns |  0.863 ns |    62.27 ns |  1.00 |    0.02 |                    - |                - |      - |         - |          NA |
| MediatR     | Synchronous | 1            |    99.20 ns | 0.504 ns |  1.636 ns |    98.71 ns |  1.58 |    0.03 |                    - |                - | 0.0277 |     232 B |          NA |
|             |             |              |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Synchronous | 5            |   253.65 ns | 0.406 ns |  1.277 ns |   254.12 ns |  1.00 |    0.01 |                    - |                - |      - |         - |          NA |
| MediatR     | Synchronous | 5            |   196.36 ns | 1.614 ns |  5.170 ns |   193.75 ns |  0.77 |    0.02 |                    - |                - | 0.0851 |     712 B |          NA |
|             |             |              |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Suspending  | 1            |   699.15 ns | 1.617 ns |  5.248 ns |   698.56 ns |  1.00 |    0.01 |               1.0234 |                - | 0.0296 |     252 B |        1.00 |
| MediatR     | Suspending  | 1            |   716.73 ns | 5.170 ns | 16.639 ns |   711.29 ns |  1.03 |    0.02 |               1.0107 |                - | 0.0563 |     471 B |        1.87 |
|             |             |              |             |          |           |             |       |         |                      |                  |        |           |             |
| RequestFlow | Suspending  | 5            | 2,570.69 ns | 5.270 ns | 16.808 ns | 2,570.16 ns |  1.00 |    0.01 |               5.0274 |                - | 0.0801 |     672 B |        1.00 |
| MediatR     | Suspending  | 5            | 2,619.50 ns | 4.746 ns | 15.272 ns | 2,618.89 ns |  1.02 |    0.01 |               5.0329 |                - | 0.1640 |    1368 B |        2.04 |
