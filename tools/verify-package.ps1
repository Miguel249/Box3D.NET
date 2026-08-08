#!/usr/bin/env pwsh
# SPDX-License-Identifier: MIT
#
# Consumes the built packages the way a stranger would, and proves the result
# actually simulates.
#
# This exists because building the repository proves almost nothing about the
# package. A project reference resolves assemblies from bin/, copies the native
# library through a target in Directory.Build.targets, and never touches
# runtimes/<rid>/native at all. Everything specific to the package - whether the
# native asset is in the right folder, whether the loader finds it, whether the
# assemblies survive trimming, whether NativeAOT can compile the P/Invokes - is
# untested until something installs the .nupkg.
#
# So this script builds a console application that has never heard of this
# repository, points it at a folder feed holding the freshly packed .nupkg,
# installs Box3D.NET by version, and runs a scene through it. Three ways:
#
#   framework-dependent   the ordinary case
#   trimmed               PublishTrimmed, self-contained
#   NativeAOT             PublishAot
#
# Each one creates a world, bodies and shapes, steps, raycasts, reads events and
# disposes, and reports what it saw. A physics result that is merely plausible is
# not enough: the consumer checks that the ball actually fell and actually
# stopped on the ground, so a library that loads but does nothing fails here.
#
# Usage:
#   pwsh tools/verify-package.ps1                    # all three, current RID
#   pwsh tools/verify-package.ps1 -Mode Aot          # just one
#   pwsh tools/verify-package.ps1 -Version 0.3.0

[CmdletBinding()]
param(
    # The package version to install. Inferred from the packages present when
    # omitted.
    [string] $Version,

    # Where the .nupkg files are.
    [string] $PackageDirectory = 'artifacts/packages',

    # Which publish modes to exercise.
    #
    # SelfContained exists for one situation: running a build for a runtime
    # identifier whose .NET runtime is not installed on this machine. A
    # framework-dependent osx-x64 binary on an arm64 Mac starts under Rosetta
    # and then fails to load libhostfxr.dylib, because the only .NET present is
    # arm64. That failure is about the machine, not about the package. A
    # self-contained build carries its own runtime and exercises the thing worth
    # exercising: that the package resolves its x64 native asset and that the
    # library loads and simulates.
    [ValidateSet('All', 'Framework', 'SelfContained', 'Trimmed', 'Aot')]
    [string] $Mode = 'All',

    # The runtime identifier to publish for. Inferred when omitted.
    [string] $Rid,

    # Where to build the consumer. Removed and recreated on each run, because a
    # stale obj/ is exactly the kind of state this script exists to avoid.
    [string] $WorkDirectory
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$PackageDirectory = Join-Path $RepoRoot $PackageDirectory

if (-not (Test-Path $PackageDirectory)) {
    throw "No package directory at $PackageDirectory. Run: dotnet pack --configuration Release --output artifacts/packages"
}

if (-not $Version) {
    $packages = Get-ChildItem $PackageDirectory -Filter 'Box3D.NET.*.nupkg' |
        Where-Object { $_.Name -match '^Box3D\.NET\.(\d+\.\d+\.\d+.*)\.nupkg$' }

    if (-not $packages) {
        throw "No Box3D.NET package found in $PackageDirectory."
    }

    $Version = [regex]::Match($packages[0].Name, '^Box3D\.NET\.(.+)\.nupkg$').Groups[1].Value
}

if (-not $Rid) {
    $arch = switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
        'X64'   { 'x64' }
        'Arm64' { 'arm64' }
        default { throw "Unsupported process architecture: $_" }
    }
    $Rid = if ($IsWindows -or $env:OS -eq 'Windows_NT') { "win-$arch" }
           elseif ($IsMacOS) { "osx-$arch" }
           else { "linux-$arch" }
}

if (-not $WorkDirectory) {
    $WorkDirectory = Join-Path $RepoRoot 'artifacts/package-consumer'
}

Write-Host "Package consumer verification"
Write-Host "  packages : $PackageDirectory"
Write-Host "  version  : $Version"
Write-Host "  runtime  : $Rid"
Write-Host "  work dir : $WorkDirectory"
Write-Host "  mode     : $Mode"

if (Test-Path $WorkDirectory) {
    Remove-Item -Recurse -Force $WorkDirectory
}
New-Item -ItemType Directory -Force $WorkDirectory | Out-Null

