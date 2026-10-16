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
| Method      | Outcome             | Mean      | Error    | StdDev   | Median    | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Allocated | Alloc Ratio |
|------------ |-------------------- |----------:|---------:|---------:|----------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| RequestFlow | AuthorizationReject |  43.21 ns | 0.053 ns | 0.166 ns |  43.22 ns |  1.00 |    0.01 |                    - |                - |      - |         - |          NA |
| MediatR     | AuthorizationReject | 124.99 ns | 1.429 ns | 4.454 ns | 126.31 ns |  2.89 |    0.10 |                    - |                - | 0.0610 |     512 B |          NA |
|             |                     |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | CacheHit            |  84.88 ns | 0.289 ns | 0.901 ns |  84.48 ns |  1.00 |    0.01 |                    - |                - |      - |         - |          NA |
| MediatR     | CacheHit            | 129.10 ns | 0.753 ns | 2.445 ns | 128.77 ns |  1.52 |    0.03 |                    - |                - | 0.0610 |     512 B |          NA |
|             |                     |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | FullExecution       | 177.38 ns | 2.835 ns | 9.081 ns | 171.31 ns |  1.00 |    0.07 |                    - |                - |      - |         - |          NA |
| MediatR     | FullExecution       | 188.79 ns | 0.984 ns | 3.080 ns | 187.89 ns |  1.07 |    0.06 |                    - |                - | 0.0610 |     512 B |          NA |
