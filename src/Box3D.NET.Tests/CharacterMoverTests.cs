// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Covers the character mover primitives: gathering contacts, solving the planes
/// they imply, and clipping velocity against them.
/// </summary>
[Collection(NativeCollection.Name)]
public class CharacterMoverTests : IDisposable
{
    private const int MaxPlanes = 16;

    private readonly PhysicsWorld _world;

    public CharacterMoverTests()
    {
        _world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private struct GatherPlanes : ICharacterCollisionCallback
    {
        public CollisionPlane[] Planes;
        public int Count;

        public bool OnContact(in CharacterContact contact)
        {
            if (Count < Planes.Length)
            {
                Planes[Count++] = CollisionPlane.From(contact);
            }

            return true;
        }
    }

    private struct CountContacts : ICharacterCollisionCallback
    {
        public int Count;

        public bool OnContact(in CharacterContact contact)
        {
            Count++;
            return true;
        }
    }

    private struct StopAfterFirst : ICharacterCollisionCallback
    {
        public int Count;

        public bool OnContact(in CharacterContact contact)
        {
            Count++;
            return false;
        }
    }

    private void AddFloor(float y = 0.0f)
    {
        Body ground = _world.CreateStaticBody(new Vector3(0.0f, y - 0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(25.0f, 0.5f, 25.0f)));
    }

    private void AddWall(float x)
    {
        Body wall = _world.CreateStaticBody(new Vector3(x, 2.0f, 0.0f));
        wall.AddBox(new Box(new Vector3(0.25f, 2.0f, 10.0f)));
    }

    // ------------------------------------------------------------ gathering

    [NativeFact]
    public void A_capsule_clear_of_everything_touches_nothing()
    {
        AddFloor();
        _world.Step(1.0f / 60.0f);

        Capsule capsule = Capsule.Upright(1.8f, 0.3f);

        var callback = new CountContacts();
        _world.CollideCapsule(capsule, new Vector3(0.0f, 50.0f, 0.0f), ref callback);

        Assert.Equal(0, callback.Count);
    }

    [NativeFact]
    public void A_capsule_standing_on_the_floor_reports_an_upward_plane()
    {
        AddFloor();
        _world.Step(1.0f / 60.0f);

        Capsule capsule = Capsule.Upright(1.8f, 0.3f);

        // The capsule spans 0.9 either side of its origin, so placing the origin
        // at 0.9 rests its bottom cap exactly on the floor.
        var gather = new GatherPlanes { Planes = new CollisionPlane[MaxPlanes] };
        _world.CollideCapsule(capsule, new Vector3(0.0f, 0.9f, 0.0f), ref gather);

        Assert.True(gather.Count > 0, "standing on the floor should report contact");

        bool foundGround = false;
        for (int i = 0; i < gather.Count; i++)
        {
            // A floor pushes up, so its normal points along positive y.
            if (Vector3.Dot(gather.Planes[i].Normal, Vector3.UnitY) > 0.9f)
            {
                foundGround = true;
            }
        }

        Assert.True(foundGround, "expected a plane whose normal points up");
    }

    [NativeFact]
    public void A_callback_can_stop_early()
    {
        AddFloor();
        AddWall(1.0f);
        _world.Step(1.0f / 60.0f);

        Capsule capsule = Capsule.Upright(1.8f, 0.3f);

        var stop = new StopAfterFirst();
        _world.CollideCapsule(capsule, new Vector3(0.5f, 0.9f, 0.0f), ref stop);

        Assert.Equal(1, stop.Count);
    }

    [NativeFact]
    public void The_filter_keeps_a_capsule_from_seeing_a_shape()
    {
        AddFloor();
        _world.Step(1.0f / 60.0f);

        Capsule capsule = Capsule.Upright(1.8f, 0.3f);

        var callback = new CountContacts();
        _world.CollideCapsule(
            capsule,
            new Vector3(0.0f, 0.9f, 0.0f),
            ref callback,
            new QueryFilter(categories: 1, collidesWith: 0));

        Assert.Equal(0, callback.Count);
    }

    // -------------------------------------------------------------- solving

    [NativeFact]
    public void With_no_planes_the_solver_returns_the_movement_unchanged()
    {
        Vector3 wanted = new(1.0f, -2.0f, 0.5f);

        PlaneSolverResult result = CharacterMover.SolvePlanes(wanted, []);

        Assert.Equal(wanted, result.Translation);
    }

    [NativeFact]
    public void The_solver_stops_movement_into_a_plane()
    {
        // A floor at the capsule's feet: pushing straight down must not go through.
        Span<CollisionPlane> planes = [new CollisionPlane(Vector3.UnitY, 0.0f)];

        PlaneSolverResult result = CharacterMover.SolvePlanes(new Vector3(0.0f, -1.0f, 0.0f), planes);

        Assert.True(result.Translation.Y > -0.01f, $"the solver let the capsule sink to {result.Translation.Y}");
    }

    [NativeFact]
    public void The_solver_lets_movement_slide_along_a_plane()
    {
        // Walking diagonally into a wall whose normal points along -x should keep
        // the z component and lose the x component.
        Span<CollisionPlane> planes = [new CollisionPlane(new Vector3(-1.0f, 0.0f, 0.0f), 0.0f)];

        PlaneSolverResult result = CharacterMover.SolvePlanes(new Vector3(1.0f, 0.0f, 1.0f), planes);

        Assert.True(result.Translation.X < 0.1f, $"movement into the wall survived: {result.Translation.X}");
        Assert.True(result.Translation.Z > 0.9f, $"movement along the wall was lost: {result.Translation.Z}");
    }

    [NativeFact]
    public void The_solver_reports_which_planes_it_pushed_against()
    {
        // Separation is dot(normal, point) - offset, so a plane the capsule is
        // clear of needs a negative offset. A wall five metres behind: its normal
        // points along -x and the capsule sits five units on the outside of it.
        Span<CollisionPlane> planes =
        [
            new CollisionPlane(Vector3.UnitY, 0.0f),
            new CollisionPlane(new Vector3(-1.0f, 0.0f, 0.0f), -5.0f),
        ];

        CharacterMover.SolvePlanes(new Vector3(0.0f, -1.0f, 0.0f), planes);

        // Only the floor resisted, so only the floor has a push. This is how a
        // controller decides it is standing on something.
        Assert.True(planes[0].Push > 0.0f, "the floor should have pushed back");
        Assert.Equal(0.0f, planes[1].Push);
    }

    [NativeFact]
    public void A_corner_of_two_walls_is_satisfied_at_once()
    {
        Span<CollisionPlane> planes =
        [
            new CollisionPlane(new Vector3(-1.0f, 0.0f, 0.0f), 0.0f),
            new CollisionPlane(new Vector3(0.0f, 0.0f, -1.0f), 0.0f),
        ];

        PlaneSolverResult result = CharacterMover.SolvePlanes(new Vector3(1.0f, 0.0f, 1.0f), planes);

        // Wedged into a corner, neither direction is available.
        Assert.True(result.Translation.X < 0.1f);
        Assert.True(result.Translation.Z < 0.1f);
    }

    // ------------------------------------------------------------- clipping

    [NativeFact]
    public void Clipping_removes_the_velocity_going_into_a_plane()
    {
        Span<CollisionPlane> planes = [new CollisionPlane(Vector3.UnitY, 0.0f)];

        // Solve first: ClipVelocity only considers planes the solver pushed on.
        CharacterMover.SolvePlanes(new Vector3(0.0f, -1.0f, 0.0f), planes);

        Vector3 clipped = CharacterMover.ClipVelocity(new Vector3(2.0f, -10.0f, 0.0f), planes);

        // Falling into the floor is removed; running along it survives.
        Assert.True(clipped.Y > -0.1f, $"downward velocity survived at {clipped.Y}");
        Assert.Equal(2.0f, clipped.X, 3);
    }

    [NativeFact]
    public void Clipping_with_no_planes_changes_nothing()
    {
        Vector3 velocity = new(1.0f, -2.0f, 3.0f);

        Assert.Equal(velocity, CharacterMover.ClipVelocity(velocity, []));
    }

    [NativeFact]
    public void A_plane_that_does_not_clip_leaves_velocity_alone()
    {
        Span<CollisionPlane> planes =
        [
            new CollisionPlane(Vector3.UnitY, 0.0f, pushLimit: 0.1f, clipsVelocity: false),
        ];

        CharacterMover.SolvePlanes(new Vector3(0.0f, -1.0f, 0.0f), planes);

        Vector3 velocity = new(0.0f, -10.0f, 0.0f);

        Assert.Equal(velocity, CharacterMover.ClipVelocity(velocity, planes));
    }

    // ---------------------------------------------------------------- casts

    [NativeFact]
    public void A_capsule_cast_into_the_open_travels_the_whole_way()
    {
        AddFloor();
        _world.Step(1.0f / 60.0f);

        Capsule capsule = Capsule.Upright(1.8f, 0.3f);

        float fraction = _world.CastCapsule(
            capsule,
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(5.0f, 0.0f, 0.0f));

        Assert.Equal(1.0f, fraction, 2);
    }

    [NativeFact]
    public void A_capsule_cast_into_a_wall_stops_short()
    {
        AddFloor();
        AddWall(3.0f);
        _world.Step(1.0f / 60.0f);

        Capsule capsule = Capsule.Upright(1.8f, 0.3f);

        float fraction = _world.CastCapsule(
            capsule,
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(10.0f, 0.0f, 0.0f));

        Assert.True(fraction < 1.0f, $"the cast should have been blocked, fraction was {fraction}");

        // The wall face is at x = 2.75 and the capsule radius is 0.3, so contact
        // happens around x = 2.45, a quarter of the way along a ten metre cast.
        Assert.True(fraction is > 0.15f and < 0.35f, $"expected to stop near the wall, fraction was {fraction}");
    }

    // ------------------------------------------------------- the whole loop

    [NativeFact]
    public void A_character_walks_along_a_wall_instead_of_through_it()
    {
        AddFloor();
        AddWall(2.0f);
        _world.Step(1.0f / 60.0f);

        Capsule capsule = Capsule.Upright(1.8f, 0.3f);
        Vector3 position = new(0.0f, 0.9f, 0.0f);
        var planes = new CollisionPlane[MaxPlanes];

        // Walk diagonally into the wall for a second.
        Vector3 wanted = new Vector3(2.0f, 0.0f, 2.0f) * (1.0f / 60.0f);

        for (int step = 0; step < 60; step++)
        {
            var gather = new GatherPlanes { Planes = planes };
            _world.CollideCapsule(capsule, position, ref gather);

            PlaneSolverResult result = CharacterMover.SolvePlanes(wanted, planes.AsSpan(0, gather.Count));
            position += result.Translation;
        }

        // The wall face sits at x = 1.75 and the capsule has a radius of 0.3, so
        // the character cannot get past about x = 1.45.
        Assert.True(position.X < 1.6f, $"the character walked through the wall to x = {position.X}");

        // But it kept moving along the wall, which is the whole point of the
        // plane solver rather than a simple stop.
        Assert.True(position.Z > 1.0f, $"the character stopped dead instead of sliding, z = {position.Z}");
    }
}
