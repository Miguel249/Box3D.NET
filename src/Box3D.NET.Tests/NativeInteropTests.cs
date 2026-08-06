// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Exercises the binding against the real Box3D library.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LayoutTests"/> checks the struct layouts against values derived
/// from the C declarations. These tests check the same thing the only way that
/// is conclusive: by asking the native library for a structure and confirming
/// the fields land where the binding expects them.
/// </para>
/// <para>
/// Every test here is skipped when no native binary has been staged. See
/// <see cref="NativeFactAttribute"/>.
/// </para>
/// </remarks>
[Collection(NativeCollection.Name)]
public unsafe class NativeInteropTests
{
    [NativeFact]
    public void The_library_reports_a_version()
    {
        b3Version version = B3.b3GetVersion();

        // Any released Box3D has a non-negative version triple, and at least one
        // component is non-zero. A zeroed result would mean the call never
        // reached the library.
        Assert.True(version.major >= 0);
        Assert.True(version.minor >= 0);
        Assert.True(version.revision >= 0);
        Assert.True(version.major + version.minor + version.revision > 0);
    }

    /*
     * The binding assumes the single-precision ABI throughout: b3Pos is b3Vec3
     * and b3WorldTransform is b3Transform. Against a double-precision build
     * every structure layout here is wrong, so this is checked first.
     */

    [NativeFact]
    public void The_library_is_built_in_single_precision()
    {
        Assert.False(
            B3.b3IsDoublePrecision(),
            "Box3D.NET targets the single-precision build. This library was built with BOX3D_DOUBLE_PRECISION, " +
            "which changes the ABI and invalidates every struct layout in the binding.");
    }

    /*
     * The strongest layout check available. b3DefaultWorldDef is filled in by
     * the native library, so if the managed struct disagreed about field order,
     * padding or the width of a bool, these values would land in the wrong
     * fields or be garbage.
     */

    [NativeFact]
    public void DefaultWorldDef_comes_back_with_sane_values()
    {
        b3WorldDef def = B3.b3DefaultWorldDef();

        Assert.True(def.internalValue != 0, "internalValue is the marker Box3D uses to accept the definition");
        Assert.True(def.contactHertz > 0.0f, $"contactHertz was {def.contactHertz}");
        Assert.True(def.contactDampingRatio > 0.0f, $"contactDampingRatio was {def.contactDampingRatio}");
        Assert.True(def.restitutionThreshold > 0.0f, $"restitutionThreshold was {def.restitutionThreshold}");
        Assert.True(def.maximumLinearSpeed > 0.0f, $"maximumLinearSpeed was {def.maximumLinearSpeed}");

        // The default is zero, not one: b3CreateWorld clamps the count into
        // [1, B3_MAX_WORKERS], so the definition leaves it unset and the world
        // ends up single-threaded. WorldSettings.ToNative applies the same clamp
        // so that a caller who never touches it gets the documented behaviour.
        Assert.True(def.workerCount <= Constants.B3_MAX_WORKERS, $"workerCount was {def.workerCount}");

        // The booleans sit between floats and a uint in the C layout, so a
        // four-byte bool would shift workerCount and corrupt these.
        Assert.True(def.enableSleep);
        Assert.True(def.enableContinuous);
    }

    [NativeFact]
    public void DefaultBodyDef_comes_back_with_sane_values()
    {
        b3BodyDef def = B3.b3DefaultBodyDef();

        Assert.True(def.internalValue != 0);
        Assert.Equal(b3BodyType.b3_staticBody, def.type);
        Assert.Equal(Quaternion.Identity, def.rotation);
        Assert.Equal(Vector3.Zero, def.position);
        Assert.Equal(1.0f, def.gravityScale);
        Assert.True(def.enableSleep);
        Assert.True(def.isAwake);
        Assert.True(def.isEnabled);
        Assert.False(def.isBullet);
    }

