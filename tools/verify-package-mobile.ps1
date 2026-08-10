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

    # Something in the application has to call into Box3D, and the call has to be
    # reachable from code the application actually keeps.
    #
    # Not for the sake of running it - nothing here runs - but because an
    # unreachable call is trimmed away, and on iOS that silently undoes the
    # linking this whole check exists to prove. Every link in that chain
    # succeeds, which is why it is worth spelling out:
    #
    #   the trimmer drops Box3DUse, since nothing the application reaches refers
    #   to it, and the P/Invokes go with it
    #
    #   mtouch builds the list of symbols to keep from the __Internal P/Invokes
    #   it finds in what survives - it finds none, so no b3 symbol is listed
    #
    #   the native link runs with -force_load, which pulls every object of
    #   libbox3d.a in, and then -dead_strip removes all of them again: nothing
    #   refers to them by name, because a P/Invoke to __Internal is resolved at
    #   run time, and the exported symbol list does not ask for them either
    #
    # The result builds, installs and launches, and dies on the first physics
    # call. A module initializer is what anchors it: ILLink keeps the module's
    # own initializer for the assembly being built, so the call below is rooted
    # without this script having to find and edit the template's entry point,
    # which is a shape that changes with the workload.
    @'
// SPDX-License-Identifier: MIT
using System.Numerics;
using System.Runtime.CompilerServices;
using Box3D;

internal static class Box3DUse
{
    internal static float Result;

    [ModuleInitializer]
    internal static void Run()
    {
        Result = Fall();
    }

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

    # A second anchor for the same call, from the template's entry point.
    #
    # The module initializer above is what the check relies on. This adds the
    # call an application would really contain, on the line before the template
    # hands control to UIKit, and it is deliberately best effort: the file that
    # calls UIApplication.Main is the workload's to write, and it has already
    # changed spelling once. When it cannot be found, what is reported is that
    # this anchor is missing rather than a failure - the symbol check at the end
    # is the judge either way, and it cannot be fooled by a missing anchor.
    if ($Platform -eq 'iOS') {
        # obj and bin are excluded because both fill with generated sources
        # during the restore that has already run, and one of those is a far
        # worse place to edit than the template's own file.
        $sources = @(Get-ChildItem -Path $WorkDirectory -Filter '*.cs' -File -Recurse |
            Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' })

        $entryPoint = $sources |
            Where-Object { (Get-Content -Raw $_.FullName) -match 'UIApplication\.Main\s*\(' } |
            Select-Object -First 1

        $patched = $null
        if ($entryPoint) {
            # Inserted ahead of the call rather than written over the file, so
            # the template keeps its own namespace and its own AppDelegate.
            $source = Get-Content -Raw $entryPoint.FullName
            $patched = ([regex] '(?m)^([ \t]*)(UIApplication\.Main\s*\()').Replace($source, "`$1global::Box3DUse.Fall();`r`n`r`n`$1`$2", 1)

            if ($patched -ne $source) {
                Set-Content -Path $entryPoint.FullName -Value $patched -Encoding utf8
                Write-Host "`nCalled Box3D from $($entryPoint.Name) as well as from the module initializer."
            }
        }

        if (-not $entryPoint -or $patched -eq $source) {
            Write-Host "`nNo entry point to call Box3D from - the module initializer is the only anchor."
            Write-Host '  sources the template produced:'
            $sources | ForEach-Object { Write-Host "    $($_.FullName.Substring($WorkDirectory.Length + 1))" }
        }
    }

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

        $allSymbols = @(& nm $executable 2>$null)
        $symbols = @($allSymbols | Select-String -Pattern '_b3[A-Za-z_]')

        # Undefined entries are what the binary imports from the system, and a
        # fully stripped executable still lists those. Only defined symbols say
        # anything about what is inside it, so they are counted separately: none
        # at all means the check cannot see anything and silence proves nothing,
        # while plenty of them and no b3 among them means Box3D is genuinely
        # absent.
        $definedSymbols = @($allSymbols | Where-Object { $_ -notmatch '^\s*U ' -and $_ -match '^[0-9a-fA-F]+\s' })

        Write-Host "  archive passed to the linker : $linkerSawArchive"
        Write-Host "  defined symbols in binary    : $($definedSymbols.Count)"
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
        elseif ($definedSymbols.Count -gt 0) {
            # The binary kept its symbol table and Box3D is not in it. That is
            # not a stripped build hiding the evidence, it is the evidence: the
            # archive was handed to the linker and dropped again, and every
            # physics call would fail on the device with an entry point that is
            # not there.
            #
            # Which half to go and fix is decided by mtouch-symbols.list, the
            # file passed to the native link as -exported_symbols_list. mtouch
            # fills it from the __Internal P/Invokes left in the application
            # after trimming, and -dead_strip keeps only what it names.
            Write-Host "`n--- diagnostics"

            $requested = @()
            $symbolList = Get-ChildItem -Path (Join-Path $WorkDirectory 'obj') -Recurse -Filter 'mtouch-symbols.list' -File |
                Select-Object -First 1

            if ($symbolList) {
                $listed = @(Get-Content $symbolList.FullName)
                $requested = @($listed | Select-String -Pattern '_b3[A-Za-z_]')
                Write-Host "  mtouch-symbols.list          : $($requested.Count) Box3D of $($listed.Count) symbols asked for"
                $requested | Select-Object -First 5 | ForEach-Object { Write-Host "    $($_.Line.Trim())" }
            }
            else {
                Write-Host "  mtouch-symbols.list          : not found under obj/"
            }

            if (Test-Path $buildLog) {
                Write-Host "  native link invocations naming the archive:"
                Select-String -Path $buildLog -Pattern 'force_load' |
                    Select-Object -First 3 |
                    ForEach-Object { Write-Host "    $(($_.Line.Trim() -split '\s+' | Select-Object -First 6) -join ' ') ..." }
            }

            if ($requested.Count -eq 0) {
                throw "The executable defines $($definedSymbols.Count) symbols and not one of them is Box3D's. mtouch asked the linker to keep no Box3D symbol at all, so -dead_strip removed everything -force_load had just pulled in: the application's own code no longer P/Invokes into Box3D by the time mtouch looks. Something reachable from the entry point has to call it - check that the call this script inserts into Main is still there and still survives trimming."
            }

            throw "The executable defines $($definedSymbols.Count) symbols and not one of them is Box3D's, even though mtouch asked the linker to keep $($requested.Count). The archive reached the linker and was then dropped - check that ForceLoad is still set in the package's .targets and that the xcframework slice matches the architecture being built."
        }
        else {
            # Nothing is visible either way. Release builds for iOS are
            # stripped, so this is expected rather than suspicious, but it does
            # mean the stronger half of the check did not run and saying "pass"
            # without saying that would be claiming more than was tested.
            Write-Host "`nThe linker was given Box3D's archive. The executable is stripped - it defines no symbols at all - so the symbol check could not run; the link is the evidence here."
        }
    }
}
finally {
    Pop-Location
}
