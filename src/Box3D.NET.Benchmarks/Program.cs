// SPDX-License-Identifier: MIT

using BenchmarkDotNet.Running;

// Run everything:            dotnet run -c Release --project src/Box3D.NET.Benchmarks
// Run one class:             dotnet run -c Release --project src/Box3D.NET.Benchmarks -- --filter *BodyCreation*
// List what is available:    dotnet run -c Release --project src/Box3D.NET.Benchmarks -- --list flat

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

/// <summary>Entry point marker, so the assembly can be located above.</summary>
internal sealed partial class Program;
