// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Box3D.Native;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Holds every managed struct to the layout the C compiler reports for its
/// Box3D counterpart.
/// </summary>
/// <remarks>
/// <para>
/// The structs in Box3D.NET.Native are hand-written mirrors of C declarations,
/// and nothing about C# forces the two to agree. A field of the wrong width, or
/// two fields swapped, still compiles and still runs: the call succeeds and
/// reads the wrong bytes, so a body ends up with its restitution in the
/// friction slot. There is no crash to investigate and no test failure to
/// notice, which makes it the worst class of bug a binding can have.
/// </para>
/// <para>
/// <see cref="LayoutTests"/> pins a handful of sizes to constants worked out by
/// hand from the headers. That is worth keeping — it runs with no native
/// library and no toolchain — but it only covers the types someone remembered,
/// and the constants are a second copy of the very thing under test. These
/// tests instead compare against <c>abi/native-layout.json</c>, which
/// <c>tools/dump-abi.ps1</c> generates by compiling a program against the real
/// Box3D headers and printing <c>sizeof</c>, <c>_Alignof</c> and
/// <c>offsetof</c>. CI regenerates that file, so a submodule bump that moves a
/// field fails the build instead of shipping.
/// </para>
/// </remarks>
public class AbiTests
{
    private static readonly Lazy<NativeAbi> Abi = new(Load);

    /// <summary>The managed assembly holding the mirrors.</summary>
    private static Assembly NativeAssembly => typeof(b3WorldDef).Assembly;

    [Fact]
    public void The_recorded_abi_is_present_and_not_empty()
    {
        // Guards against the resource silently vanishing from the csproj, which
        // would otherwise turn every test below into a vacuous pass.
        Assert.NotEmpty(Abi.Value.Structs);
        Assert.True(Abi.Value.Structs.Count >= 90, $"only {Abi.Value.Structs.Count} structs recorded; the dump looks truncated");
    }

    [Fact]
    public void Every_managed_struct_has_the_size_C_reports()
    {
        var failures = new List<string>();

        foreach ((string name, NativeStruct native) in Abi.Value.Structs)
        {
            Type? managed = NativeAssembly.GetType($"Box3D.Native.{name}");
            if (managed is null)
            {
                // Absence is reported separately, so that a missing mirror is
                // one failure rather than one per field.
                continue;
            }

            int actual = SizeOf(managed);
            if (actual != native.Size)
            {
                failures.Add($"{name}: C says {native.Size} bytes, managed is {actual}");
            }
        }

        Assert.True(failures.Count == 0, Describe("These structs do not match the size the C compiler reports:", failures));
    }

    [Fact]
    public void Every_managed_field_sits_where_C_puts_it()
    {
        var failures = new List<string>();

        foreach ((string name, NativeStruct native) in Abi.Value.Structs)
        {
            Type? managed = NativeAssembly.GetType($"Box3D.Native.{name}");
            if (managed is null)
            {
                continue;
            }

            foreach ((string field, int expected) in native.Fields)
            {
                FieldInfo? info = managed.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (info is null)
                {
                    failures.Add($"{name}.{field}: absent from the managed struct (C puts it at offset {expected})");
                    continue;
                }

                int actual = (int)Marshal.OffsetOf(managed, field);
                if (actual != expected)
                {
                    failures.Add($"{name}.{field}: C offset {expected}, managed offset {actual}");
                }
            }
        }

        Assert.True(failures.Count == 0, Describe("These fields are not where the C compiler puts them:", failures));
    }

    [Fact]
    public void Every_managed_struct_is_blittable()
    {
        // A struct needing marshalling is one the runtime will copy and rewrite
        // on every call, which is both a silent cost and a silent layout change.
        // Marshal.SizeOf reports the marshalled size; Unsafe.SizeOf reports the
        // real one. They agree only when no marshalling is involved.
        var failures = new List<string>();

        foreach (string name in Abi.Value.Structs.Keys)
        {
            Type? managed = NativeAssembly.GetType($"Box3D.Native.{name}");
            if (managed is null)
            {
                continue;
            }

            int unmanagedSize;
            try
            {
                unmanagedSize = Marshal.SizeOf(managed);
            }
            catch (ArgumentException)
            {
                failures.Add($"{name}: cannot be marshalled at all, so it holds a managed field");
                continue;
            }

            int realSize = SizeOf(managed);
            if (unmanagedSize != realSize)
            {
                failures.Add($"{name}: marshalled size {unmanagedSize} differs from actual size {realSize}");
            }
        }

        Assert.True(failures.Count == 0, Describe("These structs are not blittable:", failures));
    }

    [Fact]
    public void Every_struct_C_declares_has_a_managed_mirror()
    {
        // Coverage, stated as a test. A Box3D update that adds a struct shows up
        // here rather than being noticed whenever someone happens to need it.
        var missing = Abi.Value.Structs.Keys
            .Where(name => NativeAssembly.GetType($"Box3D.Native.{name}") is null)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            Describe(
                $"{missing.Count} struct(s) declared by Box3D have no managed mirror. Add them, or record here why they are deliberately unbound:",
                missing));
    }

    private static string Describe(string headline, IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine(headline);
        foreach (string line in lines)
        {
            sb.Append("  ").AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine("abi/native-layout.json records what the C compiler reports. Regenerate it with");
        sb.AppendLine("tools/dump-abi.ps1 after a submodule bump; otherwise the managed struct is wrong.");
        return sb.ToString();
    }

    private static int SizeOf(Type type) =>
        (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!.MakeGenericMethod(type).Invoke(null, null)!;

    private static NativeAbi Load()
    {
        using Stream? stream = typeof(AbiTests).Assembly.GetManifestResourceStream("Box3D.Tests.native-layout.json")
            ?? throw new InvalidOperationException(
                "native-layout.json is not embedded in the test assembly. It is added by Box3D.NET.Tests.csproj; " +
                "generate it with tools/dump-abi.ps1 if it is missing from the repository.");

        return JsonSerializer.Deserialize<NativeAbi>(stream, JsonOptions)
            ?? throw new InvalidOperationException("native-layout.json deserialised to null.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class NativeAbi
    {
        public Dictionary<string, NativeStruct> Structs { get; set; } = new();
    }

    private sealed class NativeStruct
    {
        public int Size { get; set; }

        public int Align { get; set; }

        public Dictionary<string, int> Fields { get; set; } = new();
    }
}
