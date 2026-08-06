// SPDX-License-Identifier: MIT

using System;
using System.Numerics;

namespace Box3D.Samples;

/// <summary>
/// Connecting physics objects to game entities, the way an engine actually does it.
/// </summary>
/// <remarks>
/// This is the sample to copy when wiring Box3D.NET into a real game. Nothing
/// here allocates per frame, and the physics never holds a reference to game
/// state: it stores an entity id, and the game indexes its own arrays with it.
/// </remarks>
internal static class EntitySample
{
    // The application's own storage, laid out as parallel arrays. This is what
    // a component in an ECS looks like, and it is why UserData is an integer
    // rather than an object reference: an index is what indexes an array.
    private struct Transform
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    private enum HitZone : ulong
    {
        Body = 0,
        Head = 1,
    }

    public static void Run()
    {
        const int EntityCount = 64;

        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        var transforms = new Transform[EntityCount];
        var names = new string[EntityCount];

        // The shorthand constructors: no definition needed for a plain pose.
        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));

        for (int entity = 0; entity < EntityCount; entity++)
        {
            Body body = world.CreateDynamicBody(
                new Vector3((entity % 8) * 1.5f, 2.0f + ((entity / 8) * 1.5f), 0.0f));

            body.AddBox(Box.Cube(0.4f));

            // The link from physics back to the game. Box3D stores the number
            // and never looks at it.
            body.UserData = (ulong)entity;

            names[entity] = $"crate {entity}";
        }

        int transformUpdates = 0;

        for (int frame = 0; frame < 180; frame++)
        {
            world.Step(1.0f / 60.0f);

            // The efficient way to read results: one contiguous array holding
            // only the bodies that actually moved, rather than asking every body
            // for its transform every frame.
            foreach (BodyMoveEvent moved in world.Events.BodyMoves)
            {
                ulong entity = moved.Body.UserData;

                ref Transform transform = ref transforms[entity];
                transform.Position = moved.Position;
                transform.Rotation = moved.Rotation;

                transformUpdates++;
            }
        }

        Console.WriteLine($"   entities      : {EntityCount}");
        Console.WriteLine($"   transform sets: {transformUpdates}");
        Console.WriteLine($"   entity 0 at   : {transforms[0].Position}");

        SampleRunner.Expect(transformUpdates > 0, "the move events drove the transforms");
        SampleRunner.Expect(transforms[0].Position.Y < 2.0f, "entity 0 fell and its transform followed");

        // Shapes carry their own identifier, which is what lets a hit be
        // attributed to a part rather than merely to the object.
        //
        // Placed well clear of the crates above, which occupy x up to about
        // 10.5: a ray fired across them would report the first crate it met
        // rather than the character, which is correct behaviour and a useless
        // demonstration.
        Body character = world.CreateStaticBody(new Vector3(100.0f, 1.0f, 0.0f));

        Shape torso = character.AddBox(new Box(new Vector3(0.3f, 0.6f, 0.2f)));
        torso.UserData = (ulong)HitZone.Body;

        Shape head = character.AddSphere(new Sphere(new Vector3(0.0f, 0.85f, 0.0f), 0.2f));
        head.UserData = (ulong)HitZone.Head;

        character.UserData = 999;

        world.Step(1.0f / 60.0f);

        // A shot aimed at head height: the head sphere is centred 0.85 above the
        // body origin, which itself sits at y = 1.
        RaycastHit hit = world.RaycastClosest(
            new Vector3(90.0f, 1.85f, 0.0f),
            new Vector3(20.0f, 0.0f, 0.0f));

        if (hit.Hit)
        {
            var zone = (HitZone)hit.Shape.UserData;
            ulong entity = hit.Shape.Body.UserData;

            Console.WriteLine($"   hit entity    : {entity}");
            Console.WriteLine($"   hit zone      : {zone}");
            Console.WriteLine($"   damage        : {(zone == HitZone.Head ? 100 : 25)}");

            SampleRunner.Expect(entity == 999, "the body identifier came back from the ray cast");
            SampleRunner.Expect(zone == HitZone.Head, "the shape identifier distinguishes the head");
        }
        else
        {
            SampleRunner.Expect(false, "the ray should have hit the character");
        }

        // Bodies also know which world they came from, which is how to reject a
        // handle that belongs to a different simulation.
        SampleRunner.Expect(character.World == world.Reference, "the body belongs to this world");

        // Silence the unused-name warning while showing what the array is for.
        SampleRunner.Expect(names[0] is not null, "the parallel arrays stay in step with the entity id");
    }
}