    [NativeFact]
    public void DefaultShapeDef_comes_back_with_sane_values()
    {
        b3ShapeDef def = B3.b3DefaultShapeDef();

        Assert.True(def.internalValue != 0);
        Assert.True(def.density > 0.0f, $"density was {def.density}");
        Assert.True(def.updateBodyMass);
        Assert.False(def.isSensor);

        // The filter follows a bool and two pointers; a shifted layout shows up
        // here as zero mask bits, which would silently disable all collision.
        Assert.Equal(ulong.MaxValue, def.filter.categoryBits);
        Assert.Equal(ulong.MaxValue, def.filter.maskBits);
    }

    [NativeFact]
    public void DefaultFilter_matches_the_documented_constants()
    {
        b3Filter filter = B3.b3DefaultFilter();

        Assert.Equal(Constants.B3_DEFAULT_CATEGORY_BITS, filter.categoryBits);
        Assert.Equal(Constants.B3_DEFAULT_MASK_BITS, filter.maskBits);
        Assert.Equal(0, filter.groupIndex);
    }

    /*
     * Round trips through the library, which prove that structures survive the
     * boundary in both directions.
     */

    [NativeFact]
    public void A_world_can_be_created_stepped_and_destroyed()
    {
        b3WorldDef def = B3.b3DefaultWorldDef();
        def.gravity = new Vector3(0.0f, -9.8f, 0.0f);

        b3WorldId world = B3.b3CreateWorld(&def);
        try
        {
            Assert.True(B3.b3World_IsValid(world));
            Assert.False(world.IsNull);

            // Gravity survives the round trip, so Vector3 crossed the boundary
            // correctly in both directions.
            Assert.Equal(new Vector3(0.0f, -9.8f, 0.0f), B3.b3World_GetGravity(world));

            B3.b3World_Step(world, 1.0f / 60.0f, 4);
        }
        finally
        {
            B3.b3DestroyWorld(world);
        }

        Assert.False(B3.b3World_IsValid(world));
    }

    [NativeFact]
    public void A_dynamic_body_falls_under_gravity()
    {
        b3WorldDef worldDef = B3.b3DefaultWorldDef();
        worldDef.gravity = new Vector3(0.0f, -10.0f, 0.0f);

        b3WorldId world = B3.b3CreateWorld(&worldDef);
        try
        {
            b3BodyDef bodyDef = B3.b3DefaultBodyDef();
            bodyDef.type = b3BodyType.b3_dynamicBody;
            bodyDef.position = new Vector3(0.0f, 100.0f, 0.0f);

            b3BodyId body = B3.b3CreateBody(world, &bodyDef);

            b3Sphere sphere = new() { center = Vector3.Zero, radius = 0.5f };
            b3ShapeDef shapeDef = B3.b3DefaultShapeDef();
            _ = B3.b3CreateSphereShape(body, &shapeDef, &sphere);

            for (int i = 0; i < 60; ++i)
            {
                B3.b3World_Step(world, 1.0f / 60.0f, 4);
            }

            Vector3 position = B3.b3Body_GetPosition(body);

            // After a second under 10 m/s^2 the body should be roughly five
            // metres lower, and must not have drifted sideways.
            Assert.True(position.Y < 96.0f, $"expected the body to fall, Y was {position.Y}");
            Assert.True(position.Y > 93.0f, $"the body fell further than gravity allows, Y was {position.Y}");
            Assert.Equal(0.0f, position.X, 3);
            Assert.Equal(0.0f, position.Z, 3);

            Assert.True(B3.b3Body_GetMass(body) > 0.0f, "a dynamic body with a shape must have mass");
        }
        finally
        {
            B3.b3DestroyWorld(world);
        }
    }

