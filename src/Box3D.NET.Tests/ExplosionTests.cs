// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Holds <see cref="PhysicsWorld.Explode"/> to what its documentation says
/// responds and what does not.
/// </summary>
/// <remarks>
/// The documentation once read "only spheres, capsules and hulls respond", which
/// in an API where <see cref="Box"/> and <see cref="ConvexHull"/> are different
/// types reads as "boxes do not". They do, because a box is a hull, and these
/// tests pin that alongside the cases that genuinely do not respond.
/// </remarks>
[Collection(NativeCollection.Name)]
public class ExplosionTests : IDisposable
{
    private readonly PhysicsWorld _world = new(WorldSettings.Default with { Gravity = Vector3.Zero });

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    // At the default density a one-metre cube weighs a tonne, so the impulse is sized to
    // move it at about a metre per second.
    private void Blast(ulong mask = ulong.MaxValue) =>
        _world.Explode(Vector3.Zero, radius: 5.0f, impulsePerArea: 1000.0f, filter: mask);

    [NativeFact]
    public void A_box_is_a_hull()
    {
        Body body = _world.CreateDynamicBody(new Vector3(2.0f, 0.0f, 0.0f));
        Shape box = body.AddBox(Box.Cube(0.5f));

        Assert.Equal(ShapeType.Hull, box.Type);
    }

    [NativeFact]
    public void A_box_is_pushed_away_from_the_blast()
    {
        Body body = _world.CreateDynamicBody(new Vector3(2.0f, 0.0f, 0.0f));
        body.AddBox(Box.Cube(0.5f));

        Blast();

        Assert.True(body.LinearVelocity.X > 0.1f, $"the box should be pushed along +x, velocity {body.LinearVelocity}");
    }

    [NativeFact]
    public void Spheres_and_capsules_are_pushed_too()
    {
        Body sphere = _world.CreateDynamicBody(new Vector3(0.0f, 2.0f, 0.0f));
        sphere.AddSphere(new Sphere(0.5f));

        Body capsule = _world.CreateDynamicBody(new Vector3(0.0f, 0.0f, 2.0f));
        capsule.AddCapsule(Capsule.Upright(1.0f, 0.25f));

        Blast();

        Assert.True(sphere.LinearVelocity.Y > 0.1f, $"sphere velocity {sphere.LinearVelocity}");
        Assert.True(capsule.LinearVelocity.Z > 0.1f, $"capsule velocity {capsule.LinearVelocity}");
    }

    [NativeFact]
    public void A_kinematic_body_is_not_pushed()
    {
        Body body = _world.CreateBody(BodyDefinition.Kinematic(new Vector3(2.0f, 0.0f, 0.0f)));
        body.AddBox(Box.Cube(0.5f));

        Blast();

        Assert.Equal(Vector3.Zero, body.LinearVelocity);
    }

    [NativeFact]
    public void A_body_beyond_the_radius_and_falloff_is_not_pushed()
    {
        Body body = _world.CreateDynamicBody(new Vector3(20.0f, 0.0f, 0.0f));
        body.AddBox(Box.Cube(0.5f));

        Blast();

        Assert.Equal(Vector3.Zero, body.LinearVelocity);
    }

    [NativeFact]
    public void A_sleeping_body_is_woken_and_pushed()
    {
        Body body = _world.CreateDynamicBody(new Vector3(2.0f, 0.0f, 0.0f));
        body.AddBox(Box.Cube(0.5f));
        body.IsAwake = false;

        Blast();

        Assert.True(body.IsAwake, "the blast should wake the body");
        Assert.True(body.LinearVelocity.X > 0.1f, $"velocity {body.LinearVelocity}");
    }

    [NativeFact]
    public void The_filter_is_a_category_mask()
    {
        var debris = ShapeDefinition.Default with { Filter = CollisionFilter.Default with { Categories = 0x2 } };
        var scenery = ShapeDefinition.Default with { Filter = CollisionFilter.Default with { Categories = 0x4 } };

        Body affected = _world.CreateDynamicBody(new Vector3(2.0f, 0.0f, 0.0f));
        affected.AddBox(Box.Cube(0.5f), debris);

        Body spared = _world.CreateDynamicBody(new Vector3(-2.0f, 0.0f, 0.0f));
        spared.AddBox(Box.Cube(0.5f), scenery);

        // The mask is compared with each shape's categories, not with what the
        // shape collides with.
        Blast(mask: 0x2);

        Assert.True(affected.LinearVelocity.X > 0.1f, $"the debris should be pushed, velocity {affected.LinearVelocity}");
        Assert.Equal(Vector3.Zero, spared.LinearVelocity);
    }

    [NativeFact]
    public void The_filter_ignores_what_the_shape_collides_with()
    {
        // Unlike a query, which also needs the shape to accept the query's
        // categories, an explosion checks one direction only. A shape that
        // collides with nothing is still pushed.
        var aloof = ShapeDefinition.Default with { Filter = CollisionFilter.Default with { CollidesWith = 0 } };

        Body body = _world.CreateDynamicBody(new Vector3(2.0f, 0.0f, 0.0f));
        body.AddBox(Box.Cube(0.5f), aloof);

        Blast();

        Assert.True(body.LinearVelocity.X > 0.1f, $"velocity {body.LinearVelocity}");
    }
}
