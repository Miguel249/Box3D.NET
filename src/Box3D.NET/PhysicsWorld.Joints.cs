// SPDX-License-Identifier: MIT

using System;
using Box3D.Native;

namespace Box3D;

/*
 * Joint creation.
 *
 * Each joint type gets its own method returning its own handle type, rather than
 * one CreateJoint returning a generic Joint. The specific types are what carry
 * the limits, motors and springs, so the alternative would hand back something
 * that has to be narrowed before it is useful, and would allow a distance joint
 * definition to produce a handle whose revolute members compile and then assert
 * at run time.
 */

public sealed partial class PhysicsWorld
{
    /// <summary>Creates a hinge.</summary>
    /// <param name="definition">The joint settings.</param>
    /// <returns>The new joint.</returns>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <exception cref="ArgumentException">The definition does not name two distinct, valid bodies.</exception>
    /// <example>
    /// <code>
    /// var hinge = world.CreateRevoluteJoint(
    ///     RevoluteJointDefinition.Hinge(frame, door, hingePoint, Vector3.UnitY) with
    ///     {
    ///         LimitsEnabled = true,
    ///         LowerAngle = 0.0f,
    ///         UpperAngle = MathF.PI * 0.5f,
    ///     });
    /// </code>
    /// </example>
    public unsafe RevoluteJoint CreateRevoluteJoint(RevoluteJointDefinition definition)
    {
        ThrowIfDisposed();
        ValidateBodies(definition.Base);

        b3RevoluteJointDef def = definition.ToNative();
        return new RevoluteJoint(B3.b3CreateRevoluteJoint(_id, &def));
    }

    /// <summary>Creates a slider.</summary>
    /// <param name="definition">The joint settings.</param>
    /// <returns>The new joint.</returns>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <exception cref="ArgumentException">The definition does not name two distinct, valid bodies.</exception>
    public unsafe PrismaticJoint CreatePrismaticJoint(PrismaticJointDefinition definition)
    {
        ThrowIfDisposed();
        ValidateBodies(definition.Base);

        b3PrismaticJointDef def = definition.ToNative();
        return new PrismaticJoint(B3.b3CreatePrismaticJoint(_id, &def));
    }

    /// <summary>Creates a distance joint, the basis for ropes and springs.</summary>
    /// <param name="definition">The joint settings.</param>
    /// <returns>The new joint.</returns>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <exception cref="ArgumentException">The definition does not name two distinct, valid bodies.</exception>
    public unsafe DistanceJoint CreateDistanceJoint(DistanceJointDefinition definition)
    {
        ThrowIfDisposed();
        ValidateBodies(definition.Base);

        b3DistanceJointDef def = definition.ToNative();
        return new DistanceJoint(B3.b3CreateDistanceJoint(_id, &def));
    }

    /// <summary>Creates a ball-in-socket joint.</summary>
    /// <param name="definition">The joint settings.</param>
    /// <returns>The new joint.</returns>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <exception cref="ArgumentException">The definition does not name two distinct, valid bodies.</exception>
    public unsafe SphericalJoint CreateSphericalJoint(SphericalJointDefinition definition)
    {
        ThrowIfDisposed();
        ValidateBodies(definition.Base);

        b3SphericalJointDef def = definition.ToNative();
        return new SphericalJoint(B3.b3CreateSphericalJoint(_id, &def));
    }

    /// <summary>Creates a weld joint, holding two bodies rigidly together.</summary>
    /// <param name="definition">The joint settings.</param>
    /// <returns>The new joint.</returns>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <exception cref="ArgumentException">The definition does not name two distinct, valid bodies.</exception>
    public unsafe WeldJoint CreateWeldJoint(WeldJointDefinition definition)
    {
        ThrowIfDisposed();
        ValidateBodies(definition.Base);

        b3WeldJointDef def = definition.ToNative();
        return new WeldJoint(B3.b3CreateWeldJoint(_id, &def));
    }

    /// <summary>Creates a wheel joint with suspension, spin and optional steering.</summary>
    /// <param name="definition">The joint settings.</param>
    /// <returns>The new joint.</returns>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <exception cref="ArgumentException">The definition does not name two distinct, valid bodies.</exception>
    public unsafe WheelJoint CreateWheelJoint(WheelJointDefinition definition)
    {
        ThrowIfDisposed();
        ValidateBodies(definition.Base);

        b3WheelJointDef def = definition.ToNative();
        return new WheelJoint(B3.b3CreateWheelJoint(_id, &def));
    }

    /// <summary>Creates a motor joint, driving the relative pose and velocity of two bodies.</summary>
    /// <param name="definition">The joint settings.</param>
    /// <returns>The new joint.</returns>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <exception cref="ArgumentException">The definition does not name two distinct, valid bodies.</exception>
    public unsafe MotorJoint CreateMotorJoint(MotorJointDefinition definition)
    {
        ThrowIfDisposed();
        ValidateBodies(definition.Base);

        b3MotorJointDef def = definition.ToNative();
        return new MotorJoint(B3.b3CreateMotorJoint(_id, &def));
    }

    /// <summary>Creates a parallel joint, keeping two frames' z axes aligned.</summary>
    /// <param name="definition">The joint settings.</param>
    /// <returns>The new joint.</returns>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <exception cref="ArgumentException">The definition does not name two distinct, valid bodies.</exception>
    public unsafe ParallelJoint CreateParallelJoint(ParallelJointDefinition definition)
    {
        ThrowIfDisposed();
        ValidateBodies(definition.Base);

        b3ParallelJointDef def = definition.ToNative();
        return new ParallelJoint(B3.b3CreateParallelJoint(_id, &def));
    }

    /// <summary>Creates a filter joint, stopping two bodies from colliding.</summary>
    /// <param name="definition">The joint settings.</param>
    /// <returns>The new joint.</returns>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <exception cref="ArgumentException">The definition does not name two distinct, valid bodies.</exception>
    public unsafe FilterJoint CreateFilterJoint(FilterJointDefinition definition)
    {
        ThrowIfDisposed();
        ValidateBodies(definition.Base);

        b3FilterJointDef def = definition.ToNative();
        return new FilterJoint(B3.b3CreateFilterJoint(_id, &def));
    }

    /*
     * Box3D asserts on these rather than returning an error, and an assert in a
     * release build of the native library is compiled out, so the call would
     * carry on with a null id and fail somewhere less obvious. Checking here
     * turns three easy mistakes into an exception naming the problem.
     */
    private static void ValidateBodies(in JointDefinition definition)
    {
        if (!definition.BodyA.IsValid)
        {
            throw new ArgumentException("The first body of the joint is null or has been destroyed.", nameof(definition));
        }

        if (!definition.BodyB.IsValid)
        {
            throw new ArgumentException("The second body of the joint is null or has been destroyed.", nameof(definition));
        }

        if (definition.BodyA.NativeId == definition.BodyB.NativeId)
        {
            throw new ArgumentException("A joint must connect two different bodies.", nameof(definition));
        }
    }
}
