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
| Method      | CatalogSize | Completion  | Mean      | Error    | StdDev   | Median    | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Allocated | Alloc Ratio |
|------------ |------------ |------------ |----------:|---------:|---------:|----------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| RequestFlow | 4           | Cached      |  61.20 ns | 0.886 ns | 2.851 ns |  62.54 ns |  1.00 |    0.07 |                    - |                - | 0.0001 |       1 B |        1.00 |
| MediatR     | 4           | Cached      | 122.08 ns | 2.313 ns | 7.376 ns | 125.73 ns |  2.00 |    0.15 |                    - |                - | 0.0200 |     169 B |      169.00 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 4           | Synchronous |  61.68 ns | 0.643 ns | 2.088 ns |  60.86 ns |  1.00 |    0.05 |                    - |                - | 0.0087 |      73 B |        1.00 |
| MediatR     | 4           | Synchronous | 124.14 ns | 2.332 ns | 7.505 ns | 129.08 ns |  2.01 |    0.14 |                    - |                - | 0.0286 |     241 B |        3.30 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 4           | Suspending  | 505.89 ns | 1.978 ns | 6.193 ns | 508.29 ns |  1.00 |    0.02 |               1.0015 |           0.0000 | 0.0124 |     107 B |        1.00 |
| MediatR     | 4           | Suspending  | 547.30 ns | 0.926 ns | 2.980 ns | 546.91 ns |  1.08 |    0.01 |               1.0007 |           0.0000 | 0.0324 |     275 B |        2.57 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 16          | Cached      |  76.59 ns | 0.303 ns | 0.985 ns |  76.51 ns |  1.00 |    0.02 |                    - |                - | 0.0001 |       1 B |        1.00 |
| MediatR     | 16          | Cached      | 164.47 ns | 0.765 ns | 2.406 ns | 163.96 ns |  2.15 |    0.04 |                    - |                - | 0.0196 |     165 B |      165.00 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 16          | Synchronous |  84.44 ns | 0.273 ns | 0.842 ns |  84.25 ns |  1.00 |    0.01 |                    - |                - | 0.0087 |      73 B |        1.00 |
| MediatR     | 16          | Synchronous | 168.20 ns | 0.661 ns | 2.060 ns | 167.93 ns |  1.99 |    0.03 |                    - |                - | 0.0281 |     237 B |        3.25 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 16          | Suspending  | 518.82 ns | 0.795 ns | 2.489 ns | 518.22 ns |  1.00 |    0.01 |               1.0012 |                - | 0.0114 |     107 B |        1.00 |
| MediatR     | 16          | Suspending  | 578.70 ns | 1.187 ns | 3.752 ns | 578.93 ns |  1.12 |    0.01 |               1.0005 |           0.0000 | 0.0324 |     271 B |        2.53 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 64          | Cached      | 103.16 ns | 0.271 ns | 0.872 ns | 103.06 ns |  1.00 |    0.01 |                    - |                - | 0.0001 |       1 B |        1.00 |
| MediatR     | 64          | Cached      | 199.71 ns | 0.976 ns | 3.086 ns | 199.23 ns |  1.94 |    0.03 |                    - |                - | 0.0200 |     168 B |      168.00 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 64          | Synchronous | 109.81 ns | 0.334 ns | 1.062 ns | 109.75 ns |  1.00 |    0.01 |                    - |                - | 0.0087 |      73 B |        1.00 |
| MediatR     | 64          | Synchronous | 204.20 ns | 1.085 ns | 3.381 ns | 205.08 ns |  1.86 |    0.04 |                    - |                - | 0.0286 |     240 B |        3.29 |
|             |             |             |           |          |          |           |       |         |                      |                  |        |           |             |
| RequestFlow | 64          | Suspending  | 546.64 ns | 2.082 ns | 6.639 ns | 546.13 ns |  1.00 |    0.02 |               1.0008 |           0.0000 | 0.0124 |     107 B |        1.00 |
| MediatR     | 64          | Suspending  | 640.03 ns | 1.968 ns | 6.361 ns | 640.09 ns |  1.17 |    0.02 |               1.0003 |                - | 0.0324 |     274 B |        2.56 |