    [NativeFact]
    public void A_body_comes_to_rest_on_a_static_floor()
    {
        b3WorldDef worldDef = B3.b3DefaultWorldDef();
        worldDef.gravity = new Vector3(0.0f, -10.0f, 0.0f);

        b3WorldId world = B3.b3CreateWorld(&worldDef);
        try
        {
            // A large static box as the floor, with its top face at y = 0.
            b3BodyDef floorDef = B3.b3DefaultBodyDef();
            floorDef.position = new Vector3(0.0f, -5.0f, 0.0f);
            b3BodyId floor = B3.b3CreateBody(world, &floorDef);

            b3BoxHull floorHull = B3.b3MakeBoxHull(50.0f, 5.0f, 50.0f);
            b3ShapeDef floorShapeDef = B3.b3DefaultShapeDef();
            _ = B3.b3CreateHullShape(floor, &floorShapeDef, &floorHull.@base);

            b3BodyDef ballDef = B3.b3DefaultBodyDef();
            ballDef.type = b3BodyType.b3_dynamicBody;
            ballDef.position = new Vector3(0.0f, 4.0f, 0.0f);
            b3BodyId ball = B3.b3CreateBody(world, &ballDef);

            b3Sphere sphere = new() { center = Vector3.Zero, radius = 1.0f };
            b3ShapeDef ballShapeDef = B3.b3DefaultShapeDef();
            _ = B3.b3CreateSphereShape(ball, &ballShapeDef, &sphere);

            for (int i = 0; i < 240; ++i)
            {
                B3.b3World_Step(world, 1.0f / 60.0f, 4);
            }

            Vector3 position = B3.b3Body_GetPosition(ball);

            // Resting on the floor means the centre sits about one radius up.
            Assert.True(position.Y > 0.5f, $"the ball fell through the floor, Y was {position.Y}");
            Assert.True(position.Y < 1.5f, $"the ball never settled, Y was {position.Y}");
        }
        finally
        {
            B3.b3DestroyWorld(world);
        }
    }

    [NativeFact]
    public void A_ray_hits_a_sphere_and_reports_the_surface()
    {
        b3WorldDef worldDef = B3.b3DefaultWorldDef();
        b3WorldId world = B3.b3CreateWorld(&worldDef);
        try
        {
            b3BodyDef bodyDef = B3.b3DefaultBodyDef();
            bodyDef.position = new Vector3(10.0f, 0.0f, 0.0f);
            b3BodyId body = B3.b3CreateBody(world, &bodyDef);

            b3Sphere sphere = new() { center = Vector3.Zero, radius = 1.0f };
            b3ShapeDef shapeDef = B3.b3DefaultShapeDef();
            b3ShapeId shape = B3.b3CreateSphereShape(body, &shapeDef, &sphere);

            // Contacts and the broad phase are updated on the step.
            B3.b3World_Step(world, 1.0f / 60.0f, 4);

            b3RayResult hit = B3.b3World_CastRayClosest(
                world,
                Vector3.Zero,
                new Vector3(20.0f, 0.0f, 0.0f),
                B3.b3DefaultQueryFilter());

            Assert.True(hit.hit, "the ray should have hit the sphere");
            Assert.Equal(shape, hit.shapeId);

            // The sphere spans x from 9 to 11, so the near surface is at x = 9.
            Assert.Equal(9.0f, hit.point.X, 2);
            Assert.Equal(0.45f, hit.fraction, 2);

            // The normal points back along the ray, out of the surface.
            Assert.True(hit.normal.X < -0.9f, $"expected an outward normal, got {hit.normal}");
        }
        finally
        {
            B3.b3DestroyWorld(world);
        }
    }

    [NativeFact]
    public void A_ray_that_misses_reports_no_hit()
    {
        b3WorldDef worldDef = B3.b3DefaultWorldDef();
        b3WorldId world = B3.b3CreateWorld(&worldDef);
        try
        {
            b3BodyDef bodyDef = B3.b3DefaultBodyDef();
            bodyDef.position = new Vector3(10.0f, 0.0f, 0.0f);
            b3BodyId body = B3.b3CreateBody(world, &bodyDef);

            b3Sphere sphere = new() { center = Vector3.Zero, radius = 1.0f };
            b3ShapeDef shapeDef = B3.b3DefaultShapeDef();
            _ = B3.b3CreateSphereShape(body, &shapeDef, &sphere);

            B3.b3World_Step(world, 1.0f / 60.0f, 4);

            b3RayResult hit = B3.b3World_CastRayClosest(
                world,
                new Vector3(0.0f, 50.0f, 0.0f),
                new Vector3(20.0f, 0.0f, 0.0f),
                B3.b3DefaultQueryFilter());

            Assert.False(hit.hit);
        }
        finally
        {
            B3.b3DestroyWorld(world);
        }
    }

