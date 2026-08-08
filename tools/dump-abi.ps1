#!/usr/bin/env pwsh
# SPDX-License-Identifier: MIT
#
# Records the ABI of every Box3D struct, as the C compiler sees it.
#
# The managed structs in Box3D.NET.Native are hand-written mirrors of the C
# declarations. Nothing about C# forces the two to agree: a wrong field type or
# a field in the wrong order still compiles, still runs, and silently reads the
# wrong bytes, so a body ends up with its restitution in the friction slot. That
# is the worst class of bug a binding can have, because there is no crash to
# investigate.
#
# LayoutTests once pinned a handful of sizes to constants derived by hand from
# the headers. That covers only the types someone remembered, and the constants
# are themselves a second copy of the thing being checked. This script instead
# asks the C compiler directly: it parses the struct declarations out of the
# Box3D headers, emits a C program that prints sizeof, alignof and offsetof for
# every field, compiles it against those same headers, and writes the answers to
# abi/native-layout.json.
#
# AbiTests then holds the managed structs to that file, and CI regenerates it to
# catch a submodule bump that moves a field.
#
# Usage:
#   pwsh tools/dump-abi.ps1              # write abi/native-layout.json
#   pwsh tools/dump-abi.ps1 -Check       # fail if the file is out of date
#
# Requires a C compiler: cl on Windows, or cc/gcc/clang elsewhere.

