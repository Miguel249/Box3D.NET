// SPDX-License-Identifier: MIT

using System;
using Box3D.Native;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Reports whether the native Box3D library can be loaded in this process.
/// </summary>
/// <remarks>
/// A fresh clone has no native binary until <c>tools/build-native.ps1</c> has
/// run, and building it needs CMake and a C compiler. Tests that call into the
/// library skip themselves in that case instead of failing, so that
/// <c>dotnet test</c> is useful immediately after cloning. Continuous
/// integration always stages a binary first, so nothing is silently skipped
/// there.
/// </remarks>
internal static class NativeLibrary
{
    private static readonly Lazy<string?> LoadFailure = new(Probe);

    /// <summary>Gets a value indicating whether the native library is available.</summary>
    public static bool IsAvailable => LoadFailure.Value is null;

    /// <summary>Gets the reason the library could not be loaded, or null when it loaded.</summary>
    public static string? Failure => LoadFailure.Value;

    private static string? Probe()
    {
        try
        {
            // Any exported function will do. This one has no side effects and
            // does not require a world.
            _ = B3.b3GetVersion();
            return null;
        }
        catch (DllNotFoundException ex)
        {
            return $"the native Box3D library was not found: {ex.Message}";
        }
        catch (EntryPointNotFoundException ex)
        {
            return $"the native Box3D library is missing an expected export: {ex.Message}";
        }
        catch (BadImageFormatException ex)
        {
            return $"the native Box3D library has the wrong architecture: {ex.Message}";
        }
    }
}

/// <summary>
/// A test that needs the native Box3D library, and is skipped when it is absent.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class NativeFactAttribute : FactAttribute
{
    /// <summary>Initializes a new instance of the <see cref="NativeFactAttribute"/> class.</summary>
    public NativeFactAttribute()
    {
        if (!NativeLibrary.IsAvailable)
        {
            Skip = $"Skipped because {NativeLibrary.Failure}. Run tools/build-native.ps1 to build it.";
        }
    }
}

/// <summary>
/// A data-driven test that needs the native Box3D library, and is skipped when
/// it is absent.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class NativeTheoryAttribute : TheoryAttribute
{
    /// <summary>Initializes a new instance of the <see cref="NativeTheoryAttribute"/> class.</summary>
    public NativeTheoryAttribute()
    {
        if (!NativeLibrary.IsAvailable)
        {
            Skip = $"Skipped because {NativeLibrary.Failure}. Run tools/build-native.ps1 to build it.";
        }
    }
}
