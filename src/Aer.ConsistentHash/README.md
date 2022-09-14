Benchmarks (256 virtual nodes for each physical one):

// * Summary *

BenchmarkDotNet=v0.13.1, OS=macOS Monterey 12.3.1 (21E258) [Darwin 21.4.0]
Apple M1, 1 CPU, 8 logical and 8 physical cores
.NET SDK=6.0.400
[Host]     : .NET 6.0.8 (6.0.822.36306), Arm64 RyuJIT
DefaultJob : .NET 6.0.8 (6.0.822.36306), Arm64 RyuJIT


|   Method | KeysNumber | NodesNumber |         Mean |      Error |     StdDev |       Median |
|--------- |----------- |------------ |-------------:|-----------:|-----------:|-------------:|
| GetNodes |          1 |           1 |     5.683 us |  0.5613 us |   1.655 us |     4.692 us |
| GetNodes |          1 |          16 |     5.485 us |  0.5255 us |   1.549 us |     4.639 us |
| GetNodes |          1 |          32 |     6.239 us |  0.7027 us |   2.072 us |     5.060 us |
| GetNodes |        128 |           1 |    33.824 us |  2.7571 us |   8.086 us |    29.377 us |
| GetNodes |        128 |          16 |   118.482 us |  7.0747 us |  20.860 us |   114.546 us |
| GetNodes |        128 |          32 |   188.920 us | 12.1676 us |  35.877 us |   181.387 us |
| GetNodes |        512 |           1 |    73.147 us |  4.7427 us |  13.984 us |    66.924 us |
| GetNodes |        512 |          16 |   175.545 us | 10.4275 us |  30.746 us |   168.805 us |
| GetNodes |        512 |          32 |   293.666 us | 13.3944 us |  39.494 us |   277.418 us |
| GetNodes |       2048 |           1 |   193.951 us |  8.5778 us |  24.749 us |   189.355 us |
| GetNodes |       2048 |          16 |   326.530 us | 15.2840 us |  44.825 us |   309.335 us |
| GetNodes |       2048 |          32 |   466.940 us | 18.1174 us |  52.849 us |   456.591 us |
| GetNodes |       5000 |           1 |   427.750 us | 16.5527 us |  48.806 us |   420.915 us |
| GetNodes |       5000 |          16 |   574.372 us | 25.4257 us |  74.569 us |   564.302 us |
| GetNodes |       5000 |          32 |   688.616 us | 26.3884 us |  76.558 us |   663.938 us |
| GetNodes |      10000 |           1 |   814.684 us | 27.5884 us |  80.039 us |   807.244 us |
| GetNodes |      10000 |          16 | 1,020.214 us | 36.8499 us | 108.074 us | 1,021.344 us |
| GetNodes |      10000 |          32 | 1,269.259 us | 35.2069 us | 103.256 us | 1,288.021 us |
| GetNodes |      20000 |           1 | 1,617.165 us | 44.5917 us | 131.480 us | 1,629.595 us |
| GetNodes |      20000 |          16 | 1,899.443 us | 63.8206 us | 188.176 us | 1,828.317 us |
| GetNodes |      20000 |          32 | 2,059.760 us | 60.0584 us | 174.240 us | 2,015.047 us |

```md
KeysNumber  : Value of the 'KeysNumber' parameter
NodesNumber : Value of the 'NodesNumber' parameter
Mean        : Arithmetic mean of all measurements
Error       : Half of 99.9% confidence interval
StdDev      : Standard deviation of all measurements
Median      : Value separating the higher half of all measurements (50th percentile)
1 us        : 1 Microsecond (0.000001 sec)
```
