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

    // ------------------------------------------------------ what was touched

    private struct GatherContacts : ICharacterCollisionCallback
    {
        public CharacterContact[] Contacts;
        public int Count;

        public bool OnContact(in CharacterContact contact)
        {
            if (Count < Contacts.Length)
            {
                Contacts[Count++] = contact;
            }

            return true;
        }
    }

    // The contact from the surface directly beneath the capsule.
    private CharacterContact GroundContactAt(Vector3 position)
    {
        Capsule capsule = Capsule.Upright(1.8f, 0.3f);
        var gather = new GatherContacts { Contacts = new CharacterContact[MaxPlanes] };
        _world.CollideCapsule(capsule, position, ref gather);

        for (int i = 0; i < gather.Count; i++)
        {
            if (Vector3.Dot(gather.Contacts[i].Normal, Vector3.UnitY) > 0.9f)
            {
                return gather.Contacts[i];
            }
        }

        Assert.Fail($"no ground contact at {position}; {gather.Count} contacts in all");
        return default;
    }

    [NativeFact]
    public void A_contact_with_a_mesh_names_the_same_triangle_a_ray_does()
    {
        // Eight by eight cells of one metre centred on the origin, two triangles
        // each, so the capsule stands on a different triangle depending on where
        // it is.
        using CollisionMesh grid = CollisionMesh.Grid(8, 8, 1.0f);
        _world.CreateStaticBody().AddMesh(grid);
        _world.Step(1.0f / 60.0f);

        var seen = new System.Collections.Generic.HashSet<int>();

        foreach (Vector3 foot in new[] { new Vector3(-2.7f, 0.0f, -2.8f), new Vector3(1.6f, 0.0f, 2.3f), new Vector3(-0.8f, 0.0f, 2.7f) })
        {
            RaycastHit ray = _world.RaycastClosest(foot + new Vector3(0.0f, 5.0f, 0.0f), new Vector3(0.0f, -10.0f, 0.0f));
            Assert.True(ray.Hit, $"the ray missed the grid at {foot}");

            CharacterContact contact = GroundContactAt(foot + new Vector3(0.0f, 0.9f, 0.0f));

            Assert.Equal(ray.Shape, contact.Shape);
            Assert.Equal(ray.TriangleIndex, contact.TriangleIndex);
            Assert.InRange(contact.TriangleIndex, 0, grid.TriangleCount - 1);
            Assert.Equal(0, contact.ChildIndex);

            // A mesh attached here carries a single material, and Box3D clamps
            // the index to the shape's material count.
            Assert.Equal(0, contact.MaterialIndex);

            seen.Add(contact.TriangleIndex);
        }

        Assert.Equal(3, seen.Count);
    }

    [NativeFact]
    public void A_contact_with_a_height_field_names_the_same_triangle_a_ray_does()
    {
        using HeightField terrain = HeightField.Grid(9, 9, new Vector3(1.0f, 1.0f, 1.0f));
        _world.CreateStaticBody().AddHeightField(terrain);
        _world.Step(1.0f / 60.0f);

        Vector3 foot = new(2.3f, 0.0f, 4.6f);
        RaycastHit ray = _world.RaycastClosest(foot + new Vector3(0.0f, 5.0f, 0.0f), new Vector3(0.0f, -10.0f, 0.0f));
        Assert.True(ray.Hit, "the ray missed the terrain");

        CharacterContact contact = GroundContactAt(new Vector3(foot.X, ray.Point.Y + 0.9f, foot.Z));

        Assert.Equal(ray.TriangleIndex, contact.TriangleIndex);
        Assert.True(contact.TriangleIndex >= 0, $"triangle index {contact.TriangleIndex}");
    }

    [NativeFact]
    public void A_contact_with_a_compound_names_the_child_and_its_material()
    {
        var stone = PhysicsMaterial.Default with { Friction = 0.9f, UserMaterialId = 1 };
        var ice = PhysicsMaterial.Default with { Friction = 0.02f, UserMaterialId = 2 };

        // Two flat-topped boxes side by side, one of stone and one of ice.
        using ConvexHull slab = ConvexHull.FromPoints(
        [
            new Vector3(-1.0f, -0.5f, -1.0f), new Vector3(1.0f, -0.5f, -1.0f),
            new Vector3(1.0f, -0.5f, 1.0f), new Vector3(-1.0f, -0.5f, 1.0f),
            new Vector3(-1.0f, 0.5f, -1.0f), new Vector3(1.0f, 0.5f, -1.0f),
            new Vector3(1.0f, 0.5f, 1.0f), new Vector3(-1.0f, 0.5f, 1.0f),
        ]);

        using CompoundGeometry compound = new CompoundBuilder()
            .AddHull(slab, new Vector3(-2.0f, -0.5f, 0.0f), material: stone)
            .AddHull(slab, new Vector3(2.0f, -0.5f, 0.0f), material: ice)
            .Build();

        _world.CreateStaticBody().AddCompound(compound);
        _world.Step(1.0f / 60.0f);

        CharacterContact onStone = GroundContactAt(new Vector3(-2.0f, 0.9f, 0.0f));
        CharacterContact onIce = GroundContactAt(new Vector3(2.0f, 0.9f, 0.0f));

        RaycastHit stoneRay = _world.RaycastClosest(new Vector3(-2.0f, 5.0f, 0.0f), new Vector3(0.0f, -10.0f, 0.0f));
        RaycastHit iceRay = _world.RaycastClosest(new Vector3(2.0f, 5.0f, 0.0f), new Vector3(0.0f, -10.0f, 0.0f));

        // The child index agrees with the one ray casts already report.
        Assert.Equal(stoneRay.ChildIndex, onStone.ChildIndex);
        Assert.Equal(iceRay.ChildIndex, onIce.ChildIndex);
        Assert.NotEqual(onStone.ChildIndex, onIce.ChildIndex);

        // And the two surfaces are told apart by material, which is what lets a
        // controller make the ice slippery.
        Assert.Equal(1UL, stoneRay.UserMaterialId);
        Assert.Equal(2UL, iceRay.UserMaterialId);
        Assert.NotEqual(onStone.MaterialIndex, onIce.MaterialIndex);
        Assert.InRange(onStone.MaterialIndex, 0, 1);
        Assert.InRange(onIce.MaterialIndex, 0, 1);
    }

    [NativeFact]
    public void A_contact_with_a_convex_shape_reports_zero_indices()
    {
        AddFloor();
        _world.Step(1.0f / 60.0f);

        CharacterContact contact = GroundContactAt(new Vector3(0.0f, 0.9f, 0.0f));

        Assert.Equal(0, contact.TriangleIndex);
        Assert.Equal(0, contact.ChildIndex);
        Assert.Equal(0, contact.MaterialIndex);
    }

    // ---------------------------------------------------- one-sided triangles

    [NativeFact]
    public void A_capsule_cast_stops_on_the_front_of_a_mesh()
    {
        using CollisionMesh grid = CollisionMesh.Grid(8, 8, 1.0f);
        _world.CreateStaticBody().AddMesh(grid);
        _world.Step(1.0f / 60.0f);

        Capsule capsule = Capsule.Upright(1.8f, 0.3f);

        float fraction = _world.CastCapsule(capsule, new Vector3(0.3f, 5.0f, 0.4f), new Vector3(0.0f, -10.0f, 0.0f));

        // The capsule bottom starts 4.1 metres up, so it lands about 41% of the way.
        Assert.True(fraction is > 0.3f and < 0.5f, $"the cast from above stopped at {fraction}");
    }

    [NativeFact]
    public void A_capsule_cast_passes_through_the_back_of_a_mesh()
    {
        using CollisionMesh grid = CollisionMesh.Grid(8, 8, 1.0f);
        _world.CreateStaticBody().AddMesh(grid);
        _world.Step(1.0f / 60.0f);

        Capsule capsule = Capsule.Upright(1.8f, 0.3f);

        float fraction = _world.CastCapsule(capsule, new Vector3(0.3f, -5.0f, 0.4f), new Vector3(0.0f, 10.0f, 0.0f));

        Assert.Equal(1.0f, fraction);
    }

    [NativeFact]
    public void A_capsule_cast_passes_through_the_back_of_a_height_field()
    {
        using HeightField terrain = HeightField.Grid(9, 9, new Vector3(1.0f, 1.0f, 1.0f));
        _world.CreateStaticBody().AddHeightField(terrain);
        _world.Step(1.0f / 60.0f);

        Capsule capsule = Capsule.Upright(1.8f, 0.3f);

        float fromAbove = _world.CastCapsule(capsule, new Vector3(4.0f, 5.0f, 4.0f), new Vector3(0.0f, -10.0f, 0.0f));
        float fromBelow = _world.CastCapsule(capsule, new Vector3(4.0f, -5.0f, 4.0f), new Vector3(0.0f, 10.0f, 0.0f));

        Assert.True(fromAbove < 1.0f, "the cast from above should land on the terrain");
        Assert.Equal(1.0f, fromBelow);
    }

    [NativeFact]
    public void A_capsule_under_a_mesh_touches_nothing()
    {
        using CollisionMesh grid = CollisionMesh.Grid(8, 8, 1.0f);
        _world.CreateStaticBody().AddMesh(grid);
        _world.Step(1.0f / 60.0f);

        // Its top cap pokes through the underside of the floor.
        var callback = new CountContacts();
        _world.CollideCapsule(Capsule.Upright(1.8f, 0.3f), new Vector3(0.3f, -1.0f, 0.4f), ref callback);

        Assert.Equal(0, callback.Count);
    }

    // --------------------------------------------------------- time of impact

    private static readonly Capsule Character = Capsule.Upright(1.8f, 0.3f);

    // A crate one metre to the side of a character standing at the origin.
    private Body CreateCrate(Vector3 position, BodyType type = BodyType.Kinematic)
    {
        Body crate = _world.CreateBody(BodyDefinition.Default with { Type = type, Position = position });
        crate.AddBox(Box.Cube(0.5f));
        return crate;
    }

    [NativeFact]
    public void A_body_swept_into_a_standing_character_reports_the_impact()
    {
        Body crate = CreateCrate(new Vector3(3.0f, 0.0f, 0.0f));

        // The crate slides from x = 3 to x = -1 while the character stands still.
        CharacterImpact impact = CharacterMover.TimeOfImpact(
            crate, Character, Vector3.Zero, Vector3.Zero,
            new Vector3(3.0f, 0.0f, 0.0f), Quaternion.Identity,
            new Vector3(-1.0f, 0.0f, 0.0f), Quaternion.Identity);

        Assert.True(impact.Hit);
        Assert.Equal(crate, impact.Shape.Body);

        // The crate face at x - 0.5 meets the capsule surface at x = 0.3, so the
        // crate centre reaches x = 0.8: 2.2 of its 4 metres.
        Assert.True(impact.Fraction is > 0.5f and < 0.6f, $"impact at fraction {impact.Fraction}");

        // The normal points from the body to the character.
        Assert.True(impact.Normal.X < -0.9f, $"normal {impact.Normal}");
        Assert.True(MathF.Abs(impact.Point.X - 0.3f) < 0.05f, $"point {impact.Point}");
    }

    [NativeFact]
    public void A_character_walking_into_a_still_body_reports_the_impact()
    {
        Body crate = CreateCrate(new Vector3(3.0f, 0.0f, 0.0f));
        Vector3 pose = crate.Position;

        CharacterImpact impact = CharacterMover.TimeOfImpact(
            crate, Character, Vector3.Zero, new Vector3(4.0f, 0.0f, 0.0f),
            pose, Quaternion.Identity, pose, Quaternion.Identity);

        Assert.True(impact.Hit);

        // The capsule surface reaches the crate face at x = 2.5 after 2.2 metres.
        Assert.True(impact.Fraction is > 0.5f and < 0.6f, $"impact at fraction {impact.Fraction}");
    }

    [NativeFact]
    public void A_body_moving_away_misses_the_character()
    {
        Body crate = CreateCrate(new Vector3(3.0f, 0.0f, 0.0f));

        CharacterImpact impact = CharacterMover.TimeOfImpact(
            crate, Character, Vector3.Zero, Vector3.Zero,
            new Vector3(3.0f, 0.0f, 0.0f), Quaternion.Identity,
            new Vector3(6.0f, 0.0f, 0.0f), Quaternion.Identity);

        Assert.False(impact.Hit);
        Assert.Equal(1.0f, impact.Fraction);
        Assert.False(impact.Shape.IsValid);
    }

    [NativeFact]
    public void A_body_already_overlapping_the_character_is_ignored()
    {
        Body crate = CreateCrate(Vector3.Zero);

        CharacterImpact impact = CharacterMover.TimeOfImpact(
            crate, Character, Vector3.Zero, Vector3.Zero,
            new Vector3(0.2f, 0.0f, 0.0f), Quaternion.Identity,
            new Vector3(-0.2f, 0.0f, 0.0f), Quaternion.Identity);

        Assert.False(impact.Hit);
    }

    [NativeFact]
    public void The_filter_decides_which_shapes_on_the_body_can_strike()
    {
        Body crate = _world.CreateBody(BodyDefinition.Kinematic(new Vector3(3.0f, 0.0f, 0.0f)));
        crate.AddBox(Box.Cube(0.5f), ShapeDefinition.Default with
        {
            Filter = CollisionFilter.Default with { Categories = 0x2 },
        });

        CharacterImpact Sweep(QueryFilter filter) => CharacterMover.TimeOfImpact(
            crate, Character, Vector3.Zero, Vector3.Zero,
            new Vector3(3.0f, 0.0f, 0.0f), Quaternion.Identity,
            new Vector3(-1.0f, 0.0f, 0.0f), Quaternion.Identity,
            filter);

        Assert.True(Sweep(QueryFilter.Default).Hit);
        Assert.False(Sweep(QueryFilter.Default with { CollidesWith = 0x4 }).Hit);
    }

    [NativeFact]
    public void A_falling_body_strikes_the_character_during_a_step()
    {
        // The way the sweep is meant to be used: record the pose, step, then
        // sweep from the old pose to the new one.
        Body crate = CreateCrate(new Vector3(0.0f, 2.0f, 0.0f), BodyType.Dynamic);
        crate.LinearVelocity = new Vector3(0.0f, -40.0f, 0.0f);

        Vector3 start = crate.Position;
        Quaternion startRotation = crate.Rotation;

        _world.Step(1.0f / 60.0f);

        CharacterImpact impact = CharacterMover.TimeOfImpact(
            crate, Character, Vector3.Zero, Vector3.Zero,
            start, startRotation, crate.Position, crate.Rotation);

        Assert.True(impact.Hit, $"the crate fell from {start} to {crate.Position} without striking");
        Assert.True(impact.Normal.Y < -0.9f, $"a falling crate strikes from above, normal {impact.Normal}");
        Assert.True(impact.Fraction is > 0.0f and < 1.0f);
    }

    [NativeFact]
    public void Only_convex_shapes_on_a_body_are_swept()
    {
        // Box3D sweeps spheres, capsules and hulls. A compound on its own body
        // is skipped, so the same geometry reports nothing.
        using CompoundGeometry compound = new CompoundBuilder()
            .AddSphere(new Sphere(0.5f))
            .Build();

        Body body = _world.CreateStaticBody(new Vector3(3.0f, 0.0f, 0.0f));
        body.AddCompound(compound);

        CharacterImpact impact = CharacterMover.TimeOfImpact(
            body, Character, Vector3.Zero, new Vector3(4.0f, 0.0f, 0.0f),
            body.Position, Quaternion.Identity, body.Position, Quaternion.Identity);

        Assert.False(impact.Hit);
    }

    [NativeFact]
    public void A_stale_body_cannot_be_swept()
    {
        Body crate = CreateCrate(new Vector3(3.0f, 0.0f, 0.0f));
        crate.Destroy();

        Assert.Throws<InvalidOperationException>(() => CharacterMover.TimeOfImpact(
            crate, Character, Vector3.Zero, Vector3.Zero,
            Vector3.Zero, Quaternion.Identity, Vector3.Zero, Quaternion.Identity));
    }
}
