// SPDX-License-Identifier: MIT
// Names Box3D has since renamed, kept so that code written against an earlier
// version of this binding still compiles.

using System;
using System.ComponentModel;

namespace Box3D.Native;

/*
 * Only pure renames belong here: a function whose C declaration is unchanged
 * apart from its name, so forwarding to the new name is exactly the old call.
 * A function whose behaviour or signature changed is not shimmed, because a
 * shim would claim a compatibility the native library no longer offers.
 */

public static unsafe partial class B3
{
    /// <summary>Creates an incremental replay player. Renamed by Box3D to <see cref="b3CreatePlayer"/>.</summary>
    /// <param name="data">The recording bytes.</param>
    /// <param name="size">The number of recording bytes.</param>
    /// <param name="workerCount">The number of workers the replay world steps with.</param>
    /// <returns>A new player, or null on a bad header or a deserialization failure.</returns>
    [Obsolete("Box3D renamed this function to b3CreatePlayer. This forwarding name will be removed in a future release.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static b3RecPlayer* b3RecPlayer_Create(void* data, int size, int workerCount) =>
        b3CreatePlayer(data, size, workerCount);

    /// <summary>Destroys a replay player. Renamed by Box3D to <see cref="b3DestroyPlayer"/>.</summary>
    /// <param name="player">The player to destroy.</param>
    [Obsolete("Box3D renamed this function to b3DestroyPlayer. This forwarding name will be removed in a future release.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void b3RecPlayer_Destroy(b3RecPlayer* player) => b3DestroyPlayer(player);
}
