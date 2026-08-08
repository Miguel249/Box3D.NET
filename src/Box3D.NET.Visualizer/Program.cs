// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using Box3D.Native;
using Box3D.Visualizer.Scenes;

namespace Box3D.Visualizer;

/// <summary>
/// Renders the documentation gallery.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var requested = new List<string>();
        string? output = null;
        bool animate = true;
        bool fast = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--list":
                    List();
                    return 0;

                case "--help" or "-h":
                    Usage();
                    return 0;

                case "--out":
                    if (++i >= args.Length)
                    {
                        Console.Error.WriteLine("--out needs a directory.");
                        return 1;
                    }

                    output = args[i];
                    break;

                case "--no-gif":
                    animate = false;
                    break;

                case "--fast":
                    fast = true;
                    break;

                default:
                    if (args[i].StartsWith('-'))
                    {
                        Console.Error.WriteLine($"Unknown option {args[i]}.");
                        Usage();
                        return 1;
                    }

                    requested.Add(args[i]);
                    break;
            }
        }

        List<Scene> scenes = Select(requested);
        if (scenes.Count == 0)
        {
            Console.Error.WriteLine("No scene matched. Run with --list to see the names.");
            return 1;
        }

        var options = new RenderOptions
        {
            OutputDirectory = output ?? DefaultOutputDirectory(),
            Animate = animate,
            StillWidth = fast ? 640 : 1280,
            StillHeight = fast ? 360 : 720,
            StillSupersample = fast ? 1 : 3,
            AnimationSupersample = fast ? 1 : 2,
            Stride = fast ? 6 : 3,
        };

        b3Version version = B3.b3GetVersion();
        Console.WriteLine($"Box3D {version.major}.{version.minor}.{version.revision}, rendering to {options.OutputDirectory}");
        Console.WriteLine();

        foreach (Scene scene in scenes)
        {
            Console.Write($"  {scene.Name,-10} ");

            RenderReport report = SceneRenderer.Render(scene, options);

            Console.Write($"{report.DrawablesBuilt,3} drawables");
            Console.Write($" · {Size(report.StillPath),8} still");

            if (report.AnimationPath is not null)
            {
                Console.Write($" · {Size(report.AnimationPath),8} over {report.FrameCount,3} frames");
            }

            Console.Write($" · {report.Elapsed.TotalSeconds,5:F1} s");

            if (report.ShapesSkipped > 0)
            {
                Console.Write($" · {report.ShapesSkipped} shape(s) not drawn");
            }

            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("Done.");
        return 0;
    }

    private static List<Scene> Select(List<string> requested)
    {
        List<Scene> all = Gallery.Create();

        if (requested.Count == 0)
        {
            return all;
        }

        var selected = new List<Scene>();

        foreach (string name in requested)
        {
            Scene? match = all.Find(scene => string.Equals(scene.Name, name, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                Console.Error.WriteLine($"No scene called '{name}'.");
                continue;
            }

            selected.Add(match);
        }

        return selected;
    }

    private static void List()
    {
        foreach (Scene scene in Gallery.Create())
        {
            Console.WriteLine($"  {scene.Name,-10} {scene.Caption}");
        }
    }

    private static void Usage()
    {
        Console.WriteLine("Renders the Box3D.NET documentation gallery.");
        Console.WriteLine();
        Console.WriteLine("  dotnet run --project src/Box3D.NET.Visualizer -- [scene...] [options]");
        Console.WriteLine();
        Console.WriteLine("  --list          the scenes and what they show");
        Console.WriteLine("  --out <dir>     where the files go, default assets/renders");
        Console.WriteLine("  --no-gif        write only the still images");
        Console.WriteLine("  --fast          small and grainy, for checking a change quickly");
    }

    private static string Size(string path)
    {
        long bytes = new FileInfo(path).Length;

        return bytes < 1024L * 1024L
            ? $"{bytes / 1024.0:F0} KB"
            : $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private static string DefaultOutputDirectory()
    {
        // Walk up from the binary looking for the repository, so that a plain
        // `dotnet run` writes where the documentation expects the files rather
        // than into bin/Debug.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Box3D.NET.sln")))
            {
                return Path.Combine(directory.FullName, "assets", "renders");
            }

            directory = directory.Parent;
        }

        return Path.Combine(Environment.CurrentDirectory, "renders");
    }
}

/// <summary>The scenes, in the order the documentation shows them.</summary>
internal static class Gallery
{
    /// <summary>Builds one of each.</summary>
    /// <returns>The scenes.</returns>
    /// <remarks>
    /// New instances every time: a scene holds the state of its own run, so
    /// rendering the same one twice from one instance would carry the first
    /// run's positions into the second.
    /// </remarks>
    public static List<Scene> Create() =>
    [
        new StackScene(),
        new PourScene(),
        new ContactsScene(),
        new ChainScene(),
        new VehicleScene(),
        new RaycastScene(),
        new CharacterScene(),
        new TerrainScene(),
    ];
}
