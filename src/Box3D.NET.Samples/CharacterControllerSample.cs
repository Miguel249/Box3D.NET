// SPDX-License-Identifier: MIT

using System;
using System.Numerics;

namespace Box3D.Samples;

/// <summary>
/// A kinematic character controller built from the mover primitives.
/// </summary>
/// <remarks>
/// <para>
/// This is the sample to copy for player movement. It is a complete controller
/// in about eighty lines: gravity, walking, jumping, ground detection, slope
/// limiting and sliding along walls.
/// </para>
/// <para>
/// None of that logic lives in Box3D.NET, on purpose. What counts as ground,
/// how high the character jumps and how it behaves on a slope are game design
/// decisions, and every game answers them differently. The library gives you
/// the two hard parts - finding the planes and solving them - and stays out of
/// the rest.
/// </para>
/// </remarks>
internal static class CharacterControllerSample
{
    // How steep a surface can be before the character slides off it instead of
    // standing on it. Forty-five degrees.
    private static readonly float MinimumGroundNormalY = MathF.Cos(MathF.PI / 4.0f);

    private const int MaxPlanes = 16;

    /// <summary>Gathers the surfaces the capsule is touching, without allocating.</summary>
    private struct GatherPlanes : ICharacterCollisionCallback
    {
        public CollisionPlane[] Planes;
        public int Count;
        public bool OnGround;

        public bool OnContact(in CharacterContact contact)
        {
            // A surface counts as ground when it faces up steeply enough to
            // stand on. Anything shallower is a wall the character slides along.
            if (contact.Normal.Y >= MinimumGroundNormalY)
            {
                OnGround = true;
            }

            if (Count < Planes.Length)
            {
                Planes[Count++] = CollisionPlane.From(contact);
            }

            return true;
        }
    }

    private sealed class Character
    {
        private readonly PhysicsWorld _world;
        private readonly CollisionPlane[] _planes = new CollisionPlane[MaxPlanes];
        private readonly Capsule _capsule = Capsule.Upright(height: 1.8f, radius: 0.3f);

        public Character(PhysicsWorld world, Vector3 start)
        {
            _world = world;
            Position = start;
        }

        public Vector3 Position { get; private set; }

        public Vector3 Velocity { get; private set; }

        public bool OnGround { get; private set; }

        public void Jump(float speed)
        {
            if (OnGround)
            {
                Velocity = Velocity with { Y = speed };
                OnGround = false;
            }
        }

        public void Update(Vector3 walkDirection, float walkSpeed, float timeStep)
        {
            // 1. Integrate the forces the game applies. Gravity is the game's
            //    business here, not the solver's: a kinematic character is not
            //    simulated, it is moved.
            Velocity = Velocity with { Y = Velocity.Y - (20.0f * timeStep) };

            Vector3 horizontal = walkDirection * walkSpeed;
            Velocity = new Vector3(horizontal.X, Velocity.Y, horizontal.Z);

            // 2. Ask the world what the capsule is touching where it is now.
            var gather = new GatherPlanes { Planes = _planes };
            _world.CollideCapsule(_capsule, Position, ref gather);

            OnGround = gather.OnGround;

            Span<CollisionPlane> planes = _planes.AsSpan(0, gather.Count);

            // 3. Solve for the movement that satisfies every plane at once. This
            //    is what makes the character slide along a wall rather than stop
            //    against it, and what keeps it out of corners.
            PlaneSolverResult result = CharacterMover.SolvePlanes(Velocity * timeStep, planes);
            Position += result.Translation;

            // 4. Clip the velocity against the planes that actually resisted, or
            //    the character keeps accumulating speed into a wall and shoots
            //    sideways the moment the wall ends.
            Velocity = CharacterMover.ClipVelocity(Velocity, planes);
        }
    }

    public static void Run()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -20.0f, 0.0f),
        });

        // Flat ground with a ramp on one side and a wall on the other.
        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(30.0f, 0.5f, 30.0f)));

        // Long enough that the character cannot simply walk around the end of it
        // during the test below, which would look like the solver failing.
        Body wall = world.CreateStaticBody(new Vector3(8.0f, 1.5f, 0.0f));
        wall.AddBox(new Box(new Vector3(0.25f, 1.5f, 28.0f)));

        // A twenty degree ramp, shallow enough to be under the forty-five degree
        // walk limit. Rotating a slab about z tilts its top face so that it
        // climbs towards positive x, and the body is lifted so the low end meets
        // the ground rather than being buried in it.
        const float RampAngle = MathF.PI / 9.0f;
        const float RampHalfLength = 5.0f;

        Body ramp = world.CreateStaticBody(
            new Vector3(-10.0f, RampHalfLength * MathF.Sin(RampAngle), 0.0f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, RampAngle));
        ramp.AddBox(new Box(new Vector3(RampHalfLength, 0.25f, 4.0f)));

        world.Step(1.0f / 60.0f);

        const float TimeStep = 1.0f / 60.0f;
        var character = new Character(world, new Vector3(0.0f, 2.0f, 0.0f));

        // Fall to the ground.
        for (int i = 0; i < 60; i++)
        {
            character.Update(Vector3.Zero, 0.0f, TimeStep);
        }

        Console.WriteLine($"   landed at     : y = {character.Position.Y:F2}");
        Console.WriteLine($"   on ground     : {character.OnGround}");

        SampleRunner.Expect(character.OnGround, "the character found the floor");
        SampleRunner.Expect(character.Position.Y is > 0.8f and < 1.0f, "it rests at capsule half-height");

        // Jump.
        float beforeJump = character.Position.Y;
        character.Jump(8.0f);

        float peak = beforeJump;
        for (int i = 0; i < 60; i++)
        {
            character.Update(Vector3.Zero, 0.0f, TimeStep);
            peak = MathF.Max(peak, character.Position.Y);
        }

        Console.WriteLine($"   jump peak     : {peak - beforeJump:F2} m");

        SampleRunner.Expect(peak - beforeJump > 1.0f, "the jump left the ground");
        SampleRunner.Expect(character.OnGround, "and landed again");

        // Walk into the wall. The plane solver should slide the character along
        // it rather than stopping dead or letting it through.
        Vector3 diagonal = Vector3.Normalize(new Vector3(1.0f, 0.0f, 1.0f));
        for (int i = 0; i < 240; i++)
        {
            character.Update(diagonal, 4.0f, TimeStep);
        }

        Console.WriteLine($"   after wall    : {character.Position}");

        // The wall face is at x = 7.75, the capsule radius is 0.3.
        SampleRunner.Expect(character.Position.X < 7.6f, "the character did not pass through the wall");
        SampleRunner.Expect(character.Position.Z > 3.0f, "it slid along the wall instead of stopping");

        // Walk up the ramp. Start beyond its low end and walk towards positive x,
        // which is uphill.
        var climber = new Character(world, new Vector3(-17.0f, 1.5f, 0.0f));
        for (int i = 0; i < 30; i++)
        {
            climber.Update(Vector3.Zero, 0.0f, TimeStep);
        }

        float footOfRamp = climber.Position.Y;

        for (int i = 0; i < 240; i++)
        {
            climber.Update(Vector3.UnitX, 3.0f, TimeStep);
        }

        float climbed = climber.Position.Y - footOfRamp;

        Console.WriteLine($"   climbed       : {climbed:F2} m up a {RampAngle * 180.0f / MathF.PI:F0} degree ramp");

        SampleRunner.Expect(climbed > 0.3f, "the character walked up the slope");
    }
}
