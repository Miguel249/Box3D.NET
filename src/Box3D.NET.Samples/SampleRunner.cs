// SPDX-License-Identifier: MIT

using System;
using Box3D.Native;

namespace Box3D.Samples;

/// <summary>
/// Runs a sample and reports whether it met its own expectations.
/// </summary>
/// <remarks>
/// The samples assert on their results rather than only printing them, so that
/// running them in continuous integration catches a regression rather than
/// producing plausible-looking output nobody reads.
/// </remarks>
internal static class SampleRunner
{
    /// <summary>Gets the version of the native library the samples are running against.</summary>
    public static string NativeVersion
    {
        get
        {
            b3Version version = B3.b3GetVersion();
            return $"{version.major}.{version.minor}.{version.revision}";
        }
    }

    /// <summary>Runs one sample, printing its name and output.</summary>
    /// <param name="name">The sample name.</param>
    /// <param name="sample">The sample body.</param>
    public static void Run(string name, Action sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        Console.WriteLine($"== {name}");
        sample();
        Console.WriteLine();
    }

    /// <summary>Fails the sample when a condition does not hold.</summary>
    /// <param name="condition">The condition that must hold.</param>
    /// <param name="message">What was expected.</param>
    /// <exception cref="InvalidOperationException">The condition did not hold.</exception>
    public static void Expect(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Sample expectation failed: {message}");
        }
    }
}
