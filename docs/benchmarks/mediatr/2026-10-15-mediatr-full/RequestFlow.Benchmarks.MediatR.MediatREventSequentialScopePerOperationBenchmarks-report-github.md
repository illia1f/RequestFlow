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
| Method      | Completion  | HandlerCount | Mean       | Error    | StdDev   | Median     | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Allocated | Alloc Ratio |
|------------ |------------ |------------- |-----------:|---------:|---------:|-----------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| RequestFlow | Synchronous | 1            |   216.2 ns |  1.49 ns |  4.56 ns |   216.1 ns |  1.00 |    0.03 |                    - |                - | 0.0582 |     488 B |        1.00 |
| MediatR     | Synchronous | 1            |   336.1 ns |  3.77 ns | 11.73 ns |   342.1 ns |  1.56 |    0.06 |                    - |                - | 0.0896 |     752 B |        1.54 |
|             |             |              |            |          |          |            |       |         |                      |                  |        |           |             |
| RequestFlow | Synchronous | 5            |   716.3 ns |  3.03 ns |  9.44 ns |   713.4 ns |  1.00 |    0.02 |                    - |                - | 0.1268 |    1064 B |        1.00 |
| MediatR     | Synchronous | 5            |   824.1 ns | 10.23 ns | 32.02 ns |   810.3 ns |  1.15 |    0.05 |                    - |                - | 0.2193 |    1840 B |        1.73 |
|             |             |              |            |          |          |            |       |         |                      |                  |        |           |             |
| RequestFlow | Suspending  | 1            |   919.6 ns | 10.62 ns | 34.02 ns |   905.1 ns |  1.00 |    0.05 |               1.0307 |                - | 0.1030 |     864 B |        1.00 |
| MediatR     | Suspending  | 1            | 1,055.7 ns |  3.36 ns | 10.90 ns | 1,056.7 ns |  1.15 |    0.04 |               1.0208 |                - | 0.1316 |    1112 B |        1.29 |
|             |             |              |            |          |          |            |       |         |                      |                  |        |           |             |
| RequestFlow | Suspending  | 5            | 2,949.4 ns | 11.18 ns | 35.81 ns | 2,964.8 ns |  1.00 |    0.02 |               5.0183 |                - | 0.2213 |    1856 B |        1.00 |
| MediatR     | Suspending  | 5            | 3,381.0 ns |  9.09 ns | 29.37 ns | 3,378.1 ns |  1.15 |    0.02 |               5.0307 |                - | 0.3128 |    2616 B |        1.41 |
