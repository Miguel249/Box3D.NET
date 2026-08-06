```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8875)
Unknown processor
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-BGKROB : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=Job-BGKROB  InvocationCount=16  IterationCount=25  
WarmupCount=8  

```
| Method                                             | Mean     | Ratio | Allocated | Alloc Ratio |
|--------------------------------------------------- |---------:|------:|----------:|------------:|
| &#39;1000 bodies + spheres (native)&#39;                   | 710.6 μs |  1.00 |         - |          NA |
| &#39;1000 bodies + spheres (wrapper)&#39;                  | 799.3 μs |  1.13 |         - |          NA |
| &#39;1000 bodies + spheres (wrapper, defaults inline)&#39; | 796.3 μs |  1.12 |         - |          NA |
