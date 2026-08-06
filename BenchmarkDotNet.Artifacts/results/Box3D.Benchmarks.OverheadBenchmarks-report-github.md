```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8875)
Unknown processor
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-MGIMIG : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=Job-MGIMIG  IterationCount=20  WarmupCount=6  

```
| Method                                   | Mean     | Ratio | Allocated | Alloc Ratio |
|----------------------------------------- |---------:|------:|----------:|------------:|
| &#39;Read position (native)&#39;                 | 7.897 ns |  1.00 |         - |          NA |
| &#39;Read position (wrapper)&#39;                | 8.104 ns |  1.03 |         - |          NA |
| &#39;Write velocity (native)&#39;                | 6.396 ns |  0.81 |         - |          NA |
| &#39;Write velocity (wrapper)&#39;               | 6.651 ns |  0.84 |         - |          NA |
| &#39;Write velocity (native + finite check)&#39; | 6.508 ns |  0.82 |         - |          NA |
| &#39;Apply force (native)&#39;                   | 6.273 ns |  0.79 |         - |          NA |
| &#39;Apply force (wrapper)&#39;                  | 6.776 ns |  0.86 |         - |          NA |
