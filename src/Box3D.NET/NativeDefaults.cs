// SPDX-License-Identifier: MIT

using Box3D.Native;

namespace Box3D;

/// <summary>
/// The definition structures Box3D fills in with its own defaults, read once.
/// </summary>
/// <remarks>
/// <para>
/// Every definition type in this library starts from the engine's defaults so
/// that it tracks Box3D rather than hard-coding a copy that silently drifts.
/// The obvious way to do that is to call <c>b3DefaultBodyDef</c> whenever one is
/// needed, but that turns out to cost real time: creating a body was three
/// P/Invokes rather than one, because the definition fetched the defaults, the
/// conversion to native fetched them again, and only the third call created
/// anything. Measured over a thousand bodies that was 63% slower than calling
/// the C API directly.
/// </para>
/// <para>
/// These structures are constant for the life of the process, so they are read
/// once into static fields and copied thereafter. Copying a definition struct
/// is a handful of bytes and no transition.
/// </para>
/// <para>
/// The one thing that changes them is <c>b3SetLengthUnitsPerMeter</c>, which
/// Box3D already requires be called before anything else in the library. These
/// fields are initialized on first use, so respecting that contract is enough.
/// <c>NativeDefaultsTests</c> asserts that each cached value still matches what
/// the engine returns.
/// </para>
/// </remarks>
internal static class NativeDefaults
{
    /// <summary>The default world definition.</summary>
    internal static readonly b3WorldDef World = B3.b3DefaultWorldDef();

    /// <summary>The default body definition.</summary>
    internal static readonly b3BodyDef Body = B3.b3DefaultBodyDef();

    /// <summary>The default shape definition.</summary>
    internal static readonly b3ShapeDef Shape = B3.b3DefaultShapeDef();

    /// <summary>The default surface material.</summary>
    internal static readonly b3SurfaceMaterial SurfaceMaterial = B3.b3DefaultSurfaceMaterial();

    /// <summary>The default collision filter.</summary>
    internal static readonly b3Filter Filter = B3.b3DefaultFilter();

    /// <summary>The default query filter.</summary>
    internal static readonly b3QueryFilter QueryFilter = B3.b3DefaultQueryFilter();

    /// <summary>The default explosion definition.</summary>
    internal static readonly b3ExplosionDef Explosion = B3.b3DefaultExplosionDef();

    /// <summary>The default revolute joint definition.</summary>
    internal static readonly b3RevoluteJointDef RevoluteJoint = B3.b3DefaultRevoluteJointDef();

    /// <summary>The default prismatic joint definition.</summary>
    internal static readonly b3PrismaticJointDef PrismaticJoint = B3.b3DefaultPrismaticJointDef();

    /// <summary>The default distance joint definition.</summary>
    internal static readonly b3DistanceJointDef DistanceJoint = B3.b3DefaultDistanceJointDef();

    /// <summary>The default spherical joint definition.</summary>
    internal static readonly b3SphericalJointDef SphericalJoint = B3.b3DefaultSphericalJointDef();

    /// <summary>The default weld joint definition.</summary>
    internal static readonly b3WeldJointDef WeldJoint = B3.b3DefaultWeldJointDef();

    /// <summary>The default wheel joint definition.</summary>
    internal static readonly b3WheelJointDef WheelJoint = B3.b3DefaultWheelJointDef();

    /// <summary>The default motor joint definition.</summary>
    internal static readonly b3MotorJointDef MotorJoint = B3.b3DefaultMotorJointDef();

    /// <summary>The default parallel joint definition.</summary>
    internal static readonly b3ParallelJointDef ParallelJoint = B3.b3DefaultParallelJointDef();

    /// <summary>The default filter joint definition.</summary>
    internal static readonly b3FilterJointDef FilterJoint = B3.b3DefaultFilterJointDef();

    /// <summary>The shared base of every joint definition.</summary>
    /// <remarks>Every <c>b3Default*JointDef</c> fills in the same base, so any of them serves.</remarks>
    internal static readonly b3JointDef Joint = RevoluteJoint.@base;
}
