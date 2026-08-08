// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using Box3D.Samples;

// Every sample runs headless and prints what it did, so this doubles as a smoke
// test: continuous integration publishes it with NativeAOT and runs it, which
// proves the whole stack survives ahead-of-time compilation and that nothing on
// these paths needs the JIT.
//
// Running them all is the default, because that is what CI wants. Anyone
// reading the samples wants one of them, so a name argument selects it:
//
//   dotnet run --project src/Box3D.NET.Samples                 # all of them
//   dotnet run --project src/Box3D.NET.Samples -- raycast      # just that one
//   dotnet run --project src/Box3D.NET.Samples -- --list       # what there is

// The table is the single source of truth for the sample list: adding one here
// makes it runnable by name and listable, with nothing else to update.
(string Name, string Summary, Action Run)[] samples =
[
    ("basic-world", "A world, a body, a shape, a step.", BasicWorldSample.Run),
    ("dynamic-body", "Gravity acting on a falling body.", DynamicBodySample.Run),
    ("collision", "A falling box landing on static ground.", CollisionSample.Run),
    ("raycast", "Closest-hit and callback ray casts.", RaycastSample.Run),
    ("contact-events", "Reading contacts after a step.", ContactEventsSample.Run),
    ("sensor", "A trigger volume that reports overlaps without colliding.", SensorSample.Run),
    ("compound", "Several shapes on one body, and many baked into one shape.", CompoundShapeSample.Run),
    ("continuous", "A fast body that would otherwise tunnel through a wall.", ContinuousCollisionSample.Run),
    ("height-field", "Terrain from a height map.", HeightFieldSample.Run),
    ("mesh", "Collision against a triangle mesh.", MeshSample.Run),
    ("character", "A kinematic character walking, sliding and climbing.", CharacterControllerSample.Run),
    ("entities", "Associating game objects with bodies through user data.", EntitySample.Run),
    ("debug-draw", "Feeding the world's debug geometry to a renderer.", DebugDrawSample.Run),
    ("hinged-door", "A revolute joint with limits.", HingedDoorSample.Run),
    ("chain", "A hanging chain of revolute joints.", ChainSample.Run),
    ("vehicle", "A wheeled vehicle built from wheel joints.", VehicleSample.Run),
];

if (args.Length > 0 && (args[0] is "--list" or "-l" or "list"))
{
    Console.WriteLine("Samples:");
    foreach ((string name, string summary, _) in samples)
    {
        Console.WriteLine($"  {name,-16} {summary}");
    }

    return 0;
}

Console.WriteLine($"Box3D.NET samples, running against Box3D {SampleRunner.NativeVersion}");
Console.WriteLine();

if (args.Length > 0)
{
    string requested = args[0];

    var match = samples.FirstOrDefault(s =>
        string.Equals(s.Name, requested, StringComparison.OrdinalIgnoreCase));

    if (match.Run is null)
    {
        Console.Error.WriteLine($"No sample named '{requested}'.");
        Console.Error.WriteLine("Run with --list to see what there is.");
        return 1;
    }

    SampleRunner.Run(match.Summary, match.Run);
    return 0;
}

foreach ((string _, string summary, Action run) in samples)
{
    SampleRunner.Run(summary, run);
}

Console.WriteLine();
Console.WriteLine($"All {samples.Length} samples completed.");
return 0;

/// <summary>Entry point marker.</summary>
internal sealed partial class Program;
