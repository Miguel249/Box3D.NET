#!/usr/bin/env pwsh
# SPDX-License-Identifier: MIT
#
# Builds the Box3D shared library and stages it where the .NET build expects it.
#
# Box3D is consumed unmodified from the external/box3d submodule. This script
# never touches those sources; it only configures and builds them.
#
# Usage:
#   pwsh tools/build-native.ps1                       # build for this machine
#   pwsh tools/build-native.ps1 -Rid linux-arm64      # name the output explicitly
#   pwsh tools/build-native.ps1 -MacUniversal         # one binary for both Macs
#   pwsh tools/build-native.ps1 -Rid android-arm64    # cross-compile with the NDK
#   pwsh tools/build-native.ps1 -Rid ios-arm64        # static slice for iOS
#
# Output:
#   runtimes/<rid>/native/box3d.dll      (Windows)
#   runtimes/<rid>/native/libbox3d.so    (Linux, Android)
#   runtimes/<rid>/native/libbox3d.dylib (macOS)
#   runtimes/<rid>/native/libbox3d.a     (iOS and the iOS simulator)
#
# That layout is the one NuGet uses to pick the right binary at run time, and
# the one the test and sample projects copy from during a local build. The iOS
# slices are the exception: a static archive cannot be loaded at run time, so
# they are inputs to tools/create-xcframework.ps1 rather than package assets.
# See the comment above the iOS section for why iOS is static at all.

[CmdletBinding()]
param(
    # The .NET runtime identifier naming the output folder, and, for the targets
    # that cannot be built for the host, the platform being cross-compiled for.
    # Inferred from the host when omitted.
    [string] $Rid,

    # Build configuration for the native library.
    [ValidateSet('Release', 'Debug', 'RelWithDebInfo')]
    [string] $Configuration = 'Release',

    # Build a macOS binary containing both x86_64 and arm64 slices.
    [switch] $MacUniversal,

    # The CMake generator to use. Left empty, CMake picks its default, which is
    # Visual Studio on a machine that has it. Set this to build with another
    # toolchain, for example: -Generator Ninja with gcc or clang on PATH.
    #
    # Android ignores CMake's default and always uses Ninja; see below.
    [string] $Generator,

    # The Android NDK to cross-compile with. Discovered from the usual
    # environment variables and install locations when omitted.
    [string] $AndroidNdk,

    # Remove the CMake build tree before configuring.
    [switch] $Clean
)

$ErrorActionPreference = 'Stop'

$RepoRoot  = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$SourceDir = Join-Path $RepoRoot 'external/box3d'

if (-not (Test-Path (Join-Path $SourceDir 'CMakeLists.txt'))) {
    throw "Box3D sources not found at $SourceDir. Run: git submodule update --init --recursive"
}

# ------------------------------------------------------------- platform facts

# The host only decides which targets are reachable. Everything else is driven
# by the RID, because the platform being built for and the platform doing the
# building stopped being the same thing once Android and iOS were added.
if ($IsWindows -or $env:OS -eq 'Windows_NT') { $hostPlatform = 'windows' }
elseif ($IsMacOS)                            { $hostPlatform = 'macos' }
else                                         { $hostPlatform = 'linux' }

if (-not $Rid) {
    $arch = switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
        'X64'   { 'x64' }
        'Arm64' { 'arm64' }
        'X86'   { 'x86' }
        default { throw "Unsupported process architecture: $_" }
    }
    $Rid = switch ($hostPlatform) {
        'windows' { "win-$arch" }
        'macos'   { "osx-$arch" }
        'linux'   { "linux-$arch" }
    }
}

$platform = switch -Wildcard ($Rid) {
    'win-*'          { 'windows' }
    'osx-*'          { 'macos' }
    'linux-*'        { 'linux' }
    'android-*'      { 'android' }
    'ios-*'          { 'ios' }
    'iossimulator-*' { 'ios' }
    default          { throw "Unrecognised runtime identifier: $Rid" }
}

# The canonical file name is the one the package ships and the one .NET resolves
# first. The search patterns are wider than that because the name depends on the
# toolchain: MSVC emits box3d.dll while MinGW emits libbox3d.dll for the same
# target. Whatever is produced is staged under the canonical name.
switch ($platform) {
    'windows' { $libraryName = 'box3d.dll';     $searchPatterns = @('box3d.dll', 'libbox3d.dll') }
    'macos'   { $libraryName = 'libbox3d.dylib'; $searchPatterns = @('libbox3d.dylib', 'box3d.dylib') }
    'linux'   { $libraryName = 'libbox3d.so';   $searchPatterns = @('libbox3d.so', 'box3d.so') }
    'android' { $libraryName = 'libbox3d.so';   $searchPatterns = @('libbox3d.so', 'box3d.so') }
    'ios'     { $libraryName = 'libbox3d.a';    $searchPatterns = @('libbox3d.a', 'box3d.a') }
}

