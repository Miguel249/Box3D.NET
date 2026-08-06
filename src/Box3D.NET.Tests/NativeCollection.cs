// SPDX-License-Identifier: MIT

using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Groups every test that touches the native library so they never run
/// concurrently with each other.
/// </summary>
/// <remarks>
/// <para>
/// xUnit runs test classes in parallel by default. That is fine for tests over
/// pure managed code, but Box3D keeps process-wide state: the allocated byte
/// count, the live world count and the length unit scale are all global. A test
/// that measures allocation before and after an operation is measuring every
/// other thread's allocations too.
/// </para>
/// <para>
/// This surfaced as a leak test that passed alone and failed in the suite, which
/// is the worst way for it to fail: intermittent, and easy to dismiss as flaky
/// when it is in fact reporting that the measurement is unsound.
/// </para>
/// <para>
/// Classes in a single xUnit collection run one at a time, so joining this
/// collection makes those measurements meaningful. Tests that never call into
/// the native library stay out of it and keep running in parallel.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NativeCollection
{
    /// <summary>The collection name, referenced by <see cref="CollectionAttribute"/>.</summary>
    public const string Name = "Box3D native library";
}
