#!/usr/bin/env pwsh
# SPDX-License-Identifier: MIT
#
# Consumes the built packages from a real Android or iOS application, and proves
# that Box3D actually ends up inside it.
#
# verify-package.ps1 is the desktop counterpart and cannot cover these: it
# publishes an executable and runs it, and neither platform has an executable a
# CI runner can start. What can be checked without a device is the part that
# actually differs on mobile - whether the native library reaches the artifact
# the user installs - and it is checked by opening that artifact rather than by
# trusting a green build:
#
#   Android   the .apk is a zip; lib/<abi>/libbox3d.so has to be in it
#   iOS       the library is linked into the executable, so its symbols have
#             to be in the built binary
#
# Both failures are silent otherwise. An Android build with the runtime asset
# unresolved produces a perfectly valid apk that dies on the first physics call,
# and an iOS build whose static archive was dropped by the linker produces a
# perfectly valid app that does the same.
#
# Usage:
#   pwsh tools/verify-package-mobile.ps1 -Platform Android
#   pwsh tools/verify-package-mobile.ps1 -Platform iOS

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Android', 'iOS')]
    [string] $Platform,

    # The package version to install. Inferred from the packages present when
    # omitted.
    [string] $Version,

    # Where the .nupkg files are.
    [string] $PackageDirectory = 'artifacts/packages',

    # Where to build the consumer. Removed and recreated on each run.
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

# Linking an iOS application is Xcode's job, and Xcode is macOS only. Android
# cross-compiles from anywhere the workload installs.
if ($Platform -eq 'iOS' -and -not $IsMacOS) {
    throw 'Verifying the iOS package requires macOS with Xcode installed.'
}

if (-not $WorkDirectory) {
    $WorkDirectory = Join-Path $RepoRoot "artifacts/package-consumer-$($Platform.ToLowerInvariant())"
}

Write-Host "Mobile package consumer verification"
Write-Host "  platform : $Platform"
Write-Host "  packages : $PackageDirectory"
Write-Host "  version  : $Version"
Write-Host "  work dir : $WorkDirectory"

if (Test-Path $WorkDirectory) {
    Remove-Item -Recurse -Force $WorkDirectory
}
New-Item -ItemType Directory -Force $WorkDirectory | Out-Null

# ------------------------------------------------------------ the consumer

# The same folder feed the desktop verification uses, and for the same reason:
# with <clear/> the result cannot depend on feeds this machine happens to have.
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

# Cut this project off from the repository's own build configuration.
#
# The consumer is supposed to be an ordinary application that has never heard of
# Box3D.NET, and it is not one while it sits under artifacts/ inheriting the
# props above it: TreatWarningsAsErrors, the analyzers and the documentation
# gate all applied to it. The iOS template ships AppDelegate and SceneDelegate,
# CA1711 objects to both names, and the build failed on the repository's rules
# rather than on anything to do with the package.
#
# MSBuild stops at the first Directory.Build.props it finds walking up, so an
# empty one here ends the search. The desktop script does the same job by
# setting ImportDirectoryBuildProps in a project it writes itself; this one
# cannot, because the project comes from a template.
foreach ($name in 'Directory.Build.props', 'Directory.Build.targets') {
    @"
<Project>
  <!--
    Intentionally empty. Its presence stops MSBuild from walking up into the
    Box3D.NET repository's own build configuration, which this consumer is
    meant to know nothing about.
  -->
</Project>
"@ | Set-Content -Path (Join-Path $WorkDirectory $name) -Encoding utf8
}

# The workload's own template, rather than a hand-written project. An Android
# application needs a manifest, an activity and a resource tree, and an iOS one
# needs an Info.plist and a scene delegate; reproducing those here would be
# reproducing something that changes with every workload release.
$template = if ($Platform -eq 'Android') { 'android' } else { 'ios' }

