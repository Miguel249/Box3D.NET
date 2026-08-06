// SPDX-License-Identifier: MIT
// Mirror of the debug draw types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/// <summary>
/// Describes a shape to the application so it can build draw resources.
/// Mirror of <c>b3DebugShape</c>.
/// </summary>
/// <remarks>
/// Passed to the create-debug-shape callback. <see cref="type"/> selects which
/// member of the union is valid; all of them are borrowed pointers into
/// Box3D-owned memory.
/// </remarks>
[StructLayout(LayoutKind.Explicit)]
public unsafe struct b3DebugShape
{
    /// <summary>The shape being described.</summary>
    [FieldOffset(0)]
    public b3ShapeId shapeId;

    /// <summary>The shape type, which selects the valid union member.</summary>
    [FieldOffset(8)]
    public b3ShapeType type;

    /// <summary>The capsule, valid when <see cref="type"/> is <see cref="b3ShapeType.b3_capsuleShape"/>.</summary>
    [FieldOffset(16)]
    public b3Capsule* capsule;

    /// <summary>The compound, valid when <see cref="type"/> is <see cref="b3ShapeType.b3_compoundShape"/>.</summary>
    [FieldOffset(16)]
    public b3CompoundData* compound;

    /// <summary>The height field, valid when <see cref="type"/> is <see cref="b3ShapeType.b3_heightShape"/>.</summary>
    [FieldOffset(16)]
    public b3HeightFieldData* heightField;

    /// <summary>The hull, valid when <see cref="type"/> is <see cref="b3ShapeType.b3_hullShape"/>.</summary>
    [FieldOffset(16)]
    public b3HullData* hull;

    /// <summary>The scaled mesh, valid when <see cref="type"/> is <see cref="b3ShapeType.b3_meshShape"/>.</summary>
    [FieldOffset(16)]
    public b3Mesh* mesh;

    /// <summary>The sphere, valid when <see cref="type"/> is <see cref="b3ShapeType.b3_sphereShape"/>.</summary>
    [FieldOffset(16)]
    public b3Sphere* sphere;
}

/// <summary>
/// The callbacks and options used by <c>b3World_Draw</c>. Mirror of <c>b3DebugDraw</c>.
/// </summary>
/// <remarks>
/// <para>
/// Start from <c>B3.b3DefaultDebugDraw()</c>, then install the callbacks you
/// need and switch on the categories you want. Null callbacks are skipped.
/// </para>
/// <para>
/// Every callback receives world coordinates and the user context from
/// <see cref="context"/>. As with all callbacks in this binding they are
/// function pointers, so the target must be a static method marked
/// <c>[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]</c>, with any
/// per-instance state reached through <see cref="context"/>.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3DebugDraw
{
    /// <summary>
    /// Draws a shape previously created through the create-debug-shape callback.
    /// </summary>
    /// <remarks>
    /// Signature: <c>void (void* userShape, b3Transform transform, b3HexColor color, void* context)</c>.
    /// Only called for shapes that passed the cull test against <see cref="drawingBounds"/>.
    /// </remarks>
    public delegate* unmanaged[Cdecl]<void*, b3Transform, b3HexColor, void*, void> DrawShapeFcn;

    /// <summary>Draws a line segment.</summary>
    /// <remarks>Signature: <c>void (Vector3 p1, Vector3 p2, b3HexColor color, void* context)</c>.</remarks>
    public delegate* unmanaged[Cdecl]<Vector3, Vector3, b3HexColor, void*, void> DrawSegmentFcn;

    /// <summary>Draws a transform. The application chooses the axis length.</summary>
    /// <remarks>Signature: <c>void (b3Transform transform, void* context)</c>.</remarks>
    public delegate* unmanaged[Cdecl]<b3Transform, void*, void> DrawTransformFcn;

    /// <summary>Draws a point.</summary>
    /// <remarks>Signature: <c>void (Vector3 p, float size, b3HexColor color, void* context)</c>.</remarks>
    public delegate* unmanaged[Cdecl]<Vector3, float, b3HexColor, void*, void> DrawPointFcn;

    /// <summary>Draws a sphere.</summary>
    /// <remarks>Signature: <c>void (Vector3 p, float radius, b3HexColor color, float alpha, void* context)</c>.</remarks>
    public delegate* unmanaged[Cdecl]<Vector3, float, b3HexColor, float, void*, void> DrawSphereFcn;

    /// <summary>Draws a capsule.</summary>
    /// <remarks>Signature: <c>void (Vector3 p1, Vector3 p2, float radius, b3HexColor color, float alpha, void* context)</c>.</remarks>
    public delegate* unmanaged[Cdecl]<Vector3, Vector3, float, b3HexColor, float, void*, void> DrawCapsuleFcn;

    /// <summary>Draws an axis-aligned bounding box.</summary>
    /// <remarks>Signature: <c>void (b3AABB aabb, b3HexColor color, void* context)</c>.</remarks>
    public delegate* unmanaged[Cdecl]<b3AABB, b3HexColor, void*, void> DrawBoundsFcn;

    /// <summary>Draws an oriented box.</summary>
    /// <remarks>Signature: <c>void (Vector3 extents, b3Transform transform, b3HexColor color, void* context)</c>.</remarks>
    public delegate* unmanaged[Cdecl]<Vector3, b3Transform, b3HexColor, void*, void> DrawBoxFcn;

    /// <summary>Draws a string in world space.</summary>
    /// <remarks>Signature: <c>void (Vector3 p, byte* utf8Text, b3HexColor color, void* context)</c>.</remarks>
    public delegate* unmanaged[Cdecl]<Vector3, byte*, b3HexColor, void*, void> DrawStringFcn;

    /// <summary>The world bounds outside which nothing is drawn.</summary>
    public b3AABB drawingBounds;

    /// <summary>The scale applied when drawing forces.</summary>
    public float forceScale;

    /// <summary>The global scale applied when drawing joints.</summary>
    public float jointScale;

    /// <summary>Whether to draw shapes.</summary>
    public NativeBool drawShapes;

    /// <summary>Whether to draw joints.</summary>
    public NativeBool drawJoints;

    /// <summary>Whether to draw additional joint information.</summary>
    public NativeBool drawJointExtras;

    /// <summary>Whether to draw shape bounding boxes.</summary>
    public NativeBool drawBounds;

    /// <summary>Whether to draw the mass and centre of mass of dynamic bodies.</summary>
    public NativeBool drawMass;

    /// <summary>Whether to draw sleep state for dynamic and kinematic bodies.</summary>
    public NativeBool drawSleep;

    /// <summary>Whether to draw body names.</summary>
    public NativeBool drawBodyNames;

    /// <summary>Whether to draw contact points.</summary>
    public NativeBool drawContacts;

    /// <summary>Whether to draw contact anchor A rather than anchor B.</summary>
    public NativeBool drawAnchorA;

    /// <summary>Whether to colour constraints by their graph colour.</summary>
    public NativeBool drawGraphColors;

    /// <summary>Whether to draw contact features.</summary>
    public NativeBool drawContactFeatures;

    /// <summary>Whether to draw contact normals.</summary>
    public NativeBool drawContactNormals;

    /// <summary>Whether to draw contact normal forces.</summary>
    public NativeBool drawContactForces;

    /// <summary>Whether to draw islands as bounding boxes.</summary>
    public NativeBool drawIslands;

    /// <summary>The user context passed to every drawing callback.</summary>
    public void* context;
}
