```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8875)
Unknown processor
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-NMASDA : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=Job-NMASDA  IterationCount=15  WarmupCount=5  

```
| Method                     | Mean     | Ratio | Allocated | Alloc Ratio |
|--------------------------- |---------:|------:|----------:|------------:|
| &#39;Read position (native)&#39;   | 7.892 ns |  1.00 |         - |          NA |
| &#39;Read position (wrapper)&#39;  | 7.886 ns |  1.00 |         - |          NA |
| &#39;Write velocity (native)&#39;  | 6.044 ns |  0.77 |         - |          NA |
| &#39;Write velocity (wrapper)&#39; | 6.155 ns |  0.78 |         - |          NA |
| &#39;Apply force (native)&#39;     | 6.192 ns |  0.78 |         - |          NA |
| &#39;Apply force (wrapper)&#39;    | 6.691 ns |  0.85 |         - |          NA |
