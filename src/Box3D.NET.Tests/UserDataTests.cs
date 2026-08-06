// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Covers the application identifier carried by worlds, bodies, shapes and joints.
/// </summary>
[Collection(NativeCollection.Name)]
public class UserDataTests : IDisposable
{
    private readonly PhysicsWorld _world;

    public UserDataTests() => _world = new PhysicsWorld();

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    [NativeFact]
    public void A_body_remembers_its_identifier()
    {
        Body body = _world.CreateDynamicBody(Vector3.Zero);

        Assert.Equal(0UL, body.UserData);

        body.UserData = 42;

        Assert.Equal(42UL, body.UserData);
    }

    [NativeFact]
    public void The_identifier_survives_the_full_range_of_a_ulong()
    {
        // The value crosses the boundary as a void*, so the top bit is where a
        // sign-extension bug would show up.
        Body body = _world.CreateDynamicBody(Vector3.Zero);

        foreach (ulong value in new[] { 0UL, 1UL, ulong.MaxValue, ulong.MaxValue / 2, 1UL << 63 })
        {
            body.UserData = value;
            Assert.Equal(value, body.UserData);
        }
    }

    [NativeFact]
    public void Shapes_carry_an_identifier_separate_from_their_body()
    {
        Body body = _world.CreateDynamicBody(Vector3.Zero);
        body.UserData = 7;

        Shape torso = body.AddBox(Box.Cube(0.5f));
        Shape head = body.AddSphere(new Sphere(new Vector3(0.0f, 1.0f, 0.0f), 0.25f));

        torso.UserData = 100;
        head.UserData = 200;

        // The point of shape-level data: the body says which entity, the shape
        // says which part of it.
        Assert.Equal(7UL, body.UserData);
        Assert.Equal(100UL, torso.UserData);
        Assert.Equal(200UL, head.UserData);
        Assert.Equal(7UL, head.Body.UserData);
    }

    [NativeFact]
    public void A_joint_carries_an_identifier()
    {
        Body a = _world.CreateStaticBody(Vector3.Zero);
        a.AddBox(Box.Cube(0.25f));

        Body b = _world.CreateDynamicBody(new Vector3(1.0f, 0.0f, 0.0f));
        b.AddBox(Box.Cube(0.25f));

        Joint joint = _world.CreateRevoluteJoint(
            RevoluteJointDefinition.Hinge(a, b, Vector3.Zero, Vector3.UnitZ)).AsJoint;

        joint.UserData = 1234;

        Assert.Equal(1234UL, joint.UserData);
    }

    [NativeFact]
    public void The_world_carries_an_identifier()
    {
        _world.UserData = 99;

        Assert.Equal(99UL, _world.UserData);
    }

    [NativeFact]
    public void The_identifier_comes_back_from_a_ray_cast()
    {
        Body target = _world.CreateStaticBody(new Vector3(10.0f, 0.0f, 0.0f));
        Shape shape = target.AddSphere(new Sphere(1.0f));

        target.UserData = 555;
        shape.UserData = 777;

        _world.Step(1.0f / 60.0f);

        RaycastHit hit = _world.RaycastClosest(Vector3.Zero, new Vector3(20.0f, 0.0f, 0.0f));

        Assert.True(hit.Hit);
        Assert.Equal(777UL, hit.Shape.UserData);
        Assert.Equal(555UL, hit.Shape.Body.UserData);
    }

    [NativeFact]
    public void The_identifier_comes_back_from_a_move_event()
    {
        Body body = _world.CreateDynamicBody(new Vector3(0.0f, 10.0f, 0.0f));
        body.AddSphere(new Sphere(0.5f));
        body.UserData = 31337;

        _world.Gravity = new Vector3(0.0f, -10.0f, 0.0f);
        _world.Step(1.0f / 60.0f);

        bool seen = false;
        foreach (BodyMoveEvent moved in _world.Events.BodyMoves)
        {
            if (moved.Body.UserData == 31337)
            {
                seen = true;
            }
        }

        Assert.True(seen, "the move event should report the body identifier");
    }

    [NativeFact]
    public void Bodies_report_the_world_they_belong_to()
    {
        Body body = _world.CreateDynamicBody(Vector3.Zero);
        Shape shape = body.AddSphere(new Sphere(0.5f));

        Assert.Equal(_world.Reference, body.World);
        Assert.Equal(_world.Reference, shape.World);
        Assert.True(body.World.IsValid);
    }

    [NativeFact]
    public void A_body_from_another_world_is_distinguishable()
    {
        using var other = new PhysicsWorld();

        Body mine = _world.CreateDynamicBody(Vector3.Zero);
        Body theirs = other.CreateDynamicBody(Vector3.Zero);

        Assert.NotEqual(mine.World, theirs.World);
        Assert.Equal(other.Reference, theirs.World);
    }

    [NativeFact]
    public void The_shorthand_constructors_produce_the_right_body_types()
    {
        Assert.Equal(BodyType.Dynamic, _world.CreateDynamicBody(Vector3.Zero).Type);
        Assert.Equal(BodyType.Static, _world.CreateStaticBody(Vector3.Zero).Type);
        Assert.Equal(BodyType.Kinematic, _world.CreateKinematicBody(Vector3.Zero).Type);
    }

    [NativeFact]
    public void The_shorthand_constructors_place_the_body_where_asked()
    {
        Vector3 position = new(1.0f, 2.0f, 3.0f);
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f);

        Body body = _world.CreateDynamicBody(position, rotation);

        Assert.Equal(position, body.Position);
        Assert.Equal(rotation.X, body.Rotation.X, 5);
        Assert.Equal(rotation.W, body.Rotation.W, 5);
    }
}