# Cross-compiling is only possible where the toolchain exists. Apple's is
# available on macOS alone, which is why the iOS slices are built on CI rather
# than wherever the release happens to be cut.
if ($platform -eq 'ios' -and $hostPlatform -ne 'macos') {
    throw "Building for $Rid requires macOS with Xcode installed; this is $hostPlatform."
}
if ($platform -eq 'macos' -and $hostPlatform -ne 'macos') {
    throw "Building for $Rid requires macOS; this is $hostPlatform."
}

# Each target gets its own build tree. A single shared one appears to work until
# the second target is built into it: CMake caches the compiler, the sysroot and
# the toolchain file on the first configure, and reconfiguring with a different
# toolchain over that cache either fails outright or, worse, silently produces a
# binary for the previous target under the new RID's name.
$BuildDir = Join-Path $RepoRoot "artifacts/native-build/$Rid"

# ------------------------------------------------------------------ toolchain

# Android ships a complete CMake and Ninja inside the SDK, so a machine set up
# for Android development can build this without a separate CMake install. The
# one on PATH still wins where there is one.
function Find-AndroidSdk {
    foreach ($candidate in @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT)) {
        if ($candidate -and (Test-Path $candidate)) { return $candidate }
    }

    $defaults = switch ($hostPlatform) {
        'windows' { @("$env:LOCALAPPDATA/Android/Sdk") }
        'macos'   { @("$HOME/Library/Android/sdk") }
        'linux'   { @("$HOME/Android/Sdk", "$HOME/android-sdk") }
    }

    foreach ($candidate in $defaults) {
        if ($candidate -and (Test-Path $candidate)) { return $candidate }
    }

    return $null
}

# Picks the highest version from a directory of side-by-side version folders,
# which is how both the NDK and the SDK's CMake are laid out. Sorting these as
# strings puts 3.9 above 3.22, so they are compared as versions.
function Get-NewestVersionedChild([string] $Root) {
    if (-not $Root -or -not (Test-Path $Root)) { return $null }

    return Get-ChildItem -Path $Root -Directory |
        Sort-Object { try { [version] $_.Name } catch { [version] '0.0' } } -Descending |
        Select-Object -First 1 |
        ForEach-Object { $_.FullName }
}

$androidSdk = $null
if ($platform -eq 'android') {
    if (-not $AndroidNdk) {
        # ANDROID_NDK_LATEST_HOME is what the GitHub-hosted runners set; the
        # other two are what a local install of the NDK sets.
        foreach ($candidate in @($env:ANDROID_NDK_HOME, $env:ANDROID_NDK_ROOT, $env:ANDROID_NDK_LATEST_HOME)) {
            if ($candidate -and (Test-Path $candidate)) { $AndroidNdk = $candidate; break }
        }
    }

    $androidSdk = Find-AndroidSdk

    if (-not $AndroidNdk -and $androidSdk) {
        $AndroidNdk = Get-NewestVersionedChild (Join-Path $androidSdk 'ndk')
    }

    if (-not $AndroidNdk) {
        throw 'The Android NDK was not found. Set ANDROID_NDK_HOME, or pass -AndroidNdk, or install the NDK through the Android SDK manager.'
    }

    $androidToolchain = Join-Path $AndroidNdk 'build/cmake/android.toolchain.cmake'
    if (-not (Test-Path $androidToolchain)) {
        throw "No CMake toolchain file at $androidToolchain. That path does not look like an Android NDK."
    }
}

# Resolved the long way round rather than with ?. because Windows PowerShell 5.1
# has no null-conditional operator, and this script is expected to run there.
$cmakeCommand = Get-Command cmake -ErrorAction SilentlyContinue
$ninjaCommand = Get-Command ninja -ErrorAction SilentlyContinue
$cmakeExe = if ($cmakeCommand) { $cmakeCommand.Source } else { $null }
$ninjaExe = if ($ninjaCommand) { $ninjaCommand.Source } else { $null }

if ($platform -eq 'android' -and $androidSdk) {
    $sdkCMake = Get-NewestVersionedChild (Join-Path $androidSdk 'cmake')
    if ($sdkCMake) {
        $suffix = if ($hostPlatform -eq 'windows') { '.exe' } else { '' }
        if (-not $cmakeExe) {
            $bundled = Join-Path $sdkCMake "bin/cmake$suffix"
            if (Test-Path $bundled) { $cmakeExe = $bundled }
        }
        if (-not $ninjaExe) {
            $bundled = Join-Path $sdkCMake "bin/ninja$suffix"
            if (Test-Path $bundled) { $ninjaExe = $bundled }
        }
    }
}

if (-not $cmakeExe) {
    throw 'CMake was not found on PATH. Install CMake 3.22 or later: https://cmake.org/download/'
}

$OutputDir = Join-Path $RepoRoot "runtimes/$Rid/native"

