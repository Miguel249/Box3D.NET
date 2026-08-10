#!/usr/bin/env pwsh
# SPDX-License-Identifier: MIT
#
# Assembles the iOS slices staged by build-native.ps1 into the xcframework that
# the package ships.
#
# Usage:
#   pwsh tools/create-xcframework.ps1
#
# Input:
#   runtimes/ios-arm64/native/libbox3d.a
#   runtimes/iossimulator-arm64/native/libbox3d.a
#   runtimes/iossimulator-x64/native/libbox3d.a
#
# Output:
#   artifacts/apple/box3d.xcframework
#
# This is a separate script rather than a step inside build-native.ps1 because
# it cannot run until every slice exists, and each slice is built by its own
# invocation - on CI, potentially in its own job.

[CmdletBinding()]
param(
    # Build the framework from whichever slices are present instead of failing
    # on a missing one. Intended for trying out a single-architecture build
    # locally; a package built this way does not support every iOS device.
    [switch] $AllowPartial
)

$ErrorActionPreference = 'Stop'

$RepoRoot  = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$OutputDir = Join-Path $RepoRoot 'artifacts/apple'
$Output    = Join-Path $OutputDir 'box3d.xcframework'

# xcodebuild and lipo are Xcode's, and Xcode is macOS only. There is no fallback
# to write here: an xcframework is an Apple packaging format that only Apple's
# tooling produces correctly.
if (-not $IsMacOS) {
    throw 'Creating an xcframework requires macOS with Xcode installed.'
}

foreach ($tool in @('xcodebuild', 'lipo')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool was not found on PATH. Install Xcode and its command line tools."
    }
}

function Get-Slice([string] $Rid) {
    $path = Join-Path $RepoRoot "runtimes/$Rid/native/libbox3d.a"
    if (Test-Path $path) { return $path }

    if (-not $AllowPartial) {
        throw "No archive staged for $Rid at $path. Run: pwsh tools/build-native.ps1 -Rid $Rid"
    }

    Write-Warning "No archive staged for $Rid. The framework will not support it."
    return $null
}

$device       = Get-Slice 'ios-arm64'
$simulatorArm = Get-Slice 'iossimulator-arm64'
$simulatorX64 = Get-Slice 'iossimulator-x64'

if (-not $device -and -not $simulatorArm -and -not $simulatorX64) {
    throw 'No iOS archives are staged. Nothing to assemble.'
}

if (Test-Path $Output) { Remove-Item -Recurse -Force $Output }
New-Item -ItemType Directory -Force $OutputDir | Out-Null

# The two simulator archives have to become one before xcodebuild sees them.
#
# An xcframework is indexed by platform, not by architecture, and the simulator
# is a single platform: passing the arm64 and x86_64 simulator archives as two
# -library arguments is rejected as a duplicate rather than merged. lipo is what
# merges architectures; xcodebuild is what separates platforms.
$simulator = $null
$simulatorSlices = @($simulatorArm, $simulatorX64) | Where-Object { $_ }

# The merged archive is written into a directory of its own and keeps the name
# libbox3d.a. xcodebuild copies each -library argument into the framework under
# the file name it arrived with, so calling this one libbox3d-simulator.a
# produced a framework whose two variants held differently named archives -
# valid, but inconsistent enough that anything matching on the file name sees
# one variant and not the other.
$merged = Join-Path $OutputDir 'merged-simulator'

if ($simulatorSlices.Count -gt 1) {
    New-Item -ItemType Directory -Force $merged | Out-Null
    $simulator = Join-Path $merged 'libbox3d.a'

    Write-Host "> lipo -create $($simulatorSlices -join ' ') -output $simulator"
    & lipo -create @simulatorSlices -output $simulator
    if ($LASTEXITCODE -ne 0) { throw "lipo failed with exit code $LASTEXITCODE" }
}
elseif ($simulatorSlices.Count -eq 1) {
    $simulator = $simulatorSlices[0]
}

$xcodeArgs = @('-create-xcframework')
if ($device)    { $xcodeArgs += @('-library', $device) }
if ($simulator) { $xcodeArgs += @('-library', $simulator) }
$xcodeArgs += @('-output', $Output)

Write-Host "`n> xcodebuild $($xcodeArgs -join ' ')"
& xcodebuild @xcodeArgs
if ($LASTEXITCODE -ne 0) { throw "xcodebuild failed with exit code $LASTEXITCODE" }

# The merged archive is an intermediate. Left in place it would be packed into
# the NuGet package along with the framework directory beside it, doubling the
# simulator payload for no reason.
if (Test-Path $merged) {
    Remove-Item -Recurse -Force $merged
}

# Report what the framework actually covers rather than what was asked for,
# which is the part that matters when -AllowPartial was used.
Write-Host "`nCreated $Output"
Get-ChildItem -Path $Output -Directory | ForEach-Object {
    $archive = Join-Path $_.FullName 'libbox3d.a'
    $architectures = if (Test-Path $archive) { (& lipo -archs $archive) } else { 'no archive' }
    Write-Host "  $($_.Name): $architectures"
}
