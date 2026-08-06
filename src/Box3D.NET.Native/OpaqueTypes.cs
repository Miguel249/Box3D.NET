// SPDX-License-Identifier: MIT
// Opaque handle types declared but never defined in the Box3D headers.

namespace Box3D.Native;

/*
 * Box3D forward-declares these two as incomplete struct types, so their layout
 * is private and they are only ever handled through a pointer. They are
 * declared here as empty structs purely so that b3Recording* and b3RecPlayer*
 * are distinct, checked types rather than bare void*.
 *
 * Never allocate one of these on the stack or dereference it. Instances come
 * only from the create and load functions, and must be released with the
 * matching destroy function.
 */

/// <summary>
/// An opaque recording buffer. Mirror of the incomplete type <c>b3Recording</c>.
/// </summary>
/// <remarks>
/// Only ever used as <c>b3Recording*</c>. Create one with
/// <see cref="B3.b3CreateRecording"/> or <see cref="B3.b3LoadRecordingFromFile"/>,
/// and release it with <see cref="B3.b3DestroyRecording"/>.
/// </remarks>
public struct b3Recording
{
}

/// <summary>
/// An opaque incremental replay player. Mirror of the incomplete type <c>b3RecPlayer</c>.
/// </summary>
/// <remarks>
/// Only ever used as <c>b3RecPlayer*</c>. Create one with
/// <see cref="B3.b3RecPlayer_Create"/> and release it with
/// <see cref="B3.b3RecPlayer_Destroy"/>. The player owns a private copy of the
/// recording bytes and drives its own replay world.
/// </remarks>
public struct b3RecPlayer
{
}
