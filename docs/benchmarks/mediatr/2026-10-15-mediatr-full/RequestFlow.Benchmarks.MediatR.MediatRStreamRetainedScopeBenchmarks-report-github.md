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
| Method      | ItemCount | Completion  | StageCount | Mean        | Error       | StdDev      | Median      | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------ |---------- |------------ |----------- |------------:|------------:|------------:|------------:|------:|--------:|---------------------:|-----------------:|-------:|-------:|----------:|------------:|
| RequestFlow | 1         | Synchronous | 0          |    164.4 ns |     0.86 ns |     2.71 ns |    163.8 ns |  1.00 |    0.02 |                    - |                - | 0.0448 |      - |     376 B |        1.00 |
| MediatR     | 1         | Synchronous | 0          |    209.0 ns |     2.64 ns |     8.41 ns |    211.7 ns |  1.27 |    0.06 |                    - |                - | 0.0629 |      - |     528 B |        1.40 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 1         | Synchronous | 1          |    216.8 ns |     1.54 ns |     4.64 ns |    215.5 ns |  1.00 |    0.03 |                    - |                - | 0.0448 |      - |     376 B |        1.00 |
| MediatR     | 1         | Synchronous | 1          |    381.1 ns |     5.16 ns |    16.09 ns |    376.0 ns |  1.76 |    0.08 |                    - |                - | 0.1144 |      - |     960 B |        2.55 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 1         | Synchronous | 5          |    359.8 ns |     0.60 ns |     1.91 ns |    360.0 ns |  1.00 |    0.01 |                    - |                - | 0.0448 |      - |     376 B |        1.00 |
| MediatR     | 1         | Synchronous | 5          |    670.9 ns |     3.27 ns |    10.24 ns |    670.4 ns |  1.86 |    0.03 |                    - |                - | 0.2861 | 0.0010 |    2400 B |        6.38 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 1         | Suspending  | 0          |    870.3 ns |     6.04 ns |    18.92 ns |    879.9 ns |  1.00 |    0.03 |               1.0379 |                - | 0.0801 |      - |     696 B |        1.00 |
| MediatR     | 1         | Suspending  | 0          |    885.3 ns |     5.00 ns |    16.25 ns |    883.4 ns |  1.02 |    0.03 |               1.0368 |                - | 0.1011 |      - |     848 B |        1.22 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 1         | Suspending  | 1          |    876.6 ns |     5.24 ns |    16.94 ns |    877.5 ns |  1.00 |    0.03 |               1.0329 |           0.0000 | 0.0830 |      - |     696 B |        1.00 |
| MediatR     | 1         | Suspending  | 1          |  1,258.1 ns |    11.82 ns |    37.34 ns |  1,252.6 ns |  1.44 |    0.05 |               1.0576 |                - | 0.1621 |      - |    1360 B |        1.95 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 1         | Suspending  | 5          |  1,070.2 ns |     5.67 ns |    18.34 ns |  1,065.1 ns |  1.00 |    0.02 |               1.0284 |                - | 0.0820 |      - |     696 B |        1.00 |
| MediatR     | 1         | Suspending  | 5          |  1,844.1 ns |     5.36 ns |    17.42 ns |  1,846.3 ns |  1.72 |    0.03 |               1.1890 |           0.0000 | 0.3719 |      - |    3117 B |        4.48 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 10        | Synchronous | 0          |    422.1 ns |     1.59 ns |     5.02 ns |    419.9 ns |  1.00 |    0.02 |                    - |                - | 0.0534 |      - |     448 B |        1.00 |
| MediatR     | 10        | Synchronous | 0          |    494.7 ns |     1.92 ns |     6.24 ns |    494.3 ns |  1.17 |    0.02 |                    - |                - | 0.0715 |      - |     600 B |        1.34 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 10        | Synchronous | 1          |    461.6 ns |     4.51 ns |    13.98 ns |    453.6 ns |  1.00 |    0.04 |                    - |                - | 0.0534 |      - |     448 B |        1.00 |
| MediatR     | 10        | Synchronous | 1          |    770.4 ns |     1.80 ns |     5.41 ns |    770.6 ns |  1.67 |    0.05 |                    - |                - | 0.1230 |      - |    1032 B |        2.30 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 10        | Synchronous | 5          |    637.8 ns |     3.17 ns |     9.99 ns |    639.4 ns |  1.00 |    0.02 |                    - |                - | 0.0534 |      - |     448 B |        1.00 |
| MediatR     | 10        | Synchronous | 5          |  1,587.4 ns |     3.93 ns |    12.37 ns |  1,584.5 ns |  2.49 |    0.04 |                    - |                - | 0.2937 |      - |    2472 B |        5.52 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 10        | Suspending  | 0          |  6,458.5 ns |    67.15 ns |   208.24 ns |  6,331.0 ns |  1.00 |    0.05 |              10.2986 |           0.0000 | 0.0763 |      - |     696 B |        1.00 |
| MediatR     | 10        | Suspending  | 0          |  6,930.5 ns |    34.74 ns |   112.30 ns |  6,896.3 ns |  1.07 |    0.04 |              10.2841 |           0.0000 | 0.0916 |      - |     848 B |        1.22 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 10        | Suspending  | 1          |  6,497.4 ns |    24.02 ns |    73.40 ns |  6,480.1 ns |  1.00 |    0.02 |              10.2227 |                - | 0.0763 |      - |     696 B |        1.00 |
| MediatR     | 10        | Suspending  | 1          |  7,791.5 ns |    63.54 ns |   204.47 ns |  7,856.3 ns |  1.20 |    0.03 |              10.2726 |           0.0001 | 0.1526 |      - |    1360 B |        1.95 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 10        | Suspending  | 5          |  6,855.7 ns |    45.94 ns |   143.83 ns |  6,800.6 ns |  1.00 |    0.03 |              10.2169 |                - | 0.0763 |      - |     696 B |        1.00 |
| MediatR     | 10        | Suspending  | 5          |  9,579.6 ns |    77.61 ns |   249.76 ns |  9,636.3 ns |  1.40 |    0.05 |              11.4133 |           0.0001 | 0.3662 |      - |    3120 B |        4.48 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 100       | Synchronous | 0          |  2,999.3 ns |     6.86 ns |    21.59 ns |  3,000.3 ns |  1.00 |    0.01 |                    - |                - | 0.0534 |      - |     448 B |        1.00 |
| MediatR     | 100       | Synchronous | 0          |  3,134.3 ns |    33.68 ns |   108.40 ns |  3,060.1 ns |  1.05 |    0.04 |                    - |                - | 0.0687 |      - |     600 B |        1.34 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 100       | Synchronous | 1          |  3,030.5 ns |    10.43 ns |    33.43 ns |  3,025.1 ns |  1.00 |    0.02 |                    - |                - | 0.0534 |      - |     448 B |        1.00 |
| MediatR     | 100       | Synchronous | 1          |  4,631.0 ns |    12.65 ns |    39.61 ns |  4,619.4 ns |  1.53 |    0.02 |                    - |                - | 0.1221 |      - |    1032 B |        2.30 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 100       | Synchronous | 5          |  3,186.4 ns |     5.16 ns |    16.54 ns |  3,182.1 ns |  1.00 |    0.01 |                    - |                - | 0.0534 |      - |     448 B |        1.00 |
| MediatR     | 100       | Synchronous | 5          | 10,840.0 ns |   113.29 ns |   366.19 ns | 10,601.6 ns |  3.40 |    0.12 |                    - |                - | 0.2899 |      - |    2472 B |        5.52 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 100       | Suspending  | 0          | 73,998.1 ns |   819.09 ns | 2,527.88 ns | 72,038.8 ns |  1.00 |    0.05 |             102.3254 |                - |      - |      - |     760 B |        1.00 |
| MediatR     | 100       | Suspending  | 0          | 74,402.3 ns | 1,090.41 ns | 3,540.09 ns | 74,722.1 ns |  1.01 |    0.06 |             103.6545 |           0.0001 |      - |      - |     912 B |        1.20 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 100       | Suspending  | 1          | 70,628.1 ns |   965.17 ns | 3,050.03 ns | 70,597.5 ns |  1.00 |    0.06 |             102.5470 |           0.0001 |      - |      - |     760 B |        1.00 |
| MediatR     | 100       | Suspending  | 1          | 79,915.1 ns |   251.17 ns |   763.82 ns | 79,878.7 ns |  1.13 |    0.05 |             104.4386 |           0.0001 | 0.1221 |      - |    1424 B |        1.87 |
|             |           |             |            |             |             |             |             |       |         |                      |                  |        |        |           |             |
| RequestFlow | 100       | Suspending  | 5          | 68,691.1 ns |   494.26 ns | 1,554.68 ns | 67,560.3 ns |  1.00 |    0.03 |             102.2881 |           0.0001 |      - |      - |     760 B |        1.00 |
| MediatR     | 100       | Suspending  | 5          | 80,843.3 ns |   485.08 ns | 1,475.14 ns | 80,167.0 ns |  1.18 |    0.03 |             103.1371 |           0.0002 | 0.3662 |      - |    3184 B |        4.19 |
