#!/usr/bin/env pwsh
# SPDX-License-Identifier: MIT
#
# Builds the Box3D shared library and stages it where the .NET build expects it.
#
# Box3D is consumed unmodified from the external/box3d submodule. This script
# never touches those sources; it only configures and builds them.
#
# Usage:
#   pwsh tools/build-native.ps1                     # build for this machine
#   pwsh tools/build-native.ps1 -Rid linux-arm64    # name the output explicitly
#   pwsh tools/build-native.ps1 -MacUniversal       # one binary for both Macs
#
# Output:
#   runtimes/<rid>/native/box3d.dll     (Windows)
#   runtimes/<rid>/native/libbox3d.so   (Linux)
#   runtimes/<rid>/native/libbox3d.dylib (macOS)
#
# That layout is the one NuGet uses to pick the right binary at run time, and
# the one the test and sample projects copy from during a local build.

[CmdletBinding()]
param(
    # The .NET runtime identifier naming the output folder. Inferred when omitted.
    [string] $Rid,

    # Build configuration for the native library.
    [ValidateSet('Release', 'Debug', 'RelWithDebInfo')]
    [string] $Configuration = 'Release',

    # Build a macOS binary containing both x86_64 and arm64 slices.
    [switch] $MacUniversal,

    # The CMake generator to use. Left empty, CMake picks its default, which is
    # Visual Studio on a machine that has it. Set this to build with another
    # toolchain, for example: -Generator Ninja with gcc or clang on PATH.
    [string] $Generator,

    # Remove the CMake build tree before configuring.
    [switch] $Clean
)

$ErrorActionPreference = 'Stop'

$RepoRoot  = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$SourceDir = Join-Path $RepoRoot 'external/box3d'
$BuildDir  = Join-Path $RepoRoot 'artifacts/native-build'

if (-not (Test-Path (Join-Path $SourceDir 'CMakeLists.txt'))) {
    throw "Box3D sources not found at $SourceDir. Run: git submodule update --init --recursive"
}

if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
    throw 'CMake was not found on PATH. Install CMake 3.22 or later: https://cmake.org/download/'
}

# ------------------------------------------------------------- platform facts

# The canonical file name is the one the package ships and the one .NET resolves
# first. The search patterns are wider than that because the name depends on the
# toolchain: MSVC emits box3d.dll while MinGW emits libbox3d.dll for the same
# target. Whatever is produced is staged under the canonical name.
if ($IsWindows -or $env:OS -eq 'Windows_NT') {
    $platform = 'windows'
    $libraryName = 'box3d.dll'
    $searchPatterns = @('box3d.dll', 'libbox3d.dll')
}
elseif ($IsMacOS) {
    $platform = 'macos'
    $libraryName = 'libbox3d.dylib'
    $searchPatterns = @('libbox3d.dylib', 'box3d.dylib')
}
else {
    $platform = 'linux'
    $libraryName = 'libbox3d.so'
    $searchPatterns = @('libbox3d.so', 'box3d.so')
}

if (-not $Rid) {
    $arch = switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
        'X64'   { 'x64' }
        'Arm64' { 'arm64' }
        'X86'   { 'x86' }
        default { throw "Unsupported process architecture: $_" }
    }
    $Rid = switch ($platform) {
        'windows' { "win-$arch" }
        'macos'   { "osx-$arch" }
        'linux'   { "linux-$arch" }
    }
}

$OutputDir = Join-Path $RepoRoot "runtimes/$Rid/native"

Write-Host "Box3D native build"
Write-Host "  source        : $SourceDir"
Write-Host "  configuration : $Configuration"
Write-Host "  runtime id    : $Rid"
Write-Host "  output        : $OutputDir"

if ($Clean -and (Test-Path $BuildDir)) {
    Remove-Item -Recurse -Force $BuildDir
}

# ------------------------------------------------------------------ configure

# Everything but the library itself is turned off: the samples pull in a
# renderer and a window system, and the unit tests and benchmarks are Box3D's
# own, none of which belong in a binding package.
#
# BOX3D_VALIDATE defaults to ON upstream and adds heavy internal checking. It is
# left off here so that a Release package is not paying for assertions.
$cmakeArgs = @(
    '-S', $SourceDir
    '-B', $BuildDir
    '-DBUILD_SHARED_LIBS=ON'
    '-DCMAKE_BUILD_TYPE=' + $Configuration
    '-DCMAKE_POSITION_INDEPENDENT_CODE=ON'
    '-DBOX3D_SAMPLES=OFF'
    '-DBOX3D_UNIT_TESTS=OFF'
    '-DBOX3D_BENCHMARKS=OFF'
    '-DBOX3D_DOCS=OFF'
    '-DBOX3D_VALIDATE=OFF'
    # This binding targets the single-precision ABI. Turning this on would
    # change b3Pos to double and invalidate every struct layout in the binding.
    '-DBOX3D_DOUBLE_PRECISION=OFF'
)