    /*
     * Ownership. Box3D allocates hulls and meshes with its own allocator, and
     * the caller frees them. b3GetByteCount reports the live total, so a leak
     * is directly observable.
     */

    [NativeFact]
    public void Creating_and_destroying_worlds_does_not_leak()
    {
        // Settle any one-time allocation first.
        b3WorldDef warmup = B3.b3DefaultWorldDef();
        b3WorldId first = B3.b3CreateWorld(&warmup);
        B3.b3DestroyWorld(first);

        int before = B3.b3GetByteCount();

        for (int i = 0; i < 16; ++i)
        {
            b3WorldDef def = B3.b3DefaultWorldDef();
            b3WorldId world = B3.b3CreateWorld(&def);

            b3BodyDef bodyDef = B3.b3DefaultBodyDef();
            bodyDef.type = b3BodyType.b3_dynamicBody;
            bodyDef.position = new Vector3(0.0f, 10.0f, 0.0f);
            b3BodyId body = B3.b3CreateBody(world, &bodyDef);

            b3Sphere sphere = new() { center = Vector3.Zero, radius = 0.5f };
            b3ShapeDef shapeDef = B3.b3DefaultShapeDef();
            _ = B3.b3CreateSphereShape(body, &shapeDef, &sphere);

            B3.b3World_Step(world, 1.0f / 60.0f, 4);
            B3.b3DestroyWorld(world);
        }

        int after = B3.b3GetByteCount();

        Assert.Equal(before, after);
    }

    [NativeFact]
    public void A_created_hull_is_freed_by_the_caller()
    {
        int before = B3.b3GetByteCount();

        b3HullData* hull = B3.b3CreateCylinder(2.0f, 1.0f, 0.0f, 16);
        try
        {
            Assert.True(hull != null);
            Assert.Equal(Constants.B3_HULL_VERSION, hull->version);
            Assert.True(hull->vertexCount > 0);
            Assert.True(hull->faceCount > 0);
            Assert.True(hull->volume > 0.0f);
            Assert.True(B3.b3GetByteCount() > before, "creating a hull should allocate");

            // The offset accessors must land inside the allocation and produce
            // points that agree with the hull's own bounding box.
            ReadOnlySpan<Vector3> points = B3.GetHullPointSpan(hull);
            Assert.Equal(hull->vertexCount, points.Length);

            foreach (Vector3 p in points)
            {
                Assert.True(
                    p.X >= hull->aabb.lowerBound.X - 1e-3f && p.X <= hull->aabb.upperBound.X + 1e-3f &&
                    p.Y >= hull->aabb.lowerBound.Y - 1e-3f && p.Y <= hull->aabb.upperBound.Y + 1e-3f &&
                    p.Z >= hull->aabb.lowerBound.Z - 1e-3f && p.Z <= hull->aabb.upperBound.Z + 1e-3f,
                    $"hull point {p} lies outside the hull's own bounds");
            }
        }
        finally
        {
            B3.b3DestroyHull(hull);
        }

        Assert.Equal(before, B3.b3GetByteCount());
    }

    [NativeFact]
    public void A_box_hull_is_self_contained_and_must_not_be_freed()
    {
        // b3MakeBoxHull returns by value with its arrays embedded, so it does
        // not allocate and must never reach b3DestroyHull.
        int before = B3.b3GetByteCount();

        b3BoxHull box = B3.b3MakeBoxHull(1.0f, 2.0f, 3.0f);

        Assert.Equal(before, B3.b3GetByteCount());
        Assert.Equal(Constants.B3_HULL_VERSION, box.@base.version);
        Assert.Equal(8, box.@base.vertexCount);
        Assert.Equal(6, box.@base.faceCount);

        // 2 x 4 x 6 metres.
        Assert.Equal(48.0f, box.@base.volume, 3);
    }

