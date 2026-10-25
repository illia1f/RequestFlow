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
| Method      | ItemCount | Completion  | StageCount | Mean         | Error        | StdDev       | Median       | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------ |---------- |------------ |----------- |-------------:|-------------:|-------------:|-------------:|------:|--------:|-------:|---------------------:|-----------------:|----------:|------------:|
| RequestFlow | 1         | Synchronous | 0          |    165.25 ns |     0.553 ns |     1.762 ns |    164.82 ns |  1.00 |    0.01 | 0.0448 |                    - |                - |     376 B |        1.00 |
| DispatchR   | 1         | Synchronous | 0          |     89.00 ns |     0.187 ns |     0.580 ns |     89.04 ns |  0.54 |    0.01 | 0.0191 |                    - |                - |     160 B |        0.43 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 1         | Synchronous | 1          |    206.94 ns |     1.051 ns |     3.274 ns |    205.33 ns |  1.00 |    0.02 | 0.0448 |                    - |                - |     376 B |        1.00 |
| DispatchR   | 1         | Synchronous | 1          |     91.97 ns |     0.674 ns |     2.061 ns |     91.51 ns |  0.44 |    0.01 | 0.0191 |                    - |                - |     160 B |        0.43 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 1         | Synchronous | 5          |    370.93 ns |     2.498 ns |     7.671 ns |    374.35 ns |  1.00 |    0.03 | 0.0448 |                    - |                - |     376 B |        1.00 |
| DispatchR   | 1         | Synchronous | 5          |    107.64 ns |     1.949 ns |     6.046 ns |    112.86 ns |  0.29 |    0.02 | 0.0191 |                    - |                - |     160 B |        0.43 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 1         | Suspending  | 0          |    843.69 ns |     5.079 ns |    16.489 ns |    848.48 ns |  1.00 |    0.03 | 0.0830 |               1.0299 |                - |     696 B |        1.00 |
| DispatchR   | 1         | Suspending  | 0          |    698.19 ns |     3.023 ns |     9.815 ns |    698.37 ns |  0.83 |    0.02 | 0.0467 |               1.0220 |                - |     399 B |        0.57 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 1         | Suspending  | 1          |    863.31 ns |     4.550 ns |    14.771 ns |    860.31 ns |  1.00 |    0.02 | 0.0830 |               1.0307 |                - |     696 B |        1.00 |
| DispatchR   | 1         | Suspending  | 1          |    717.40 ns |     1.835 ns |     5.932 ns |    718.20 ns |  0.83 |    0.02 | 0.0477 |               1.0224 |                - |     399 B |        0.57 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 1         | Suspending  | 5          |  1,061.36 ns |     7.525 ns |    24.432 ns |  1,060.62 ns |  1.00 |    0.03 | 0.0820 |               1.0301 |                - |     696 B |        1.00 |
| DispatchR   | 1         | Suspending  | 5          |    725.74 ns |     5.916 ns |    18.697 ns |    728.57 ns |  0.68 |    0.02 | 0.0477 |               1.0196 |                - |     400 B |        0.57 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 10        | Synchronous | 0          |    428.32 ns |     1.437 ns |     4.434 ns |    429.37 ns |  1.00 |    0.01 | 0.0534 |                    - |                - |     448 B |        1.00 |
| DispatchR   | 10        | Synchronous | 0          |    228.86 ns |     1.453 ns |     4.420 ns |    231.59 ns |  0.53 |    0.01 | 0.0277 |                    - |                - |     232 B |        0.52 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 10        | Synchronous | 1          |    455.60 ns |     1.556 ns |     4.895 ns |    457.59 ns |  1.00 |    0.02 | 0.0534 |                    - |                - |     448 B |        1.00 |
| DispatchR   | 10        | Synchronous | 1          |    225.12 ns |     0.869 ns |     2.615 ns |    225.87 ns |  0.49 |    0.01 | 0.0277 |                    - |                - |     232 B |        0.52 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 10        | Synchronous | 5          |    619.50 ns |     1.845 ns |     5.937 ns |    618.75 ns |  1.00 |    0.01 | 0.0534 |                    - |                - |     448 B |        1.00 |
| DispatchR   | 10        | Synchronous | 5          |    238.55 ns |     0.399 ns |     1.261 ns |    238.46 ns |  0.39 |    0.00 | 0.0277 |                    - |                - |     232 B |        0.52 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 10        | Suspending  | 0          |  6,636.47 ns |    68.507 ns |   222.412 ns |  6,579.37 ns |  1.00 |    0.05 | 0.0763 |              10.2310 |                - |     696 B |        1.00 |
| DispatchR   | 10        | Suspending  | 0          |  5,581.94 ns |    10.394 ns |    33.598 ns |  5,580.38 ns |  0.84 |    0.03 | 0.0458 |              10.3294 |                - |     400 B |        0.57 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 10        | Suspending  | 1          |  6,571.86 ns |    21.215 ns |    65.791 ns |  6,568.67 ns |  1.00 |    0.01 | 0.0763 |              10.1515 |           0.0000 |     696 B |        1.00 |
| DispatchR   | 10        | Suspending  | 1          |  5,640.64 ns |    11.451 ns |    36.187 ns |  5,637.63 ns |  0.86 |    0.01 | 0.0458 |              10.2899 |                - |     400 B |        0.57 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 10        | Suspending  | 5          |  6,751.98 ns |    38.137 ns |   120.516 ns |  6,751.00 ns |  1.00 |    0.03 | 0.0763 |              10.2344 |                - |     696 B |        1.00 |
| DispatchR   | 10        | Suspending  | 5          |  5,670.55 ns |    12.278 ns |    37.337 ns |  5,667.92 ns |  0.84 |    0.02 | 0.0458 |              10.3308 |           0.0000 |     400 B |        0.57 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 100       | Synchronous | 0          |  3,054.37 ns |     4.520 ns |    14.545 ns |  3,054.59 ns |  1.00 |    0.01 | 0.0534 |                    - |                - |     448 B |        1.00 |
| DispatchR   | 100       | Synchronous | 0          |  1,361.80 ns |     1.637 ns |     5.221 ns |  1,361.31 ns |  0.45 |    0.00 | 0.0267 |                    - |                - |     232 B |        0.52 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 100       | Synchronous | 1          |  3,181.98 ns |    19.850 ns |    63.305 ns |  3,141.05 ns |  1.00 |    0.03 | 0.0534 |                    - |                - |     448 B |        1.00 |
| DispatchR   | 100       | Synchronous | 1          |  1,372.66 ns |     1.739 ns |     5.520 ns |  1,372.18 ns |  0.43 |    0.01 | 0.0267 |                    - |                - |     232 B |        0.52 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 100       | Synchronous | 5          |  3,142.48 ns |     5.332 ns |    16.771 ns |  3,136.43 ns |  1.00 |    0.01 | 0.0534 |                    - |                - |     448 B |        1.00 |
| DispatchR   | 100       | Synchronous | 5          |  1,377.73 ns |     1.505 ns |     4.755 ns |  1,377.19 ns |  0.44 |    0.00 | 0.0267 |                    - |                - |     232 B |        0.52 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 100       | Suspending  | 0          | 67,745.88 ns |   233.419 ns |   737.628 ns | 67,888.10 ns |  1.00 |    0.02 |      - |             101.5266 |           0.0002 |     760 B |        1.00 |
| DispatchR   | 100       | Suspending  | 0          | 62,074.76 ns |   137.405 ns |   440.194 ns | 61,964.29 ns |  0.92 |    0.01 |      - |             102.2771 |           0.0001 |     464 B |        0.61 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 100       | Suspending  | 1          | 68,765.53 ns |    97.329 ns |   313.206 ns | 68,736.38 ns |  1.00 |    0.01 |      - |             101.7520 |           0.0001 |     760 B |        1.00 |
| DispatchR   | 100       | Suspending  | 1          | 62,243.76 ns |   120.018 ns |   381.017 ns | 62,397.02 ns |  0.91 |    0.01 |      - |             102.4177 |           0.0001 |     464 B |        0.61 |
|             |           |             |            |              |              |              |              |       |         |        |                      |                  |           |             |
| RequestFlow | 100       | Suspending  | 5          | 71,064.42 ns | 1,193.399 ns | 3,753.788 ns | 74,125.26 ns |  1.00 |    0.07 |      - |             102.1124 |           0.0001 |     760 B |        1.00 |
| DispatchR   | 100       | Suspending  | 5          | 62,793.77 ns |   107.156 ns |   344.829 ns | 62,814.69 ns |  0.89 |    0.05 |      - |             102.3267 |           0.0001 |     464 B |        0.61 |