if ($platform -eq 'macos') {
    if ($MacUniversal) {
        $cmakeArgs += '-DCMAKE_OSX_ARCHITECTURES=x86_64;arm64'
    }
    elseif ($Rid -eq 'osx-x64') {
        $cmakeArgs += '-DCMAKE_OSX_ARCHITECTURES=x86_64'
    }
    elseif ($Rid -eq 'osx-arm64') {
        $cmakeArgs += '-DCMAKE_OSX_ARCHITECTURES=arm64'
    }

    # The oldest macOS the resulting binary will load on.
    $cmakeArgs += '-DCMAKE_OSX_DEPLOYMENT_TARGET=11.0'
}

if ($Generator) {
    $cmakeArgs += @('-G', $Generator)
}

# The architecture flag is a Visual Studio generator feature. Passing it to any
# other generator is a hard error, so it is only added when Visual Studio is
# actually in play: either explicitly requested, or left to CMake's default on
# Windows, which is Visual Studio wherever it is installed.
$usingVisualStudio = $platform -eq 'windows' -and (-not $Generator -or $Generator -like 'Visual Studio*')

if ($usingVisualStudio) {
    # Single-config generators ignore CMAKE_BUILD_TYPE; Visual Studio takes the
    # configuration at build time instead, which is passed below.
    if ($Rid -eq 'win-arm64') {
        $cmakeArgs += @('-A', 'ARM64')
    }
    elseif ($Rid -eq 'win-x64') {
        $cmakeArgs += @('-A', 'x64')
    }
}
elseif ($platform -eq 'windows') {
    # Building on Windows with GCC or Clang instead of MSVC. Those link their own
    # runtime support library dynamically by default, which leaves the DLL
    # depending on libgcc_s_seh-1.dll and libwinpthread-1.dll sitting in the
    # toolchain directory. That dependency is invisible until the library fails
    # to load on a machine without the toolchain, which is every machine that
    # installs the package. Link them in statically so the result stands alone.
    $cmakeArgs += '-DCMAKE_SHARED_LINKER_FLAGS=-static-libgcc -static -Wl,--exclude-libs,ALL'
}

Write-Host "`n> cmake $($cmakeArgs -join ' ')"
& cmake @cmakeArgs
if ($LASTEXITCODE -ne 0) { throw "CMake configure failed with exit code $LASTEXITCODE" }

# ---------------------------------------------------------------------- build

$buildArgs = @('--build', $BuildDir, '--config', $Configuration, '--target', 'box3d')

# Use every core, but let CMake decide how when the generator disagrees.
$buildArgs += @('--parallel')

Write-Host "`n> cmake $($buildArgs -join ' ')"
& cmake @buildArgs
if ($LASTEXITCODE -ne 0) { throw "CMake build failed with exit code $LASTEXITCODE" }

# ---------------------------------------------------------------------- stage

$built = $null
foreach ($pattern in $searchPatterns) {
    $built = Get-ChildItem -Path $BuildDir -Recurse -Filter $pattern -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($built) { break }
}

if (-not $built) {
    throw "Build succeeded but none of $($searchPatterns -join ', ') was found under $BuildDir"
}

New-Item -ItemType Directory -Force $OutputDir | Out-Null
Copy-Item $built.FullName (Join-Path $OutputDir $libraryName) -Force

# Windows ships the import library and debug symbols alongside the DLL.
if ($platform -eq 'windows') {
    $pdb = Get-ChildItem -Path $BuildDir -Recurse -Filter 'box3d.pdb' -File |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($pdb) { Copy-Item $pdb.FullName (Join-Path $OutputDir 'box3d.pdb') -Force }
}

# Measure what was staged, not what was found.
#
# On macOS CMake writes libbox3d.<version>.dylib and leaves libbox3d.dylib as a
# symlink to it, so $built above is the link and its Length is zero. Copy-Item
# follows the link and stages the real contents, but the log read
# "Staged libbox3d.dylib (0 KB)", which looks exactly like a staging failure and
# was investigated as one.
$staged = Get-Item (Join-Path $OutputDir $libraryName)
$size = [math]::Round($staged.Length / 1KB, 1)

if ($staged.Length -eq 0) {
    throw "Staged $libraryName is empty. The build produced a file but nothing was copied into $OutputDir."
}

Write-Host "`nStaged $libraryName ($size KB) to $OutputDir"