    /*
     * The deterministic math the binding delegates to the library rather than
     * reimplementing.
     */

    [NativeFact]
    public void ComputeCosSin_approximates_the_base_class_library()
    {
        // Box3D computes these itself rather than calling libm, because libm is
        // not bit-identical across platforms and Box3D guarantees deterministic
        // simulation. The trade is accuracy: the polynomial approximation is off
        // by up to a couple of parts in a thousand, so this checks a tolerance
        // rather than agreement to N decimal places.
        //
        // The tolerance is loose on purpose. What this test is really guarding
        // is that the value crossed the boundary at all and landed in the right
        // field; a marshalling fault produces garbage, not a small error.
        const float Tolerance = 5e-3f;

        foreach (float angle in new[] { 0.0f, 0.5f, -1.25f, 3.0f, -3.0f })
        {
            b3CosSin cs = B3.b3ComputeCosSin(angle);

            Assert.True(
                MathF.Abs(MathF.Cos(angle) - cs.cosine) < Tolerance,
                $"cos({angle}): expected about {MathF.Cos(angle)}, got {cs.cosine}");
            Assert.True(
                MathF.Abs(MathF.Sin(angle) - cs.sine) < Tolerance,
                $"sin({angle}): expected about {MathF.Sin(angle)}, got {cs.sine}");

            // Whatever the accuracy, the pair must still lie on the unit circle,
            // or every rotation built from it would drift.
            float magnitude = (cs.cosine * cs.cosine) + (cs.sine * cs.sine);
            Assert.True(MathF.Abs(1.0f - magnitude) < 1e-3f, $"cos^2 + sin^2 was {magnitude}");
        }
    }

    [NativeFact]
    public void Atan2_agrees_with_the_base_class_library()
    {
        foreach ((float y, float x) in new[] { (1.0f, 1.0f), (-1.0f, 2.0f), (0.5f, -3.0f), (-2.0f, -2.0f) })
        {
            // Box3D documents this as accurate to about 0.0023 degrees.
            Assert.Equal(MathF.Atan2(y, x), B3.b3Atan2(y, x), 3);
        }
    }

    [NativeFact]
    public void The_ported_math_agrees_with_the_native_validators()
    {
        Quaternion q = B3Math.b3MakeQuatFromAxisAngle(Vector3.UnitY, 0.75f);

        Assert.True(B3.b3IsValidQuat(q), "the ported axis-angle constructor must produce a normalized quaternion");
        Assert.True(B3Math.b3IsNormalizedQuat(q));

        b3Transform t = new() { p = new Vector3(1.0f, 2.0f, 3.0f), q = q };
        Assert.True(B3.b3IsValidTransform(t));

        Assert.True(B3.b3IsValidVec3(B3Math.b3Normalize(new Vector3(1.0f, 2.0f, 3.0f))));
        Assert.False(B3.b3IsValidFloat(float.NaN));
    }

    [NativeFact]
    public void The_ported_matrix_conversion_agrees_with_the_native_one()
    {
        Quaternion q = Quaternion.Normalize(new Quaternion(0.3f, -0.5f, 0.2f, 0.8f));

        b3Matrix3 m = B3Math.b3MakeMatrixFromQuat(q);
        Quaternion back = B3.b3MakeQuatFromMatrix(&m);

        // A quaternion and its negation are the same rotation, so compare the
        // dot product magnitude rather than the components.
        Assert.Equal(1.0f, MathF.Abs(B3Math.b3DotQuat(q, back)), 4);
    }

    [NativeFact]
    public void Length_units_are_reported_as_the_default()
    {
        // Nothing in the test suite calls b3SetLengthUnitsPerMeter, so the
        // scaled constants must be resting at their documented defaults.
        Assert.Equal(1.0f, B3.b3GetLengthUnitsPerMeter(), 6);
        Assert.Equal(0.005f, ScaledConstants.B3_LINEAR_SLOP, 6);
    }
}
