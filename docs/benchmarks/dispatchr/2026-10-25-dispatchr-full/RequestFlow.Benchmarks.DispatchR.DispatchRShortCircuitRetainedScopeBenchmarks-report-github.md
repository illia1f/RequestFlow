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
| Method      | Outcome             | Mean      | Error    | StdDev   | Median    | Ratio | RatioSD | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------ |-------------------- |----------:|---------:|---------:|----------:|------:|--------:|---------------------:|-----------------:|----------:|------------:|
| RequestFlow | AuthorizationReject |  44.73 ns | 0.045 ns | 0.145 ns |  44.73 ns |  1.00 |    0.00 |                    - |                - |         - |          NA |
| DispatchR   | AuthorizationReject |  37.07 ns | 0.135 ns | 0.427 ns |  37.09 ns |  0.83 |    0.01 |                    - |                - |         - |          NA |
|             |                     |           |          |          |           |       |         |                      |                  |           |             |
| RequestFlow | CacheHit            |  83.32 ns | 0.156 ns | 0.494 ns |  83.18 ns |  1.00 |    0.01 |                    - |                - |         - |          NA |
| DispatchR   | CacheHit            |  40.85 ns | 1.349 ns | 4.302 ns |  44.52 ns |  0.49 |    0.05 |                    - |                - |         - |          NA |
|             |                     |           |          |          |           |       |         |                      |                  |           |             |
| RequestFlow | FullExecution       | 165.53 ns | 1.496 ns | 4.503 ns | 169.21 ns |  1.00 |    0.04 |                    - |                - |         - |          NA |
| DispatchR   | FullExecution       |  40.06 ns | 0.249 ns | 0.791 ns |  40.64 ns |  0.24 |    0.01 |                    - |                - |         - |          NA |
