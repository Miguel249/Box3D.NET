// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer.Scenes;

/// <summary>
/// A sweep of ray casts over a field of shapes.
/// </summary>
/// <remarks>
/// Rays are the one thing in this gallery the engine cannot draw for you: a
/// cast leaves no trace in the world, so the scene draws what it asked and what
/// came back. That is the honest picture of a query anyway.
/// </remarks>
internal sealed class RaycastScene : Scene
{
    private const int RayCount = 28;
    private const float Range = 7.5f;
    private const float MuzzleHeight = 0.85f;

    /// <inheritdoc/>
    public override string Name => "raycast";

    /// <inheritdoc/>
    public override string Caption => "A sweeping fan of ray casts, with the closest hit and its normal on each one.";

    /// <inheritdoc/>
    /// <remarks>
    /// Exactly enough steps for the sweep below to come back round to where it
    /// started, so the animation loops without a jump.
    /// </remarks>
    public override int FrameCount => 180;

    /// <inheritdoc/>
    public override int HeroFrame => 104;

    /// <inheritdoc/>
    public override void Build(PhysicsWorld world, ShapeMeshFactory visuals)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visuals);

        SceneKit.Ground(world, visuals);

        // A ring of things to hit, static so that the picture is about the
        // query rather than about the simulation.
        var random = new Random(7);

        for (int i = 0; i < 14; i++)
        {
            int kind = i % 3;
            float angle = (i / 14.0f) * MathF.Tau;
            float radius = 3.4f + ((float)random.NextDouble() * 1.9f);
            float lift = kind == 2 ? 0.45f : 0.55f;

            Body body = world.CreateStaticBody(
                new Vector3(MathF.Cos(angle) * radius, lift, MathF.Sin(angle) * radius),
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)random.NextDouble() * MathF.Tau));

            Shape shape = kind switch
            {
                0 => body.AddBox(new Box(new Vector3(0.45f, 0.55f, 0.45f))),
                1 => body.AddCapsule(Capsule.Upright(1.1f, 0.3f)),
                _ => body.AddSphere(new Sphere(0.45f)),
            };

            visuals.Paint(shape, new Appearance(Palette.Static));
        }

        // Two dynamic bodies, so the sweep has something that moves between it
        // and the rest.
        for (int i = 0; i < 2; i++)
        {
            Body tumbler = world.CreateDynamicBody(new Vector3(-1.4f + (2.8f * i), 3.5f + i, 0.6f - (1.2f * i)));
            visuals.Paint(tumbler.AddBox(Box.Cube(0.4f)), new Appearance(Palette.Cool));
        }
    }

    /// <inheritdoc/>
    public override void Decorate(PhysicsWorld world, Renderer renderer, int frame)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(renderer);

        SceneKit.Grid(renderer, Vector3.Zero, 8.0f);

        Vector3 muzzle = new(0.0f, MuzzleHeight, 0.0f);
        float sweep = frame * 2.0f * (MathF.PI / 180.0f);

        renderer.DrawPoint(muzzle, 7.0f, Palette.Ray);

        for (int i = 0; i < RayCount; i++)
        {
            float angle = sweep + ((i / (float)RayCount) * MathF.Tau);
            Vector3 direction = new(MathF.Cos(angle), 0.0f, MathF.Sin(angle));

            RaycastHit hit = world.RaycastClosest(muzzle, direction * Range);

            if (!hit.Hit)
            {
                renderer.DrawLine(muzzle, muzzle + (direction * Range), Palette.Ray, 1.0f, 0.16f);
                continue;
            }

            renderer.DrawLine(muzzle, hit.Point, Palette.Ray, 1.2f, 0.55f);
            renderer.DrawPoint(hit.Point, 5.0f, Palette.Hit);
            renderer.DrawLine(hit.Point, hit.Point + (hit.Normal * 0.45f), Palette.Hit, 1.6f);
        }
    }

    /// <inheritdoc/>
    public override Camera GetCamera(int frame) =>
        Camera.Orbit(new Vector3(0.0f, 0.8f, 0.0f), 12.5f, 24.0f + (frame * 0.05f), 26.0f);
}

/// <summary>
/// A kinematic character walking, climbing and jumping.
/// </summary>
/// <remarks>
/// The controller is the one from the samples, unchanged: gather the planes the
/// capsule is touching, solve them, clip the velocity. The character is not a
/// body in the world - nothing simulates it - so the scene draws its own
/// capsule and the planes it found.
/// </remarks>
internal sealed class CharacterScene : Scene
{
    private const int MaxPlanes = 16;
    private const float Radius = 0.32f;
    private const float Height = 1.75f;

    private static readonly float MinimumGroundNormalY = MathF.Cos(MathF.PI / 4.0f);

    private readonly CollisionPlane[] _planes = new CollisionPlane[MaxPlanes];
    private readonly Vector3[] _contactPoints = new Vector3[MaxPlanes];
    private readonly Capsule _capsule = Capsule.Upright(Height, Radius);
    private readonly Mesh _mesh = Tessellate.Capsule(
        new Vector3(0.0f, -((Height * 0.5f) - Radius), 0.0f),
        new Vector3(0.0f, (Height * 0.5f) - Radius, 0.0f),
        Radius);

    private Vector3 _position = new(-4.5f, 1.6f, 0.0f);
    private Vector3 _velocity;
    private int _planeCount;
    private bool _onGround;

    /// <inheritdoc/>
    public override string Name => "character";

    /// <inheritdoc/>
    public override string Caption =>
        "A kinematic character on the mover primitives: gather the planes, solve them, clip the velocity.";