# NativeAOT on Windows links with MSVC, and the ILCompiler targets locate it by
# running vswhere.exe off PATH. A machine with Visual Studio installed but no
# developer prompt open has vswhere in a fixed location and not on PATH, and the
# failure is an unhelpful "'vswhere.exe' is not recognized" from inside a linker
# command line. Putting it on PATH here is not papering over anything: the
# toolchain is present, only unfindable.
if ($IsWindows -or $env:OS -eq 'Windows_NT') {
    $vsInstaller = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
    if ((Test-Path (Join-Path $vsInstaller 'vswhere.exe')) -and $env:PATH -notlike "*$vsInstaller*") {
        $env:PATH = "$vsInstaller;$env:PATH"
        Write-Host "  added the Visual Studio Installer directory to PATH so NativeAOT can find vswhere.exe"
    }
}

# ------------------------------------------------------------ the consumer

# A nuget.config naming only the folder feed and nuget.org, and clearing
# whatever the machine has configured. Without <clear/> a developer's own feeds
# take part, and the point of this script is that the result does not depend on
# the machine.
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$PackageDirectory" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -Path (Join-Path $WorkDirectory 'nuget.config') -Encoding utf8

# Deliberately no Directory.Build.props of its own, and the repository's must
# not reach it: the consumer is meant to be an ordinary project. It is outside
# the repository tree for framework and trimmed runs by virtue of living under
# artifacts/, which has no props file above it other than the repository root's,
# so the project opts out explicitly.
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <AssemblyName>consumer</AssemblyName>
    <RootNamespace>Consumer</RootNamespace>
    <ImportDirectoryBuildProps>false</ImportDirectoryBuildProps>
    <ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets>
    <!-- Built for net8.0 and run on whatever runtime the machine has. -->
    <RollForward>LatestMajor</RollForward>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
"@ | Set-Content -Path (Join-Path $WorkDirectory 'consumer.csproj') -Encoding utf8

# The scene. Every claim the README makes about the five-minute path is here:
# create a world, create bodies, attach shapes, step, raycast, read events,
# dispose. And it checks the physics rather than merely surviving it.
@'
using System;
using System.Numerics;
using Box3D;

internal static class Program
{
    private struct CountHits : IRaycastCallback
    {
        public int Count;

        public RaycastAction OnHit(in RaycastHit hit)
        {
            Count++;
            return RaycastAction.Continue;
        }
    }