Write-Host "Box3D native build"
Write-Host "  source        : $SourceDir"
Write-Host "  configuration : $Configuration"
Write-Host "  runtime id    : $Rid"
Write-Host "  host          : $hostPlatform"
Write-Host "  cmake         : $cmakeExe"
if ($platform -eq 'android') { Write-Host "  android ndk   : $AndroidNdk" }
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
#
# Box3D picks its SIMD path from the compiler's own macros rather than from
# anything set here, so each target gets the right one without help: NEON on
# arm64, SSE2 on x64, and the scalar path on armv7, where upstream disables NEON
# because it has no divide or square root.
$cmakeArgs = @(
    '-S', $SourceDir
    '-B', $BuildDir
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

# iOS is the one target that is linked statically rather than shipped as a
# loadable library. Apple requires every dynamic library inside an application
# to be a signed framework in the bundle, and the .NET iOS build links native
# dependencies into the executable instead. That is also why the binding names
# __Internal rather than box3d on this platform: by the time the P/Invoke runs,
# the symbols are already in the main image.
if ($platform -eq 'ios') {
    $cmakeArgs += '-DBUILD_SHARED_LIBS=OFF'
}
else {
    $cmakeArgs += '-DBUILD_SHARED_LIBS=ON'
}

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

if ($platform -eq 'ios') {
    # Device and simulator are different sysroots, not different architectures,
    # and a slice built against the wrong one is rejected by the linker rather
    # than at run time. The RID carries which is which.
    $sysroot = if ($Rid -like 'iossimulator-*') { 'iphonesimulator' } else { 'iphoneos' }
    $arch    = if ($Rid -like '*-x64') { 'x86_64' } else { 'arm64' }

    $cmakeArgs += @(
        '-DCMAKE_SYSTEM_NAME=iOS'
        "-DCMAKE_OSX_SYSROOT=$sysroot"
        "-DCMAKE_OSX_ARCHITECTURES=$arch"
        # Matches the minimum that .NET 8's iOS workload targets. A lower value
        # would produce a library the application cannot deploy against.
        '-DCMAKE_OSX_DEPLOYMENT_TARGET=12.2'
    )

    # CMake's default generator on macOS is Makefiles, which does build this,
    # but Xcode is the generator Apple's toolchain is tested against and the one
    # that gets the simulator sysroot and bitcode-era defaults right.
    if (-not $Generator) { $Generator = 'Xcode' }
}

if ($platform -eq 'android') {
    $abi = switch ($Rid) {
        'android-arm64' { 'arm64-v8a' }
        'android-x64'   { 'x86_64' }
        'android-arm'   { 'armeabi-v7a' }
        'android-x86'   { 'x86' }
        default         { throw "No Android ABI is mapped to the runtime identifier $Rid." }
    }

    $cmakeArgs += @(
        "-DCMAKE_TOOLCHAIN_FILE=$androidToolchain"
        "-DANDROID_ABI=$abi"
        # API 21 is the floor .NET for Android supports, so building lower would
        # buy nothing the managed side could use.
        '-DANDROID_PLATFORM=android-21'
    )

    # The NDK toolchain only supports single-configuration generators, so
    # CMake's default is wrong here on every host that has Visual Studio.
    if (-not $Generator) {
        if (-not $ninjaExe) {
            throw 'Ninja was not found. It is required to build for Android; it ships inside the Android SDK''s CMake package, or install it separately.'
        }
        $Generator = 'Ninja'
        $cmakeArgs += "-DCMAKE_MAKE_PROGRAM=$ninjaExe"
    }
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
& $cmakeExe @cmakeArgs
if ($LASTEXITCODE -ne 0) { throw "CMake configure failed with exit code $LASTEXITCODE" }

# ---------------------------------------------------------------------- build

$buildArgs = @('--build', $BuildDir, '--config', $Configuration, '--target', 'box3d')

# Use every core, but let CMake decide how when the generator disagrees.
$buildArgs += @('--parallel')

Write-Host "`n> cmake $($buildArgs -join ' ')"
& $cmakeExe @buildArgs
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

# An unstripped Release build of Box3D is around 6 MB per ABI, nearly all of it
# symbol and debug data that nothing on the device reads. That cost lands in
# every installed application, once per ABI shipped, so it is removed here.
#
# Only Android. The iOS output is a static archive whose symbols the linker
# still needs, and the desktop packages keep theirs so that a crash in the
# native library has a usable stack.
if ($platform -eq 'android') {
    $stripTool = Get-ChildItem -Path (Join-Path $AndroidNdk 'toolchains/llvm/prebuilt') -Recurse -Filter 'llvm-strip*' -File -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($stripTool) {
        & $stripTool.FullName '--strip-unneeded' (Join-Path $OutputDir $libraryName)
        if ($LASTEXITCODE -ne 0) { throw "llvm-strip failed with exit code $LASTEXITCODE" }
    }
    else {
        Write-Warning "llvm-strip was not found under $AndroidNdk. Staging the unstripped library, which is several times larger than it needs to be."
    }
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

if ($platform -eq 'ios') {
    Write-Host "This is a static slice, not a package asset. Run tools/create-xcframework.ps1 once every iOS slice has been built."
}
