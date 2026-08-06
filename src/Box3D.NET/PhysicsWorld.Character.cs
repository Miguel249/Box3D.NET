// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Box3D.Native;

namespace Box3D;

public sealed unsafe partial class PhysicsWorld
{
    /*
     * Same shape as the query callbacks: a non-generic thunk reads the caller's
     * struct and a managed function pointer out of the context and calls
     * through, because [UnmanagedCallersOnly] cannot be generic.
     *
     * The mover callback differs in one way that matters. Box3D reports an
     * array of planes per shape rather than one at a time, so the thunk loops
     * over them and stops early if the caller says so.
     */

    private struct CharacterContext
    {
        public void* Callback;
        public delegate*<void*, in CharacterContact, bool> Invoke;
    }

    private static bool InvokeCharacter<TCallback>(void* callback, in CharacterContact contact)
        where TCallback : struct, ICharacterCollisionCallback =>
        Unsafe.AsRef<TCallback>(callback).OnContact(in contact);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static NativeBool CharacterThunk(
        b3ShapeId shapeId,
        b3PlaneResult* planes,
        int planeCount,
        void* context)
    {
        ref CharacterContext ctx = ref Unsafe.AsRef<CharacterContext>(context);
        Shape shape = new(shapeId);

        for (int i = 0; i < planeCount; i++)
        {
            ref readonly b3PlaneResult plane = ref planes[i];

            CharacterContact contact = new()
            {
                Shape = shape,
                Normal = plane.plane.normal,
                Offset = plane.plane.offset,
                Point = plane.point,
            };

            if (!ctx.Invoke(ctx.Callback, in contact))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Finds every surface a character capsule is touching, for the plane solver.
    /// </summary>
    /// <typeparam name="TCallback">
    /// The callback type. A struct, so gathering contacts every frame allocates nothing.
    /// </typeparam>
    /// <param name="capsule">The character capsule, relative to <paramref name="origin"/>.</param>
    /// <param name="origin">The world position the capsule is measured from.</param>
    /// <param name="callback">The callback, passed by reference so what it records survives the call.</param>
    /// <param name="filter">Which shapes the capsule collides with, or null for everything.</param>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This is the first step of kinematic character movement. Feed the contacts
    /// to <see cref="CharacterMover.SolvePlanes"/> to find where the character
    /// can actually go.
    /// </para>
    /// <para>
    /// Prefer this over <see cref="CastCapsule"/> for finding out what the
    /// character is touching: a cast tells you where it stops, these planes tell
    /// you what is in the way and in which direction.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var gather = new GatherPlanes { Planes = planeBuffer };
    /// world.CollideCapsule(capsule, position, ref gather);
    ///
    /// var result = CharacterMover.SolvePlanes(desiredMove, planeBuffer.AsSpan(0, gather.Count));
    /// position += result.Translation;
    /// velocity = CharacterMover.ClipVelocity(velocity, planeBuffer.AsSpan(0, gather.Count));
    /// </code>
    /// </example>
    public void CollideCapsule<TCallback>(
        Capsule capsule,
        Vector3 origin,
        ref TCallback callback,
        QueryFilter? filter = null)
        where TCallback : struct, ICharacterCollisionCallback
    {
        ThrowIfDisposed();

        CharacterContext context = new()
        {
            Callback = Unsafe.AsPointer(ref callback),
            Invoke = &InvokeCharacter<TCallback>,
        };

        b3Capsule native = capsule.ToNative();

        B3.b3World_CollideMover(
            _id,
            origin,
            &native,
            (filter ?? QueryFilter.Default).ToNative(),
            &CharacterThunk,
            &context);
    }

    /// <summary>
    /// Sweeps a character capsule through the world and reports how far it got.
    /// </summary>
    /// <param name="capsule">The capsule, relative to <paramref name="origin"/>.</param>
    /// <param name="origin">The world position the capsule is measured from.</param>
    /// <param name="translation">The movement to attempt.</param>
    /// <param name="filter">Which shapes block the capsule, or null for everything.</param>
    /// <returns>
    /// The fraction of <paramref name="translation"/> the capsule could travel,
    /// from zero for blocked immediately to one for unobstructed.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <remarks>
    /// A specialised shape cast that slides along surfaces instead of catching on
    /// them. Useful for a quick "can the character get there" test, but a poor
    /// source of information about what it is touching; use
    /// <see cref="CollideCapsule{TCallback}"/> for that.
    /// </remarks>
    public float CastCapsule(
        Capsule capsule,
        Vector3 origin,
        Vector3 translation,
        QueryFilter? filter = null)
    {
        ThrowIfDisposed();

        b3Capsule native = capsule.ToNative();

        return B3.b3World_CastMover(
            _id,
            origin,
            &native,
            translation,
            (filter ?? QueryFilter.Default).ToNative(),
            null,
            null);
    }
}
