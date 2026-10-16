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
| Method      | ItemCount | Completion  | StageCount | Mean        | Error     | StdDev      | Median      | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------ |---------- |------------ |----------- |------------:|----------:|------------:|------------:|------:|--------:|---------------------:|-----------------:|-------:|-------:|----------:|------------:|
| RequestFlow | 1         | Synchronous | 0          |    369.2 ns |   4.40 ns |    13.98 ns |    374.5 ns |  1.00 |    0.05 |                    - |                - | 0.1030 |      - |     864 B |        1.00 |
| MediatR     | 1         | Synchronous | 0          |    428.2 ns |   1.03 ns |     3.11 ns |    428.4 ns |  1.16 |    0.04 |                    - |                - | 0.1211 |      - |    1016 B |        1.18 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 1         | Synchronous | 1          |    452.8 ns |   3.14 ns |     9.74 ns |    457.4 ns |  1.00 |    0.03 |                    - |                - | 0.1068 |      - |     896 B |        1.00 |
| MediatR     | 1         | Synchronous | 1          |    781.6 ns |   3.27 ns |    10.13 ns |    779.0 ns |  1.73 |    0.04 |                    - |                - | 0.2232 | 0.0010 |    1872 B |        2.09 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 1         | Synchronous | 5          |    956.3 ns |   6.67 ns |    21.18 ns |    950.7 ns |  1.00 |    0.03 |                    - |                - | 0.1755 |      - |    1472 B |        1.00 |
| MediatR     | 1         | Synchronous | 5          |  1,551.8 ns |   8.49 ns |    27.09 ns |  1,554.1 ns |  1.62 |    0.05 |                    - |                - | 0.5207 | 0.0038 |    4360 B |        2.96 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 1         | Suspending  | 0          |  1,176.2 ns |   4.52 ns |    14.55 ns |  1,171.1 ns |  1.00 |    0.02 |               1.0431 |                - | 0.1545 |      - |    1304 B |        1.00 |
| MediatR     | 1         | Suspending  | 0          |  1,249.6 ns |   6.59 ns |    21.22 ns |  1,253.7 ns |  1.06 |    0.02 |               1.0320 |                - | 0.1678 |      - |    1456 B |        1.12 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 1         | Suspending  | 1          |  1,322.5 ns |   9.96 ns |    32.19 ns |  1,334.6 ns |  1.00 |    0.03 |               1.0388 |           0.0000 | 0.1583 |      - |    1336 B |        1.00 |
| MediatR     | 1         | Suspending  | 1          |  1,829.6 ns |   4.59 ns |    14.91 ns |  1,831.8 ns |  1.38 |    0.04 |               1.0355 |                - | 0.2842 |      - |    2392 B |        1.79 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 1         | Suspending  | 5          |  1,851.1 ns |  11.50 ns |    37.18 ns |  1,863.1 ns |  1.00 |    0.03 |               1.0355 |                - | 0.2251 |      - |    1912 B |        1.00 |
| MediatR     | 1         | Suspending  | 5          |  2,937.0 ns |   9.05 ns |    29.24 ns |  2,925.0 ns |  1.59 |    0.04 |               1.2366 |                - | 0.6180 | 0.0038 |    5195 B |        2.72 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 10        | Synchronous | 0          |    655.7 ns |   4.25 ns |    13.44 ns |    657.2 ns |  1.00 |    0.03 |                    - |                - | 0.1202 |      - |    1008 B |        1.00 |
| MediatR     | 10        | Synchronous | 0          |    765.1 ns |   6.12 ns |    19.34 ns |    771.6 ns |  1.17 |    0.04 |                    - |                - | 0.1383 |      - |    1160 B |        1.15 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 10        | Synchronous | 1          |    751.9 ns |   3.62 ns |    11.53 ns |    753.0 ns |  1.00 |    0.02 |                    - |                - | 0.1240 |      - |    1040 B |        1.00 |
| MediatR     | 10        | Synchronous | 1          |  1,216.1 ns |   5.38 ns |    16.77 ns |  1,222.4 ns |  1.62 |    0.03 |                    - |                - | 0.2403 |      - |    2016 B |        1.94 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 10        | Synchronous | 5          |  1,257.1 ns |  18.09 ns |    58.49 ns |  1,290.0 ns |  1.00 |    0.07 |                    - |                - | 0.1926 |      - |    1616 B |        1.00 |
| MediatR     | 10        | Synchronous | 5          |  2,496.6 ns |  11.95 ns |    37.94 ns |  2,486.8 ns |  1.99 |    0.10 |                    - |                - | 0.5379 |      - |    4504 B |        2.79 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 10        | Suspending  | 0          |  6,877.9 ns |  20.30 ns |    65.32 ns |  6,883.0 ns |  1.00 |    0.01 |              10.2574 |           0.0000 | 0.1526 |      - |    1304 B |        1.00 |
| MediatR     | 10        | Suspending  | 0          |  7,131.6 ns |  17.14 ns |    55.17 ns |  7,130.4 ns |  1.04 |    0.01 |              10.2991 |                - | 0.1678 |      - |    1456 B |        1.12 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 10        | Suspending  | 1          |  7,335.3 ns |  32.89 ns |   105.35 ns |  7,340.5 ns |  1.00 |    0.02 |              10.3394 |                - | 0.1526 |      - |    1336 B |        1.00 |
| MediatR     | 10        | Suspending  | 1          |  8,965.4 ns |  34.99 ns |   107.45 ns |  8,963.6 ns |  1.22 |    0.02 |              10.3853 |           0.0003 | 0.2747 |      - |    2392 B |        1.79 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 10        | Suspending  | 5          |  8,130.7 ns |  36.93 ns |   117.24 ns |  8,127.7 ns |  1.00 |    0.02 |              10.2163 |           0.0001 | 0.2136 |      - |    1912 B |        1.00 |
| MediatR     | 10        | Suspending  | 5          | 10,555.4 ns |  66.28 ns |   203.57 ns | 10,641.5 ns |  1.30 |    0.03 |              10.6330 |           0.0003 | 0.6104 |      - |    5200 B |        2.72 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 100       | Synchronous | 0          |  3,352.9 ns |  20.72 ns |    64.88 ns |  3,316.4 ns |  1.00 |    0.03 |                    - |                - | 0.1183 |      - |    1008 B |        1.00 |
| MediatR     | 100       | Synchronous | 0          |  3,424.6 ns |   4.48 ns |    14.21 ns |  3,423.2 ns |  1.02 |    0.02 |                    - |                - | 0.1373 |      - |    1160 B |        1.15 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 100       | Synchronous | 1          |  3,395.1 ns |  27.90 ns |    87.75 ns |  3,449.7 ns |  1.00 |    0.04 |                    - |                - | 0.1221 |      - |    1040 B |        1.00 |
| MediatR     | 100       | Synchronous | 1          |  5,114.3 ns |  29.35 ns |    91.03 ns |  5,139.0 ns |  1.51 |    0.05 |                    - |                - | 0.2365 |      - |    2016 B |        1.94 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 100       | Synchronous | 5          |  3,968.0 ns |   8.67 ns |    28.03 ns |  3,965.9 ns |  1.00 |    0.01 |                    - |                - | 0.1907 |      - |    1616 B |        1.00 |
| MediatR     | 100       | Synchronous | 5          | 10,820.2 ns |  69.23 ns |   223.77 ns | 10,754.0 ns |  2.73 |    0.06 |                    - |                - | 0.5341 |      - |    4504 B |        2.79 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 100       | Suspending  | 0          | 70,240.1 ns | 313.13 ns |   984.94 ns | 70,241.7 ns |  1.00 |    0.02 |             102.0520 |           0.0002 | 0.1221 |      - |    1368 B |        1.00 |
| MediatR     | 100       | Suspending  | 0          | 71,706.4 ns | 378.46 ns | 1,228.71 ns | 72,025.1 ns |  1.02 |    0.02 |             102.6260 |           0.0004 | 0.1221 |      - |    1520 B |        1.11 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 100       | Suspending  | 1          | 68,194.2 ns | 311.49 ns |   975.19 ns | 68,258.6 ns |  1.00 |    0.02 |             102.2664 |           0.0001 | 0.1221 |      - |    1400 B |        1.00 |
| MediatR     | 100       | Suspending  | 1          | 78,037.6 ns | 653.45 ns | 2,121.46 ns | 78,436.9 ns |  1.14 |    0.04 |             101.8491 |                - | 0.2441 |      - |    2456 B |        1.75 |
|             |           |             |            |             |           |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 100       | Suspending  | 5          | 70,887.1 ns | 372.90 ns | 1,150.86 ns | 70,211.7 ns |  1.00 |    0.02 |             101.9225 |           0.0004 | 0.1221 |      - |    1976 B |        1.00 |
| MediatR     | 100       | Suspending  | 5          | 83,487.7 ns | 201.61 ns |   640.05 ns | 83,509.1 ns |  1.18 |    0.02 |             104.3787 |                - | 0.4883 |      - |    5264 B |        2.66 |
