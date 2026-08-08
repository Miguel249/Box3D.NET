// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Who owns the geometry, how long it has to live, and what survives being
/// created and released tens of thousands of times.
/// </summary>
/// <remarks>
/// <para>
/// Hulls are copied into the shape when it is created; meshes, height fields
/// and baked compounds are not. Those three are pointed at, so the shape reads
/// the geometry for as long as it exists and the geometry has to outlive it.
/// That asymmetry is the single easiest thing to get wrong with this library,
/// and these tests are what hold the documented rule to the observed behaviour.
/// </para>
/// <para>
/// Every count here is Box3D's own <c>b3GetByteCount</c>, which is exact rather
/// than sampled: the number must come back to where it started, not merely stay
/// close. That only means anything while nothing else is allocating, hence the
/// shared non-parallel collection.
/// </para>
/// </remarks>
[Collection(NativeCollection.Name)]
public class GeometryOwnershipTests
{
    /// <summary>How many times the repeated-lifecycle tests go round.</summary>
    /// <remarks>
    /// High enough that a per-cycle leak of a single allocation is unmissable in
    /// the byte count, low enough that the suite stays quick. A leak of one
    /// eight-byte block per cycle shows up as 80 kB.
    /// </remarks>
    private const int Cycles = 10_000;

    private static void AssertNoLeak(string what, Action operation)
    {
        // A warm-up pass first: the first world of a process and the first use
        // of some internal pools allocate structures that are then reused, and
        // measuring from cold would report that as a leak.
        operation();

        int before = B3.b3GetByteCount();
        operation();
        int after = B3.b3GetByteCount();

        Assert.True(
            before == after,
            $"{what} leaked {after - before} bytes ({before} before, {after} after)");
    }

    // ------------------------------------------------------ shared geometry

    /// <summary>
    /// One mesh backing many shapes at once. The mesh is not copied, so every
    /// shape holds the same pointer and none of them owns it.
    /// </summary>
    [NativeFact]
    public void OneMeshCanBackManyShapes()
    {
        const int ShapeCount = 200;

        using var world = new PhysicsWorld();
        using CollisionMesh mesh = CollisionMesh.Grid(8, 8, 1.0f);

        var shapes = new Shape[ShapeCount];
        for (int i = 0; i < ShapeCount; i++)
        {
            Body body = world.CreateStaticBody(new Vector3(i * 20.0f, 0.0f, 0.0f));
            shapes[i] = body.AddMesh(mesh);
        }

        world.Step(1.0f / 60.0f);

        foreach (Shape shape in shapes)
        {
            Assert.True(shape.IsValid);
            Assert.Equal(ShapeType.Mesh, shape.Type);
        }

        // Destroying every shape must leave the mesh intact and usable.
        foreach (Shape shape in shapes)
        {
            shape.Destroy();
        }

        Assert.False(mesh.IsDisposed);

        Body reuser = world.CreateStaticBody();
        Shape afterwards = reuser.AddMesh(mesh);
        world.Step(1.0f / 60.0f);
        Assert.True(afterwards.IsValid);
    }

