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
| Method      | ItemCount | Completion  | StageCount | Mean        | Error     | StdDev      | Median      | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------ |---------- |------------ |----------- |------------:|----------:|------------:|------------:|------:|--------:|-------:|---------------------:|-----------------:|----------:|------------:|
| RequestFlow | 1         | Synchronous | 0          |    388.2 ns |   7.65 ns |    24.17 ns |    373.0 ns |  1.00 |    0.09 | 0.1030 |                    - |                - |     864 B |        1.00 |
| DispatchR   | 1         | Synchronous | 0          |    442.0 ns |   1.21 ns |     3.75 ns |    441.9 ns |  1.14 |    0.07 | 0.1230 |                    - |                - |    1032 B |        1.19 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 1         | Synchronous | 1          |    585.1 ns |  11.96 ns |    38.66 ns |    562.8 ns |  1.00 |    0.09 | 0.1497 |                    - |                - |    1256 B |        1.00 |
| DispatchR   | 1         | Synchronous | 1          |    563.1 ns |   3.95 ns |    12.83 ns |    563.2 ns |  0.97 |    0.07 | 0.1287 |                    - |                - |    1080 B |        0.86 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 1         | Synchronous | 5          |  1,023.7 ns |   3.77 ns |    11.86 ns |  1,020.2 ns |  1.00 |    0.02 | 0.2708 |                    - |                - |    2272 B |        1.00 |
| DispatchR   | 1         | Synchronous | 5          |  1,002.4 ns |   8.03 ns |    26.06 ns |  1,003.3 ns |  0.98 |    0.03 | 0.2575 |                    - |                - |    2160 B |        0.95 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 1         | Suspending  | 0          |  1,160.0 ns |   7.32 ns |    23.12 ns |  1,164.8 ns |  1.00 |    0.03 | 0.1545 |               1.0346 |                - |    1304 B |        1.00 |
| DispatchR   | 1         | Suspending  | 0          |  1,172.4 ns |   4.83 ns |    15.69 ns |  1,170.5 ns |  1.01 |    0.02 | 0.1659 |               1.0252 |                - |    1392 B |        1.07 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 1         | Suspending  | 1          |  1,334.1 ns |   4.81 ns |    15.27 ns |  1,333.4 ns |  1.00 |    0.02 | 0.2022 |               1.0307 |                - |    1696 B |        1.00 |
| DispatchR   | 1         | Suspending  | 1          |  1,295.1 ns |   3.03 ns |     9.66 ns |  1,295.0 ns |  0.97 |    0.01 | 0.1717 |               1.0264 |                - |    1440 B |        0.85 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 1         | Suspending  | 5          |  1,948.3 ns |   6.74 ns |    21.87 ns |  1,948.7 ns |  1.00 |    0.02 | 0.3204 |               1.0262 |                - |    2712 B |        1.00 |
| DispatchR   | 1         | Suspending  | 5          |  1,829.4 ns |   9.49 ns |    30.81 ns |  1,819.1 ns |  0.94 |    0.02 | 0.2995 |               1.0206 |                - |    2520 B |        0.93 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 10        | Synchronous | 0          |    665.5 ns |   2.40 ns |     7.66 ns |    667.0 ns |  1.00 |    0.02 | 0.1202 |                    - |                - |    1008 B |        1.00 |
| DispatchR   | 10        | Synchronous | 0          |    590.5 ns |   3.42 ns |    11.09 ns |    591.3 ns |  0.89 |    0.02 | 0.1402 |                    - |                - |    1176 B |        1.17 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 10        | Synchronous | 1          |    791.2 ns |   1.75 ns |     5.39 ns |    789.7 ns |  1.00 |    0.01 | 0.1669 |                    - |                - |    1400 B |        1.00 |
| DispatchR   | 10        | Synchronous | 1          |    704.8 ns |   3.47 ns |    10.66 ns |    708.1 ns |  0.89 |    0.01 | 0.1459 |                    - |                - |    1224 B |        0.87 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 10        | Synchronous | 5          |  1,378.5 ns |   7.19 ns |    22.29 ns |  1,378.1 ns |  1.00 |    0.02 | 0.2880 |                    - |                - |    2416 B |        1.00 |
| DispatchR   | 10        | Synchronous | 5          |  1,160.7 ns |   8.30 ns |    26.09 ns |  1,171.7 ns |  0.84 |    0.02 | 0.2747 |                    - |                - |    2304 B |        0.95 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 10        | Suspending  | 0          |  6,818.0 ns |  13.69 ns |    43.65 ns |  6,816.3 ns |  1.00 |    0.01 | 0.1526 |              10.3135 |                - |    1304 B |        1.00 |
| DispatchR   | 10        | Suspending  | 0          |  6,214.3 ns |  19.16 ns |    61.65 ns |  6,209.7 ns |  0.91 |    0.01 | 0.1526 |              10.3414 |                - |    1392 B |        1.07 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 10        | Suspending  | 1          |  7,172.2 ns |  17.22 ns |    55.65 ns |  7,174.1 ns |  1.00 |    0.01 | 0.1984 |              10.2518 |           0.0000 |    1696 B |        1.00 |
| DispatchR   | 10        | Suspending  | 1          |  6,365.2 ns |  40.26 ns |   128.98 ns |  6,374.7 ns |  0.89 |    0.02 | 0.1678 |              10.3874 |                - |    1440 B |        0.85 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 10        | Suspending  | 5          |  7,960.9 ns |  28.12 ns |    90.90 ns |  7,952.6 ns |  1.00 |    0.02 | 0.3052 |              10.3253 |           0.0001 |    2712 B |        1.00 |
| DispatchR   | 10        | Suspending  | 5          |  7,025.7 ns |  25.91 ns |    82.25 ns |  7,024.9 ns |  0.88 |    0.01 | 0.2747 |              10.3412 |                - |    2520 B |        0.93 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 100       | Synchronous | 0          |  3,174.3 ns |   6.47 ns |    19.68 ns |  3,180.3 ns |  1.00 |    0.01 | 0.1183 |                    - |                - |    1008 B |        1.00 |
| DispatchR   | 100       | Synchronous | 0          |  1,747.9 ns |   5.71 ns |    17.36 ns |  1,752.7 ns |  0.55 |    0.01 | 0.1392 |                    - |                - |    1176 B |        1.17 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 100       | Synchronous | 1          |  3,446.6 ns |  15.25 ns |    47.30 ns |  3,469.1 ns |  1.00 |    0.02 | 0.1640 |                    - |                - |    1400 B |        1.00 |
| DispatchR   | 100       | Synchronous | 1          |  1,868.6 ns |  13.51 ns |    42.89 ns |  1,892.7 ns |  0.54 |    0.01 | 0.1450 |                    - |                - |    1224 B |        0.87 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 100       | Synchronous | 5          |  3,950.9 ns |  16.94 ns |    54.76 ns |  3,937.7 ns |  1.00 |    0.02 | 0.2823 |                    - |                - |    2416 B |        1.00 |
| DispatchR   | 100       | Synchronous | 5          |  2,292.6 ns |  12.01 ns |    37.42 ns |  2,309.0 ns |  0.58 |    0.01 | 0.2747 |                    - |                - |    2304 B |        0.95 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 100       | Suspending  | 0          | 68,260.1 ns |  54.69 ns |   172.83 ns | 68,274.7 ns |  1.00 |    0.00 | 0.1221 |             102.0483 |           0.0002 |    1368 B |        1.00 |
| DispatchR   | 100       | Suspending  | 0          | 63,528.1 ns | 270.24 ns |   869.62 ns | 63,318.8 ns |  0.93 |    0.01 | 0.1221 |             102.2471 |           0.0002 |    1456 B |        1.06 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 100       | Suspending  | 1          | 68,946.6 ns | 384.31 ns | 1,197.50 ns | 69,271.2 ns |  1.00 |    0.02 | 0.1221 |             102.0322 |           0.0002 |    1760 B |        1.00 |
| DispatchR   | 100       | Suspending  | 1          | 63,507.9 ns | 162.94 ns |   524.33 ns | 63,458.8 ns |  0.92 |    0.02 | 0.1221 |             102.4032 |                - |    1504 B |        0.85 |
|             |           |             |            |             |           |             |             |       |         |        |                      |                  |           |             |
| RequestFlow | 100       | Suspending  | 5          | 67,506.6 ns | 609.47 ns | 1,961.26 ns | 68,343.8 ns |  1.00 |    0.04 | 0.2441 |             101.9631 |           0.0001 |    2776 B |        1.00 |
| DispatchR   | 100       | Suspending  | 5          | 63,776.5 ns |  81.30 ns |   261.63 ns | 63,747.4 ns |  0.95 |    0.03 | 0.2441 |             102.1656 |           0.0001 |    2584 B |        0.93 |
