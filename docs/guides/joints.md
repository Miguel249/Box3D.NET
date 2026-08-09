# Joints

A joint constrains how two bodies may move relative to each other. There are
nine, each with its own handle type and its own definition.

```csharp
// A door that opens ninety degrees and swings shut behind you.
RevoluteJoint hinge = world.CreateRevoluteJoint(
    RevoluteJointDefinition.Hinge(frame, door, hingePoint, Vector3.UnitY) with
    {
        LimitsEnabled = true,
        LowerAngle = 0.0f,
        UpperAngle = MathF.PI * 0.5f,
        MotorEnabled = true,
        MotorSpeed = -1.0f,
        MaxMotorTorque = 50.0f,
    });
```

## The nine

| Joint | Leaves free | Built with | For |
| --- | --- | --- | --- |
| `Revolute` | one rotation axis | `Hinge(a, b, anchor, axis)` | Doors, wheels, chains, ragdoll elbows |
| `Prismatic` | one translation axis | `Slider(a, b, anchor, axis)` | Lifts, pistons, drawers |
| `Distance` | everything, at a fixed range | `Between(a, b, anchorA, anchorB)` | Ropes, springs, struts |
| `Spherical` | all three rotations | `BallAndSocket(a, b, anchor, axis)` | Shoulders, hips, pendulums |
| `Weld` | nothing | `Weld(a, b, anchor)` | Rigid assemblies, breakable joins |
| `Wheel` | spin plus suspension travel | `Suspension(chassis, wheel, anchor, axis)` | Vehicles |
| `Motor` | everything, while driving a target pose | `MotorJointDefinition` | Followers, mouse dragging, active props |
| `Parallel` | everything but the frame's z axis | `ParallelJointDefinition` | Keeping something upright without locking it |
| `Filter` | everything | `Between(a, b)` | Two bodies that must not collide |

Each `Create…Joint` returns the specific handle, not a generic one, so
`hinge.MotorSpeed` compiles and `hinge.MinLength` does not. The shared members
are one hop away through `hinge.AsJoint`.

## Use the factory methods

A joint needs a *pair* of local frames describing the same world pose from each
body's point of view. Get that wrong and the joint starts out violated and snaps
on the first step.

`Hinge`, `Slider`, `Between`, `BallAndSocket`, `Weld` and `Suspension` derive
that pair from a world-space anchor and axis, which is how you would describe
the joint out loud. For the joints without a factory, or for a frame you want to
build yourself, [`Joint.FramesFromWorldAnchor`](../api/Box3D.Joint.yml) does the same
calculation.

Build the assembly in its rest pose. A chain assembled already displaced has
every joint violated on the first step and snaps — give it angular velocity
instead.

## Limits and motors

Most joints take limits, a motor, or both. The pattern is the same everywhere:
an `…Enabled` flag, the range, and a maximum force or torque the motor may
spend.

```csharp
// A lift that travels four metres straight up.
PrismaticJoint lift = world.CreatePrismaticJoint(
    PrismaticJointDefinition.Slider(shaft, platform, basePoint, Vector3.UnitY) with
    {
        LimitsEnabled = true,
        LowerTranslation = 0.0f,
        UpperTranslation = 4.0f,
        MotorEnabled = true,
        MotorSpeed = 1.0f,
        MaxMotorForce = 5000.0f,
    });
```

| Joint | Limits | Motor |
| --- | --- | --- |
| `Revolute` | `LowerAngle`, `UpperAngle` | `MotorSpeed`, `MaxMotorTorque` |
| `Prismatic` | `LowerTranslation`, `UpperTranslation` | `MotorSpeed`, `MaxMotorForce` |
| `Distance` | `MinLength`, `MaxLength` | `MotorSpeed`, `MaxMotorForce` |
| `Spherical` | `ConeAngle`, `LowerTwistAngle`, `UpperTwistAngle` | `MotorVelocity`, `MaxMotorTorque` |
| `Wheel` | suspension and steering, each with its own pair | `SpinSpeed`/`MaxSpinTorque`, `TargetSteeringAngle`/`MaxSteeringTorque` |

A motor with an unlimited maximum will hold anything, including things it should
not. The maximum is what makes a door closer stop when you push against it.

## Springs

Several joints can be soft rather than rigid, described in hertz and a damping
ratio rather than in stiffness:

```csharp
var rope = DistanceJointDefinition.Between(anchor, load, top, hook) with
{
    SpringEnabled = true,
    Hertz = 4.0f,           // how fast it oscillates
    DampingRatio = 0.5f,    // 1.0 is critically damped
    LimitsEnabled = true,
    MinLength = 0.1f,
    MaxLength = 3.0f,
};
```

A wheel is the same idea twice under different names: `SuspensionHertz` and
`SuspensionDampingRatio` for the travel, `SteeringHertz` and
`SteeringDampingRatio` for how sharply it turns to a target angle. `Parallel`
uses a spring to keep two frames' z axes aligned, which is how you keep a body
upright without locking its rotation outright.

## Reading a joint back

```csharp
Vector3 force = hinge.AsJoint.ConstraintForce;
float drift = hinge.AsJoint.LinearSeparation;

if (force.Length() > BreakingForce)
{
    hinge.AsJoint.Destroy();
}
```

`ForceThreshold` and `TorqueThreshold` on the base definition make the engine
report when a joint is overloaded, which is the ingredient for something that
breaks under load.

## Bodies that should not collide

Connected bodies do not collide by default. Set `CollideConnected` on the base
definition when they should — a wheel that must still hit the ground it sits on.

For two bodies that are not jointed at all but must not collide, use a filter
joint rather than spending a [category bit](collision-filtering.md):

```csharp
world.CreateFilterJoint(FilterJointDefinition.Between(turret, shell));
```

## Tuning

Every joint carries `ConstraintHertz` and `ConstraintDampingRatio` on its base
definition, which control how hard the solver works to hold it together. Raise
the hertz for an assembly that visibly stretches under load; lower it for one
that jitters.

```csharp
joint.SetConstraintTuning(hertz: 60.0f, dampingRatio: 2.0f);
```

`DrawScale` decides how large the joint's markers are when
[drawn](debug-draw.md). Joints are one of the things worth seeing before you
believe them.