    /// <inheritdoc/>
    public override WorldSettings Settings => WorldSettings.Default with
    {
        Gravity = new Vector3(0.0f, -20.0f, 0.0f),
    };

    /// <inheritdoc/>
    public override int FrameCount => 320;

    /// <inheritdoc/>
    public override int HeroFrame => 177;

    /// <inheritdoc/>
    public override void Build(PhysicsWorld world, ShapeMeshFactory visuals)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visuals);

        SceneKit.Ground(world, visuals);

        // A ramp, and a platform at the top of it.
        //
        // The ramp is placed so that the low end of its *top face* meets the
        // ground exactly, with the wedge underneath buried. Sitting it on the
        // ground instead leaves a step the height of the slab at the bottom,
        // and the character reads that vertical face as a wall and stops dead
        // against it - which looks like the mover failing and is not.
        const float RampAngle = 0.32f;
        const float RampHalfLength = 3.0f;
        const float RampHalfThickness = 0.2f;
        const float RampFoot = 1.0f;

        float cosine = MathF.Cos(RampAngle);
        float sine = MathF.Sin(RampAngle);

        Body ramp = world.CreateStaticBody(
            new Vector3(
                RampFoot + (RampHalfLength * cosine) + (RampHalfThickness * sine),
                (RampHalfLength * sine) - (RampHalfThickness * cosine),
                0.0f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, RampAngle));

        visuals.Paint(
            ramp.AddBox(new Box(new Vector3(RampHalfLength, RampHalfThickness, 1.1f))),
            new Appearance(Palette.Static));

        float platformTop = 2.0f * RampHalfLength * sine;
        float platformFoot = RampFoot + (2.0f * RampHalfLength * cosine);

        Body platform = world.CreateStaticBody(
            new Vector3(platformFoot + 1.6f, platformTop - RampHalfThickness, 0.0f));

        visuals.Paint(
            platform.AddBox(new Box(new Vector3(1.6f, RampHalfThickness, 1.1f))),
            new Appearance(Palette.Static));

        // A crate on the way, to be pushed past rather than walked through.
        Body crate = world.CreateStaticBody(new Vector3(-2.0f, 0.3f, 0.9f));
        visuals.Paint(crate.AddBox(Box.Cube(0.3f)), new Appearance(Palette.Cool));
    }

    /// <inheritdoc/>
    public override void Update(PhysicsWorld world, ShapeMeshFactory visuals, int frame)
    {
        ArgumentNullException.ThrowIfNull(world);

        Vector3 walk = frame is > 20 and < 240 ? Vector3.UnitX : Vector3.Zero;

        if (frame == 250 && _onGround)
        {
            _velocity = _velocity with { Y = 9.0f };
        }

        // Gravity is the game's business for a character that is moved rather
        // than simulated.
        _velocity = _velocity with { Y = _velocity.Y - (20.0f * TimeStep) };
        _velocity = new Vector3(walk.X * 3.2f, _velocity.Y, walk.Z * 3.2f);

        var gather = new GatherPlanes { Planes = _planes, Points = _contactPoints };
        world.CollideCapsule(_capsule, _position, ref gather);

        _planeCount = gather.Count;
        _onGround = gather.OnGround;

        Span<CollisionPlane> planes = _planes.AsSpan(0, gather.Count);

        PlaneSolverResult result = CharacterMover.SolvePlanes(_velocity * TimeStep, planes);
        _position += result.Translation;
        _velocity = CharacterMover.ClipVelocity(_velocity, planes);
    }

    /// <inheritdoc/>
    public override void Decorate(PhysicsWorld world, Renderer renderer, int frame)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        SceneKit.Grid(renderer, new Vector3(_position.X, 0.0f, 0.0f), 8.0f);

        renderer.DrawMesh(_mesh, _position, Quaternion.Identity, _onGround ? Palette.Accent : Palette.Ray);

        // The planes the mover is solving against, drawn where they were found.
        // A contact point comes back relative to the query origin rather than
        // in world space, which is easy to miss and puts every marker on the
        // world origin if it is.
        for (int i = 0; i < _planeCount; i++)
        {
            Vector3 point = _position + _contactPoints[i];
            Vector3 normal = _planes[i].Normal;

            // Drawn through the character on purpose: the planes it is standing
            // on are underneath it, and a marker hidden by the body it belongs
            // to shows nothing at all.
            renderer.DrawPoint(point, 6.0f, Palette.Hit, alpha: 1.0f, onTop: true);
            renderer.DrawLine(point, point + (normal * 0.6f), Palette.Hit, 1.8f, alpha: 1.0f, onTop: true);
        }
    }

    /// <inheritdoc/>
    public override Camera GetCamera(int frame) =>
        Camera.Orbit(new Vector3(_position.X + 0.4f, _position.Y + 0.1f, 0.0f), 7.4f, 6.0f, 8.0f);

    /// <summary>Collects the surfaces the capsule is touching, without allocating.</summary>
    private struct GatherPlanes : ICharacterCollisionCallback
    {
        public CollisionPlane[] Planes;
        public Vector3[] Points;
        public int Count;
        public bool OnGround;

        public bool OnContact(in CharacterContact contact)
        {
            if (contact.Normal.Y >= MinimumGroundNormalY)
            {
                OnGround = true;
            }

            if (Count < Planes.Length)
            {
                Points[Count] = contact.Point;
                Planes[Count] = CollisionPlane.From(contact);
                Count++;
            }

            return true;
        }
    }
}