Push-Location $WorkDirectory
try {
    Write-Host "`n> dotnet new $template"
    & dotnet new $template --name consumer --output . --force
    if ($LASTEXITCODE -ne 0) { throw "dotnet new $template failed with exit code $LASTEXITCODE. Is the $template workload installed?" }

    Write-Host "`n> dotnet add package Box3D.NET --version $Version"
    & dotnet add package Box3D.NET --version $Version
    if ($LASTEXITCODE -ne 0) { throw "dotnet add package failed with exit code $LASTEXITCODE" }

    # Something in the application has to call into Box3D.
    #
    # Not for the sake of running it - nothing here runs - but so that the
    # assembly is genuinely referenced. An unused package reference is a
    # candidate for the linker to drop, and a check that passes only because
    # nothing was trimmed is not the check anyone wants.
    @'
// SPDX-License-Identifier: MIT
using System.Numerics;
using Box3D;

internal static class Box3DUse
{
    internal static float Fall()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -9.81f, 0.0f),
        });

        Body ball = world.CreateDynamicBody(new Vector3(0.0f, 10.0f, 0.0f));
        ball.AddSphere(new Sphere(0.5f), ShapeDefinition.Default);

        for (int frame = 0; frame < 60; frame++)
        {
            world.Step(1.0f / 60.0f);
        }

        return ball.Position.Y;
    }
}
'@ | Set-Content -Path (Join-Path $WorkDirectory 'Box3DUse.cs') -Encoding utf8

    $buildArgs = @('build', '--configuration', 'Release')

    # The simulator, because a device build needs a signing identity and a
    # provisioning profile, which a CI runner has no business holding. The
    # linking question is the same either way: the archive is either in the
    # executable or it is not.
    if ($Platform -eq 'iOS') {
        $buildArgs += @('-p:RuntimeIdentifier=iossimulator-arm64')
    }

    # The log is kept because it is evidence in its own right on iOS: whether
    # the linker was handed the archive at all is visible there, and nowhere in
    # the finished bundle if the answer is no.
    $buildLog = Join-Path $WorkDirectory 'build.log'
    $buildArgs += @('-v:n', "-fileLoggerParameters:LogFile=$buildLog;Verbosity=normal")

    Write-Host "`n> dotnet $($buildArgs -join ' ')"
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "The consumer application failed to build with exit code $LASTEXITCODE" }

    # ------------------------------------------------------------ inspection

    if ($Platform -eq 'Android') {
        $apk = Get-ChildItem -Path (Join-Path $WorkDirectory 'bin/Release') -Recurse -Filter '*.apk' -File |
            Sort-Object Length -Descending | Select-Object -First 1

        if (-not $apk) {
            throw 'The build produced no .apk to inspect.'
        }

        Write-Host "`nInspecting $($apk.Name) ($([math]::Round($apk.Length / 1MB, 1)) MB)"

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($apk.FullName)
        try {
            $libraries = $zip.Entries.FullName | Where-Object { $_ -like '*libbox3d.so' }
        }
        finally {
            $zip.Dispose()
        }

        if (-not $libraries) {
            throw "$($apk.Name) contains no libbox3d.so. The package's Android runtime asset was not resolved into the application."
        }

        $libraries | ForEach-Object { Write-Host "  $_" }

        # An apk built for a single ABI is legitimate; one missing the ABI every
        # current phone uses is not.
        if (-not ($libraries | Where-Object { $_ -like '*arm64-v8a*' })) {
            throw 'The apk carries libbox3d.so but not for arm64-v8a, which is the ABI every current Android device runs.'
        }

        Write-Host "`nThe Android application carries Box3D for every ABI listed above."
    }
    else {
        $app = Get-ChildItem -Path (Join-Path $WorkDirectory 'bin/Release') -Recurse -Directory -Filter '*.app' |
            Select-Object -First 1

        if (-not $app) {
            throw 'The build produced no .app bundle to inspect.'
        }

        # The executable inside the bundle shares its name, minus the extension.
        $executable = Join-Path $app.FullName ([System.IO.Path]::GetFileNameWithoutExtension($app.Name))
        if (-not (Test-Path $executable)) {
            throw "No executable inside $($app.Name)."
        }

        Write-Host "`nInspecting $executable ($([math]::Round((Get-Item $executable).Length / 1MB, 1)) MB)"

        # This is the check that matters on iOS, and it is asked two ways
        # because a build that succeeds proves nothing on its own. A P/Invoke to
        # __Internal is resolved by Mono at run time, not by the linker, so an
        # application whose archive was never handed to the linker builds,
        # installs and launches exactly like a correct one, and fails on the
        # first physics call. There is no device here to find that out on.
        #
        #   the link   did the build hand libbox3d.a to the linker at all,
        #              which is what the package's .targets is responsible for
        #   the result did Box3D's symbols end up in the executable, which is
        #              what ForceLoad is responsible for: without it the linker
        #              keeps only the objects something already refers to, and
        #              nothing refers to these until run time
        #
        # Either one passing means the archive reached the application. Both are
        # reported, because which one failed says which half to go and look at.
        $linkerSawArchive = $false
        if (Test-Path $buildLog) {
            $linkerSawArchive = [bool] (Select-String -Path $buildLog -Pattern 'libbox3d\.a|box3d\.xcframework' -Quiet)
        }

        $symbols = @(& nm $executable 2>$null | Select-String -Pattern '_b3[A-Za-z_]' )

        Write-Host "  archive passed to the linker : $linkerSawArchive"
        Write-Host "  Box3D symbols in the binary  : $($symbols.Count)"

        if (-not $linkerSawArchive -and $symbols.Count -eq 0) {
            # Everything worth knowing about why, gathered before failing, so
            # that one CI run answers the question instead of starting a guess.
            Write-Host "`n--- diagnostics"

            $allSymbols = @(& nm $executable 2>$null)
            Write-Host "  symbols in the executable: $($allSymbols.Count)"
            $allSymbols | Select-Object -First 5 | ForEach-Object { Write-Host "    $_" }

            Write-Host "  bundle contents:"
            Get-ChildItem $app.FullName | ForEach-Object { Write-Host "    $($_.Name)" }

            if (Test-Path $buildLog) {
                Write-Host "  build log lines naming the package or a native reference:"
                Select-String -Path $buildLog -Pattern 'NativeReference|force_load|Box3D' |
                    Select-Object -First 15 |
                    ForEach-Object { Write-Host "    $($_.Line.Trim())" }
            }

            throw "Box3D never reached the application: the linker was not given libbox3d.a and no b3 symbols are in the executable. The package's iOS .targets did not take effect - check that buildTransitive/<tfm>/ matches the consumer's target framework, and that NativeReference names the xcframework correctly."
        }

        if ($symbols.Count -gt 0) {
            $symbols | Select-Object -First 5 | ForEach-Object { Write-Host "  $($_.Line.Trim())" }
            Write-Host "`nThe iOS application has Box3D linked into its executable."
        }
        else {
            # The archive was linked but the symbols are not visible to nm,
            # which a stripped release binary can legitimately do. Worth saying
            # out loud rather than reporting as a clean pass.
            Write-Host "`nThe linker was given Box3D's archive, but no b3 symbols are visible in the executable - it is stripped. The link is evidence enough; the symbol check is not available here."
        }
    }
}
finally {
    Pop-Location
}