[CmdletBinding()]
param(
    # Verify the committed file matches this machine rather than rewriting it.
    [switch] $Check,

    # Where to write the result. Defaults to abi/native-layout.json.
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'

$RepoRoot   = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$IncludeDir = Join-Path $RepoRoot 'external/box3d/include'
$BuildDir   = Join-Path $RepoRoot 'artifacts/abi'

if (-not $OutputPath) {
    $OutputPath = Join-Path $RepoRoot 'abi/native-layout.json'
}

if (-not (Test-Path (Join-Path $IncludeDir 'box3d/box3d.h'))) {
    throw "Box3D headers not found at $IncludeDir. Run: git submodule update --init --recursive"
}

# --------------------------------------------------------------- parse headers

# Only these headers declare public structs. base.h and constants.h hold macros
# and the allocator hooks, neither of which carries a layout worth pinning.
$headers = @('types.h', 'collision.h', 'box3d.h') |
    ForEach-Object { Join-Path $IncludeDir "box3d/$_" } |
    Where-Object { Test-Path $_ }

# Fields whose declaration this script cannot read are a hard error rather than
# a silent omission: an unparsed field is an unchecked field, and the whole
# point here is that nothing goes unchecked.
$unparsed = [System.Collections.Generic.List[string]]::new()

function Get-FieldName {
    param([string] $Declaration)

    $d = $Declaration.Trim()

    # void ( *DrawShapeFcn )( ... );  ->  DrawShapeFcn
    if ($d -match '\(\s*\*\s*(\w+)\s*\)') {
        return $Matches[1]
    }

    # b3Vec3 points[8];  ->  points
    if ($d -match '(\w+)\s*\[[^\]]*\]\s*$') {
        return $Matches[1]
    }

    # b3Vec3 position;  ->  position
    if ($d -match '(\w+)\s*$') {
        return $Matches[1]
    }

    return $null
}

$structs = [ordered] @{}

foreach ($header in $headers) {
    $text = Get-Content $header -Raw

    # Strip comments first so that a '{' or ';' inside one cannot confuse the
    # brace matching below.
    $text = [regex]::Replace($text, '/\*.*?\*/', '', 'Singleline')
    $text = [regex]::Replace($text, '//[^\r\n]*', '')

    # Resolve the one preprocessor branch that changes which structs exist.
    #
    # Box3D declares b3WorldCastOutput twice: a distinct struct under
    # BOX3D_DOUBLE_PRECISION, and a plain alias for b3CastOutput otherwise.
    # This binding is single precision only, and the native library is built
    # that way, so the alias is the truth here. Reading the header without
    # honouring this produced a phantom struct with no managed mirror, and the
    # mirror was right to be absent.
    $text = [regex]::Replace(
        $text,
        '#if\s+defined\s*\(\s*BOX3D_DOUBLE_PRECISION\s*\).*?#else(?<single>.*?)#endif',
        { param($match) $match.Groups['single'].Value },
        'Singleline')

    # A double-precision block with no #else arm removes declarations outright.
    $text = [regex]::Replace(
        $text,
        '#if\s+defined\s*\(\s*BOX3D_DOUBLE_PRECISION\s*\).*?#endif',
        '',
        'Singleline')

    # Any other conditional carrying a struct declaration is a construct this
    # script has not been taught, and guessing which arm is live is exactly how
    # a phantom struct gets recorded. Report it instead.
    foreach ($conditional in [regex]::Matches($text, '#if[^\r\n]*(?<body>.*?)#endif', 'Singleline')) {
        if ($conditional.Groups['body'].Value -match 'typedef\s+struct\s+\w+\s*\{') {
            $unparsed.Add("conditional block declares a struct: '$(($conditional.Value -split "`n")[0].Trim())'")
        }
    }

    # typedef struct b3Foo { ... } b3Foo;
    # The lazy body match stops at the first '}' , which is correct here because
    # Box3D declares no nested braces inside a public struct body. If that ever
    # changes, the field parse below reports it rather than guessing.
    foreach ($m in [regex]::Matches($text, 'typedef\s+struct\s+(\w+)\s*\{(.*?)\}\s*(\w+)\s*;', 'Singleline')) {
        $name = $m.Groups[3].Value
        if (-not $name.StartsWith('b3')) { continue }
        if ($structs.Contains($name)) { continue }

        $body = $m.Groups[2].Value

        # Flatten anonymous unions. Their members are addressed through the
        # enclosing struct in C, so offsetof(b3TreeNode, children) is legal and
        # every arm of the union starts at the same offset, which is exactly the
        # property worth pinning. A *named* union is a single field instead, so
        # its members are dropped and the name is kept.
        $body = [regex]::Replace($body, '(?:union|struct)\s*\{(?<inner>[^{}]*)\}\s*(?<tag>\w*)\s*;', {
            param($match)
            $tag = $match.Groups['tag'].Value
            if ([string]::IsNullOrWhiteSpace($tag)) {
                # Anonymous: hoist the members into the parent, keeping the ';'
                # separators the split below relies on.
                return $match.Groups['inner'].Value + ';'
            }
            # Named: one field, whose type happens to be an aggregate.
            return "aggregate $tag;"
        }, 'Singleline')

        $fields = [System.Collections.Generic.List[string]]::new()

        foreach ($raw in $body -split ';') {
            $decl = ($raw -replace '\s+', ' ').Trim()
            if (-not $decl) { continue }

            # Anything still carrying a brace is a construct the flattening above
            # did not understand, and an unparsed field is an unchecked field.
            if ($decl -match '[{}]') {
                $unparsed.Add("$name : nested aggregate '$decl'")
                continue
            }

            $field = Get-FieldName $decl
            if (-not $field) {
                $unparsed.Add("$name : unreadable declaration '$decl'")
                continue
            }

            $fields.Add($field)
        }

        $structs[$name] = $fields
    }
}

if ($structs.Count -eq 0) {
    throw 'No structs were parsed from the Box3D headers. The declaration style has probably changed.'
}

if ($unparsed.Count -gt 0) {
    $unparsed | ForEach-Object { Write-Host "::error::cannot record ABI for $_" }
    throw "$($unparsed.Count) field declaration(s) could not be parsed. Teach this script the construct rather than leaving them unchecked."
}

Write-Host "Parsed $($structs.Count) structs from the Box3D headers."

# ------------------------------------------------------------ emit the program

New-Item -ItemType Directory -Force $BuildDir | Out-Null
$programPath = Join-Path $BuildDir 'abi-dump.c'

$sb = [System.Text.StringBuilder]::new()
[void] $sb.AppendLine('// Generated by tools/dump-abi.ps1. Do not edit.')
[void] $sb.AppendLine('#include <stdio.h>')
[void] $sb.AppendLine('#include <stddef.h>')
[void] $sb.AppendLine('#include "box3d/box3d.h"')
[void] $sb.AppendLine('#include "box3d/collision.h"')
[void] $sb.AppendLine('#include "box3d/types.h"')
[void] $sb.AppendLine('int main(void) {')
[void] $sb.AppendLine('    printf("{\n");')
[void] $sb.AppendLine('    printf("  \"structs\": {\n");')

$structNames = @($structs.Keys)
for ($i = 0; $i -lt $structNames.Count; $i++) {
    $name = $structNames[$i]
    $fields = $structs[$name]
    $structComma = if ($i -lt $structNames.Count - 1) { ',' } else { '' }

    [void] $sb.AppendLine("    printf(`"    \`"$name\`": { \`"size\`": %zu, \`"align\`": %zu, \`"fields\`": {`", sizeof($name), _Alignof($name));")

    for ($j = 0; $j -lt $fields.Count; $j++) {
        $field = $fields[$j]
        $fieldComma = if ($j -lt $fields.Count - 1) { ', ' } else { '' }
        [void] $sb.AppendLine("    printf(`"\`"$field\`": %zu$fieldComma`", offsetof($name, $field));")
    }

    [void] $sb.AppendLine("    printf(`"} }$structComma\n`");")
}

[void] $sb.AppendLine('    printf("  }\n");')
[void] $sb.AppendLine('    printf("}\n");')
[void] $sb.AppendLine('    return 0;')
[void] $sb.AppendLine('}')

Set-Content -Path $programPath -Value $sb.ToString() -Encoding utf8
Write-Host "Wrote $programPath"

# ----------------------------------------------------------- compile and run it

# $IsWindows only exists in PowerShell Core, and this script has to run under
# Windows PowerShell 5.1 as well, where the variable is silently $null.
$onWindows = ($env:OS -eq 'Windows_NT')
$exeName = if ($onWindows) { 'abi-dump.exe' } else { 'abi-dump' }
$exePath = Join-Path $BuildDir $exeName

# The compiler is reached through CMake rather than invoked directly.
#
# The direct route needs a different command line per toolchain, and on Windows
# cl is only on PATH inside a Developer Command Prompt, which would make this
# script fail from an ordinary shell. CMake already solves all of that, the
# repository already requires it to build the native library, and using it here
# guarantees the ABI is read with the same toolchain that compiled Box3D.
$cmake = (Get-Command cmake -ErrorAction SilentlyContinue).Source

if (-not $cmake) {
    # Visual Studio ships its own CMake and does not put it on PATH. Finding it
    # means a machine with the C++ workload needs nothing else installed.
    $candidates = @()
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        foreach ($vsPath in @(& $vswhere -latest -products * -property installationPath)) {
            $candidates += Join-Path $vsPath 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
        }
    }

    $cmake = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $cmake) {
    throw 'CMake was not found on PATH, nor inside a Visual Studio installation. Install CMake 3.22 or later: https://cmake.org/download/'
}

$cmakeLists = @"
cmake_minimum_required(VERSION 3.22)
project(abi_dump C)
set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
add_executable(abi-dump abi-dump.c)
target_include_directories(abi-dump PRIVATE "$($IncludeDir -replace '\\', '/')")
# Keep the binary where this script can predict it, on every generator.
set_target_properties(abi-dump PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "`${CMAKE_BINARY_DIR}/out"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "`${CMAKE_BINARY_DIR}/out"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "`${CMAKE_BINARY_DIR}/out")
"@

Set-Content -Path (Join-Path $BuildDir 'CMakeLists.txt') -Value $cmakeLists -Encoding utf8

$cmakeBuild = Join-Path $BuildDir 'build'
$configure = & $cmake -S $BuildDir -B $cmakeBuild 2>&1
if ($LASTEXITCODE -ne 0) {
    $configure | ForEach-Object { Write-Host $_ }
    throw 'CMake failed to configure the ABI dump program.'
}

$compile = & $cmake --build $cmakeBuild --config Release 2>&1
if ($LASTEXITCODE -ne 0) {
    $compile | ForEach-Object { Write-Host $_ }
    throw 'CMake failed to build the ABI dump program.'
}

$exePath = Get-ChildItem -Path (Join-Path $cmakeBuild 'out') -Filter $exeName -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $exePath) {
    throw "The ABI dump program was built but $exeName was not found under $cmakeBuild/out."
}

$json = & $exePath
if ($LASTEXITCODE -ne 0) { throw 'The ABI dump program did not run.' }

$json = $json -join "`n"

# Prove it is well-formed before anything downstream depends on it.
try { $null = $json | ConvertFrom-Json }
catch { throw "The ABI dump produced invalid JSON: $_" }

# ------------------------------------------------------------------- write out

$outDir = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force $outDir | Out-Null

# Normalise the line endings so the file is identical on every platform and the
# -Check comparison below is not defeated by git's autocrlf.
$json = ($json -replace "`r`n", "`n").TrimEnd() + "`n"

if ($Check) {
    if (-not (Test-Path $OutputPath)) {
        throw "$OutputPath does not exist. Run: pwsh tools/dump-abi.ps1"
    }

    $existing = (Get-Content $OutputPath -Raw) -replace "`r`n", "`n"
    if ($existing.TrimEnd() -ne $json.TrimEnd()) {
        Write-Host '::error::The recorded native ABI does not match this machine.'
        Write-Host 'Box3D''s struct layout has changed. Re-run tools/dump-abi.ps1 and review the diff:'
        Write-Host '  a size or offset that moved means every managed mirror of that struct must move with it.'
        throw 'abi/native-layout.json is out of date.'
    }

    Write-Host "The recorded ABI matches this machine ($($structs.Count) structs)."
    return
}

# Written through .NET rather than Set-Content: -Encoding utf8 means "with BOM"
# in Windows PowerShell and "without" in PowerShell Core, so the same script on
# two machines would produce two different files and -Check would fail on a
# byte that has nothing to do with the ABI.
[System.IO.File]::WriteAllText($OutputPath, $json, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $OutputPath ($($structs.Count) structs)."
