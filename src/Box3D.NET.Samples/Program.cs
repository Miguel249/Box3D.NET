// SPDX-License-Identifier: MIT

using System;
using Box3D.Samples;

// Every sample runs headless and prints what it did, so this doubles as a smoke
// test: continuous integration publishes it with NativeAOT and runs it, which
// proves the whole stack survives ahead-of-time compilation and that nothing on
// these paths needs the JIT.

Console.WriteLine($"Box3D.NET samples, running against Box3D {SampleRunner.NativeVersion}");
Console.WriteLine();

SampleRunner.Run("Basic world", BasicWorldSample.Run);
SampleRunner.Run("Dynamic body", DynamicBodySample.Run);
SampleRunner.Run("Static body and collision", CollisionSample.Run);
SampleRunner.Run("Raycast", RaycastSample.Run);
SampleRunner.Run("Contact events", ContactEventsSample.Run);
SampleRunner.Run("Sensor trigger", SensorSample.Run);
SampleRunner.Run("Compound body", CompoundShapeSample.Run);
SampleRunner.Run("Continuous collision", ContinuousCollisionSample.Run);
SampleRunner.Run("Height field terrain", HeightFieldSample.Run);
SampleRunner.Run("Triangle mesh", MeshSample.Run);
SampleRunner.Run("Character controller", CharacterControllerSample.Run);
SampleRunner.Run("Entities and user data", EntitySample.Run);
SampleRunner.Run("Debug draw", DebugDrawSample.Run);
SampleRunner.Run("Hinged door", HingedDoorSample.Run);
SampleRunner.Run("Chain", ChainSample.Run);
SampleRunner.Run("Vehicle", VehicleSample.Run);

Console.WriteLine();
Console.WriteLine("All samples completed.");
