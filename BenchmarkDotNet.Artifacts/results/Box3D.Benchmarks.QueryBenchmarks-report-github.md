```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8875)
Unknown processor
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-XLSJMA : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=Job-XLSJMA  IterationCount=15  WarmupCount=5  

```
| Method                               | Mean        | Ratio  | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------:|----------:|------------:|
| &#39;RaycastClosest over 200 shapes&#39;     |    166.6 ns |   1.00 |         - |          NA |
| &#39;Raycast with callback, nearest&#39;     |    163.2 ns |   0.98 |         - |          NA |
| &#39;Raycast with callback, all hits&#39;    | 16,959.3 ns | 101.82 |         - |          NA |
| &#39;OverlapBox over the whole corridor&#39; |  1,724.5 ns |  10.35 |         - |          NA |