    /// <summary>
    /// A hull is copied into each shape, so the shapes go on working after the
    /// hull that produced them has been disposed.
    /// </summary>
    [NativeFact]
    public void AHullIsCopiedIntoEveryShapeItCreates()
    {
        using var world = new PhysicsWorld();

        Shape first;
        Shape second;

        using (ConvexHull hull = ConvexHull.Cylinder(1.0f, 0.5f))
        {
            first = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f)).AddHull(hull);
            second = world.CreateDynamicBody(new Vector3(4.0f, 5.0f, 0.0f)).AddHull(hull);
        }

        // The hull is gone; the shapes are not.
        for (int frame = 0; frame < 30; frame++)
        {
            world.Step(1.0f / 60.0f);
        }

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.Equal(ShapeType.Hull, first.Type);
    }

    /// <summary>
    /// A height field is shared the same way a mesh is.
    /// </summary>
    [NativeFact]
    public void OneHeightFieldCanBackManyShapes()
    {
        const int ShapeCount = 64;

        using var world = new PhysicsWorld();
        using HeightField field = HeightField.Grid(8, 8, Vector3.One);

        var shapes = new Shape[ShapeCount];
        for (int i = 0; i < ShapeCount; i++)
        {
            shapes[i] = world.CreateStaticBody(new Vector3(i * 20.0f, 0.0f, 0.0f)).AddHeightField(field);
        }

        world.Step(1.0f / 60.0f);

        foreach (Shape shape in shapes)
        {
            Assert.True(shape.IsValid);
        }
    }

    /// <summary>
    /// A baked compound is shared the same way, and the shape it produces is a
    /// single shape whatever the compound holds.
    /// </summary>
    [NativeFact]
    public void OneCompoundCanBackManyShapes()
    {
        const int ShapeCount = 32;

        using var world = new PhysicsWorld();
        using ConvexHull rock = ConvexHull.Rock(0.5f);
        using CompoundGeometry wall = new CompoundBuilder()
            .AddHull(rock, new Vector3(0.0f, 0.5f, 0.0f))
            .AddHull(rock, new Vector3(1.0f, 0.5f, 0.0f))
            .Build();

        var shapes = new Shape[ShapeCount];
        for (int i = 0; i < ShapeCount; i++)
        {
            shapes[i] = world.CreateStaticBody(new Vector3(i * 20.0f, 0.0f, 0.0f)).AddCompound(wall);
        }

        world.Step(1.0f / 60.0f);

        foreach (Shape shape in shapes)
        {
            Assert.True(shape.IsValid);
        }
    }

    // --------------------------------------------------- destruction orders

    /// <summary>
    /// The documented order - shapes, then geometry, then bodies, then world -
    /// and every other order that is also safe.
    /// </summary>
    /// <remarks>
    /// The one order that is <em>not</em> safe, disposing a mesh while a shape
    /// still reads it, is a use-after-free inside the solver rather than an
    /// exception, so it cannot be tested without corrupting the process. It is
    /// documented on <see cref="Body.AddMesh"/> instead.
    /// </remarks>
    [NativeTheory]
    [InlineData(TeardownOrder.ShapesGeometryBodiesWorld)]
    [InlineData(TeardownOrder.ShapesBodiesGeometryWorld)]
    [InlineData(TeardownOrder.BodiesGeometryWorld)]
    [InlineData(TeardownOrder.WorldThenGeometry)]
    public void EveryValidTeardownOrderIsSafe(TeardownOrder order)
    {
        var world = new PhysicsWorld();
        CollisionMesh mesh = CollisionMesh.Grid(8, 8, 1.0f);
        HeightField field = HeightField.Grid(8, 8, Vector3.One);

        Body meshBody = world.CreateStaticBody();
        Shape meshShape = meshBody.AddMesh(mesh);

        Body fieldBody = world.CreateStaticBody(new Vector3(100.0f, 0.0f, 0.0f));
        Shape fieldShape = fieldBody.AddHeightField(field);

        world.Step(1.0f / 60.0f);

        switch (order)
        {
            case TeardownOrder.ShapesGeometryBodiesWorld:
                meshShape.Destroy();
                fieldShape.Destroy();
                mesh.Dispose();
                field.Dispose();
                meshBody.Destroy();
                fieldBody.Destroy();
                world.Dispose();
                break;

            case TeardownOrder.ShapesBodiesGeometryWorld:
                meshShape.Destroy();
                fieldShape.Destroy();
                meshBody.Destroy();
                fieldBody.Destroy();
                mesh.Dispose();
                field.Dispose();
                world.Dispose();
                break;

            case TeardownOrder.BodiesGeometryWorld:
                // Destroying the body takes its shapes with it, which releases
                // the last reader of the geometry.
                meshBody.Destroy();
                fieldBody.Destroy();
                mesh.Dispose();
                field.Dispose();
                world.Dispose();
                break;

            case TeardownOrder.WorldThenGeometry:
                // The world owns every shape, so disposing it releases every
                // reader at once. This is the order most applications will use.
                world.Dispose();
                mesh.Dispose();
                field.Dispose();
                break;
        }

        Assert.True(world.IsDisposed);
        Assert.True(mesh.IsDisposed);
        Assert.True(field.IsDisposed);
        Assert.False(meshShape.IsValid);
        Assert.False(fieldShape.IsValid);
    }

    /// <summary>The order the pieces of a scene are released in.</summary>
    public enum TeardownOrder
    {
        /// <summary>Shapes, then the geometry they read, then bodies, then the world.</summary>
        ShapesGeometryBodiesWorld,

        /// <summary>Shapes, then bodies, then the geometry, then the world.</summary>
        ShapesBodiesGeometryWorld,

        /// <summary>Bodies, which take their shapes with them, then the geometry, then the world.</summary>
        BodiesGeometryWorld,

        /// <summary>The world, which takes everything in it, and only then the geometry.</summary>
        WorldThenGeometry,
    }

    [NativeFact]
    public void DisposingGeometryTwiceIsHarmless()
    {
        var mesh = CollisionMesh.Grid(4, 4, 1.0f);
        var field = HeightField.Grid(4, 4, Vector3.One);
        var hull = ConvexHull.Cylinder(1.0f, 0.5f);

        mesh.Dispose();
        mesh.Dispose();
        field.Dispose();
        field.Dispose();
        hull.Dispose();
        hull.Dispose();

        Assert.True(mesh.IsDisposed);
        Assert.True(field.IsDisposed);
        Assert.True(hull.IsDisposed);

        // Reading disposed geometry is an exception, not a read of freed memory.
        Assert.Throws<ObjectDisposedException>(() => mesh.TriangleCount);
        Assert.Throws<ObjectDisposedException>(() => field.RowCount);
        Assert.Throws<ObjectDisposedException>(() => hull.VertexCount);
    }

    [NativeFact]
    public void AttachingDisposedGeometryThrowsRatherThanReadingFreedMemory()
    {
        using var world = new PhysicsWorld();
        Body body = world.CreateStaticBody();

        var mesh = CollisionMesh.Grid(4, 4, 1.0f);
        mesh.Dispose();
        Assert.Throws<ObjectDisposedException>(() => body.AddMesh(mesh));

        var field = HeightField.Grid(4, 4, Vector3.One);
        field.Dispose();
        Assert.Throws<ObjectDisposedException>(() => body.AddHeightField(field));

        var hull = ConvexHull.Cylinder(1.0f, 0.5f);
        hull.Dispose();
        Assert.Throws<ObjectDisposedException>(() => body.AddHull(hull));
    }

    // ------------------------------------------------- repeated lifecycles

    /// <summary>
    /// Ten thousand build-and-release cycles for each kind of geometry, with
    /// Box3D's live byte count required to return to where it started.
    /// </summary>
    [NativeFact]
    public void TenThousandHullLifecyclesDoNotLeak() =>
        AssertNoLeak($"{Cycles} hull lifecycles", () =>
        {
            for (int i = 0; i < Cycles; i++)
            {
                using ConvexHull hull = ConvexHull.Cylinder(1.0f, 0.5f);
                Assert.True(hull.VertexCount > 0);
            }
        });

    [NativeFact]
    public void TenThousandMeshLifecyclesDoNotLeak() =>
        AssertNoLeak($"{Cycles} mesh lifecycles", () =>
        {
            for (int i = 0; i < Cycles; i++)
            {
                using CollisionMesh mesh = CollisionMesh.Grid(4, 4, 1.0f);
                Assert.True(mesh.TriangleCount > 0);
            }
        });

    [NativeFact]
    public void TenThousandHeightFieldLifecyclesDoNotLeak() =>
        AssertNoLeak($"{Cycles} height field lifecycles", () =>
        {
            for (int i = 0; i < Cycles; i++)
            {
                using HeightField field = HeightField.Grid(4, 4, Vector3.One);
                Assert.Equal(4, field.RowCount);
            }
        });

    [NativeFact]
    public void TenThousandCompoundLifecyclesDoNotLeak()
    {
        using ConvexHull rock = ConvexHull.Rock(0.5f);

        AssertNoLeak($"{Cycles} compound lifecycles", () =>
        {
            for (int i = 0; i < Cycles; i++)
            {
                using CompoundGeometry compound = new CompoundBuilder()
                    .AddHull(rock, Vector3.Zero)
                    .Build();
                Assert.True(compound.ChildCount > 0);
            }
        });
    }

    /// <summary>
    /// Attaching and detaching a mesh shape ten thousand times against one
    /// long-lived mesh. This is the cycle a streaming world runs.
    /// </summary>
    [NativeFact]
    public void TenThousandMeshShapeAttachmentsDoNotLeak()
    {
        using CollisionMesh mesh = CollisionMesh.Grid(4, 4, 1.0f);

        AssertNoLeak($"{Cycles} mesh shape attachments", () =>
        {
            using var world = new PhysicsWorld();
            Body body = world.CreateStaticBody();

            for (int i = 0; i < Cycles; i++)
            {
                Shape shape = body.AddMesh(mesh);
                shape.Destroy();
            }
        });

        // The mesh survived every one of them.
        Assert.False(mesh.IsDisposed);
        Assert.True(mesh.TriangleCount > 0);
    }

    /// <summary>
    /// Whole worlds built out of every kind of geometry, then torn down, over
    /// and over.
    /// </summary>
    [NativeFact]
    public void RepeatedWorldsFullOfGeometryDoNotLeak() =>
        AssertNoLeak("worlds full of geometry", () =>
        {
            for (int cycle = 0; cycle < 100; cycle++)
            {
                using ConvexHull hull = ConvexHull.Rock(0.5f);
                using CollisionMesh mesh = CollisionMesh.Grid(8, 8, 1.0f);
                using HeightField field = HeightField.Grid(8, 8, Vector3.One);
                using CompoundGeometry compound = new CompoundBuilder()
                    .AddHull(hull, Vector3.Zero)
                    .AddHull(hull, Vector3.UnitX)
                    .Build();

                using var world = new PhysicsWorld();

                world.CreateStaticBody().AddMesh(mesh);
                world.CreateStaticBody(new Vector3(100.0f, 0.0f, 0.0f)).AddHeightField(field);
                world.CreateStaticBody(new Vector3(200.0f, 0.0f, 0.0f)).AddCompound(compound);

                for (int i = 0; i < 10; i++)
                {
                    world.CreateDynamicBody(new Vector3(i, 10.0f, 0.0f)).AddHull(hull);
                }

                world.Step(1.0f / 60.0f);
            }
        });

    /// <summary>
    /// Ten thousand bodies created and destroyed in one long-lived world, which
    /// is the shape of a bullet or particle system.
    /// </summary>
    [NativeFact]
    public void TenThousandBodyLifecyclesInOneWorldDoNotLeak() =>
        AssertNoLeak($"{Cycles} body lifecycles", () =>
        {
            using var world = new PhysicsWorld();

            for (int i = 0; i < Cycles; i++)
            {
                Body body = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
                body.AddSphere(new Sphere(0.25f));
                body.Destroy();
            }
        });
}