    private static int Main()
    {
        int failures = 0;

        void Check(bool condition, string what)
        {
            Console.WriteLine($"  {(condition ? "ok  " : "FAIL")}  {what}");
            if (!condition)
            {
                failures++;
            }
        }

        Console.WriteLine($"Box3D.NET consumer smoke test on {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}");

        using (var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -9.81f, 0.0f),
        }))
        {
            Check(world.WorkerCount >= 1, "the native library loaded and the world was created");

            Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
            ground.AddBox(
                new Box(new Vector3(50.0f, 0.5f, 50.0f)),
                ShapeDefinition.Default with { EnableContactEvents = true });

            Body ball = world.CreateDynamicBody(new Vector3(0.0f, 10.0f, 0.0f));
            Shape ballShape = ball.AddSphere(
                new Sphere(0.5f),
                ShapeDefinition.Default with { EnableContactEvents = true });

            Check(ball.IsValid && ballShape.IsValid, "bodies and shapes were created");
            Check(ball.Mass > 0.0f, $"the ball has mass ({ball.Mass:F3} kg)");

            float startHeight = ball.Position.Y;

            int moveEvents = 0;
            int contactBegins = 0;

            for (int frame = 0; frame < 240; frame++)
            {
                world.Step(1.0f / 60.0f);

                foreach (BodyMoveEvent moved in world.Events.BodyMoves)
                {
                    moveEvents += moved.Body.IsValid ? 1 : 0;
                }

                foreach (ContactBeginEvent begin in world.Events.ContactBegins)
                {
                    contactBegins += begin.ShapeA.IsValid ? 1 : 0;
                }
            }

            float endHeight = ball.Position.Y;

            // The simulation really ran: the ball fell, and it stopped on the
            // ground rather than falling through it or never moving.
            Check(endHeight < startHeight - 8.0f, $"the ball fell ({startHeight:F2} m -> {endHeight:F2} m)");
            Check(Math.Abs(endHeight - 0.5f) < 0.1f, $"the ball came to rest on the ground ({endHeight:F3} m)");
            Check(moveEvents > 0, $"body move events were raised ({moveEvents})");
            Check(contactBegins > 0, $"a contact begin event was raised ({contactBegins})");

            // Queries.
            RaycastHit hit = world.RaycastClosest(new Vector3(0.0f, 20.0f, 0.0f), new Vector3(0.0f, -30.0f, 0.0f));
            Check(hit.Hit, "the ray hit something");
            Check(hit.Shape.IsValid, "the ray reported a live shape");

            CountHits all = default;
            world.Raycast(new Vector3(0.0f, 20.0f, 0.0f), new Vector3(0.0f, -30.0f, 0.0f), ref all);
            Check(all.Count >= 2, $"the callback raycast saw the ball and the ground ({all.Count} hits)");

            // The stale-handle guard, which is a managed-side behaviour and has
            // to survive trimming and AOT along with everything else.
            Body destroyed = world.CreateDynamicBody(new Vector3(20.0f, 5.0f, 0.0f));
            destroyed.Destroy();
            bool threw = false;
            try
            {
                _ = destroyed.Position;
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Check(!destroyed.IsValid, "a destroyed body reports itself invalid");
            Check(threw, "reading a destroyed body throws rather than crashing");
        }

        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
'@ | Set-Content -Path (Join-Path $WorkDirectory 'Program.cs') -Encoding utf8

Push-Location $WorkDirectory
try {
    Write-Host "`n> dotnet add package Box3D.NET --version $Version"
    & dotnet add package Box3D.NET --version $Version
    if ($LASTEXITCODE -ne 0) { throw "dotnet add package failed with exit code $LASTEXITCODE" }

    $results = [ordered] @{}

    function Invoke-Consumer([string] $name, [string[]] $publishArgs, [string] $exeSubPath) {
        Write-Host "`n=== $name"
        Write-Host "> dotnet publish $($publishArgs -join ' ')"

        & dotnet publish @publishArgs
        if ($LASTEXITCODE -ne 0) {
            $script:results[$name] = "publish FAILED (exit $LASTEXITCODE)"
            Write-Host "::error::$name publish failed"
            return
        }

        $exe = Join-Path $WorkDirectory $exeSubPath
        if ($IsWindows -or $env:OS -eq 'Windows_NT') { $exe += '.exe' }

        if (-not (Test-Path $exe)) {
            $script:results[$name] = "published but no executable at $exeSubPath"
            Write-Host "::error::$name produced no executable"
            return
        }

        $size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
        Write-Host "> $exe ($size MB)"

        & $exe
        $code = $LASTEXITCODE

        $script:results[$name] = if ($code -eq 0) { "PASS ($size MB)" } else { "RAN BUT FAILED (exit $code)" }
    }

    if ($Mode -in 'All', 'Framework') {
        Invoke-Consumer 'framework-dependent' `
            @('--configuration', 'Release', '--runtime', $Rid, '--self-contained', 'false') `
            "bin/Release/net8.0/$Rid/publish/consumer"
    }

    if ($Mode -eq 'SelfContained') {
        Invoke-Consumer 'self-contained' `
            @('--configuration', 'Release', '--runtime', $Rid, '--self-contained', 'true') `
            "bin/Release/net8.0/$Rid/publish/consumer"
    }

    if ($Mode -in 'All', 'Trimmed') {
        Invoke-Consumer 'trimmed' `
            @('--configuration', 'Release', '--runtime', $Rid, '--self-contained', 'true',
              '-p:PublishTrimmed=true', '-p:TrimMode=full', '-p:SuppressTrimAnalysisWarnings=false',
              '-p:TreatWarningsAsErrors=true') `
            "bin/Release/net8.0/$Rid/publish/consumer"
    }

    if ($Mode -in 'All', 'Aot') {
        Invoke-Consumer 'NativeAOT' `
            @('--configuration', 'Release', '--runtime', $Rid, '-p:PublishAot=true',
              '-p:TreatWarningsAsErrors=true') `
            "bin/Release/net8.0/$Rid/publish/consumer"
    }

    Write-Host "`n================ results ($Rid, Box3D.NET $Version)"
    $failed = $false
    foreach ($entry in $results.GetEnumerator()) {
        Write-Host ("  {0,-20} {1}" -f $entry.Key, $entry.Value)
        if ($entry.Value -notlike 'PASS*') { $failed = $true }
    }

    if ($failed) {
        throw 'One or more consumer verifications failed.'
    }

    Write-Host "`nEvery mode installed the package, loaded the native library and simulated correctly."
}
finally {
    Pop-Location
}
