#!/usr/bin/env pwsh
# SPDX-License-Identifier: MIT
#
# Answers the question a contributor actually has: why does this function exist
# in Box3D.NET.Native but not in Box3D.NET?
#
# It reads every B3_API declaration out of the Box3D headers, works out how each
# one is bound, and whether the idiomatic layer names it. The result goes to
# docs/api-coverage.md.
#
# The three classifications:
#
#   HIGH_LEVEL    bound, and named somewhere in src/Box3D.NET. Reachable through
#                 the idiomatic API.
#   NATIVE_ONLY   bound and reachable through Box3D.NET.Native, but the
#                 idiomatic layer does not use or expose it. This is not a gap
#                 to be closed on sight: much of the C API is internal
#                 machinery, recording and replay, or accessors whose idiomatic
#                 equivalent is a property that reads several of them at once.
#   UNBOUND       deliberately not bound at all. Every one of these has to
#                 appear in the reasons table below or this script fails, so
#                 nothing falls out of the binding unnoticed.
#
# Usage:
#   pwsh tools/api-coverage.ps1            # rewrite docs/api-coverage.md
#   pwsh tools/api-coverage.ps1 -Check     # fail if it would change

[CmdletBinding()]
param(
    # Fail instead of writing, for CI.
    [switch] $Check
)

$ErrorActionPreference = 'Stop'

$RepoRoot   = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$IncludeDir = Join-Path $RepoRoot 'external/box3d/include/box3d'
$NativeDir  = Join-Path $RepoRoot 'src/Box3D.NET.Native'
$HighDir    = Join-Path $RepoRoot 'src/Box3D.NET'
$OutputPath = Join-Path $RepoRoot 'docs/api-coverage.md'

if (-not (Test-Path $IncludeDir)) {
    throw "Box3D headers not found at $IncludeDir. Run: git submodule update --init --recursive"
}

# Why a declaration is not bound at all. A function missing from the binding and
# missing from here is an error: the whole point is that dropping one has to be
# a decision someone wrote down.
$UnboundReasons = @{
    'b3InternalAssert' = 'Compiled only when NDEBUG is undefined, so it is absent from the release binary this package ships. Binding it would produce an EntryPointNotFoundException at run time.'
}

# ------------------------------------------------------------- read the headers

$declarations = [System.Collections.Generic.List[object]]::new()

foreach ($header in Get-ChildItem $IncludeDir -Filter *.h | Sort-Object Name) {
    $text = (Get-Content $header.FullName -Raw) -replace "`r`n", "`n"
    $lines = $text -split "`n"

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -notmatch '^\s*B3_API\s') { continue }

        $decl = $lines[$i]
        while ($decl -notmatch ';' -and $i + 1 -lt $lines.Count) {
            $i++
            $decl += ' ' + $lines[$i].Trim()
        }

        if ($decl -match '\b(b3[A-Za-z0-9_]+)\s*\(') {
            $declarations.Add([pscustomobject]@{
                Name   = $Matches[1]
                Header = $header.Name
            })
        }
    }
}

if ($declarations.Count -eq 0) {
    throw "No B3_API declarations were found under $IncludeDir. The parser is broken, not the headers."
}

# ------------------------------------------------- read the binding and the API

# Where each name is declared on the managed side. Generated and hand-written
# are distinguished because they are maintained differently: one is re-emitted
# from the headers, the other is not and can therefore drift.
$generated = @{}
foreach ($file in Get-ChildItem (Join-Path $NativeDir 'Generated') -Filter *.g.cs) {
    foreach ($match in [regex]::Matches((Get-Content $file.FullName -Raw), 'partial\s+[\w\.\*\[\]<>]+\s+(b3[A-Za-z0-9_]+)\s*\(')) {
        $generated[$match.Groups[1].Value] = $file.Name
    }
}

$handWritten = @{}
foreach ($file in Get-ChildItem $NativeDir -Filter *.cs -Recurse |
         Where-Object { $_.FullName -notlike '*\Generated\*' -and $_.FullName -notlike '*\obj\*' }) {
    $content = Get-Content $file.FullName -Raw
    foreach ($match in [regex]::Matches($content, '(?:partial|static)\s+[\w\.\*\[\]<>]+\s+(b3[A-Za-z0-9_]+)\s*\(')) {
        $handWritten[$match.Groups[1].Value] = $file.Name
    }
}

# Everything the idiomatic layer mentions, as one blob. A name appearing here
# means Box3D.NET reaches that function on the caller's behalf.
$highLevelText = (Get-ChildItem $HighDir -Filter *.cs -Recurse |
    Where-Object { $_.FullName -notlike '*\obj\*' } |
    ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

# ------------------------------------------------------------------- classify

$rows = foreach ($declaration in $declarations) {
    $name = $declaration.Name

    $binding =
        if ($generated.ContainsKey($name))   { 'generated' }
        elseif ($handWritten.ContainsKey($name)) { 'hand-written' }
        else { 'none' }

    $classification =
        if ($binding -eq 'none') { 'UNBOUND' }
        elseif ($highLevelText -match "\b$([regex]::Escape($name))\b") { 'HIGH_LEVEL' }
        else { 'NATIVE_ONLY' }

    [pscustomobject]@{
        Name           = $name
        Header         = $declaration.Header
        Binding        = $binding
        Classification = $classification
    }
}

$unbound = @($rows | Where-Object Classification -eq 'UNBOUND')
$undocumented = @($unbound | Where-Object { -not $UnboundReasons.ContainsKey($_.Name) })

if ($undocumented) {
    throw ("These Box3D functions are not bound and no reason is recorded for it in " +
           "tools/api-coverage.ps1: $($undocumented.Name -join ', '). Either bind them or say why not.")
}

$total       = $rows.Count
$highLevel   = @($rows | Where-Object Classification -eq 'HIGH_LEVEL').Count
$nativeOnly  = @($rows | Where-Object Classification -eq 'NATIVE_ONLY').Count
$generatedCount   = @($rows | Where-Object Binding -eq 'generated').Count
$handWrittenCount = @($rows | Where-Object Binding -eq 'hand-written').Count

# --------------------------------------------------------------- write it out

$sb = [System.Text.StringBuilder]::new()
$null = $sb.AppendLine('<!--')
$null = $sb.AppendLine('  Generated by tools/api-coverage.ps1. Do not edit by hand.')
$null = $sb.AppendLine('-->')
$null = $sb.AppendLine()
$null = $sb.AppendLine('# API coverage')
$null = $sb.AppendLine()
$null = $sb.AppendLine('Every function Box3D exports, how it is bound, and whether the idiomatic')
$null = $sb.AppendLine('layer reaches it. Regenerate with `pwsh tools/api-coverage.ps1`; CI fails if')
$null = $sb.AppendLine('this file and the headers disagree.')
$null = $sb.AppendLine()
$null = $sb.AppendLine('## Totals')
$null = $sb.AppendLine()
$null = $sb.AppendLine('| | Count |')
$null = $sb.AppendLine('| --- | ---: |')
# A backtick is PowerShell's escape character, so a literal one in an
# interpolated string has to come from a variable rather than from the source.
$tick = [char] 0x60

$null = $sb.AppendLine("| Functions declared $tick`B3_API$tick in the Box3D headers | $total |")
$null = $sb.AppendLine("| Bound by the generator | $generatedCount |")
$null = $sb.AppendLine("| Bound by hand | $handWrittenCount |")
$null = $sb.AppendLine("| Deliberately not bound | $($unbound.Count) |")
$null = $sb.AppendLine("| Reachable through $tick`Box3D.NET$tick (HIGH_LEVEL) | $highLevel |")
$null = $sb.AppendLine("| Reachable only through $tick`Box3D.NET.Native$tick (NATIVE_ONLY) | $nativeOnly |")
$null = $sb.AppendLine()
$null = $sb.AppendLine('`NATIVE_ONLY` is not a to-do list. Most of it is machinery an idiomatic API')
$null = $sb.AppendLine('should not surface: individual accessors that a single property reads several')
$null = $sb.AppendLine('of at once, recording and replay, tree internals, and the profiling and')
$null = $sb.AppendLine('counter functions. The escape hatch in `Box3D.Interop` exists so that needing')
$null = $sb.AppendLine('one of them is an inconvenience rather than a wall.')
$null = $sb.AppendLine()

if ($unbound.Count -gt 0) {
    $null = $sb.AppendLine('## Not bound')
    $null = $sb.AppendLine()
    $null = $sb.AppendLine('| Function | Why |')
    $null = $sb.AppendLine('| --- | --- |')
    foreach ($row in $unbound | Sort-Object Name) {
        $null = $sb.AppendLine("| ``$($row.Name)`` | $($UnboundReasons[$row.Name]) |")
    }
    $null = $sb.AppendLine()
}

$null = $sb.AppendLine('## Every function')
$null = $sb.AppendLine()

foreach ($group in $rows | Group-Object Header | Sort-Object Name) {
    $null = $sb.AppendLine("### ``$($group.Name)``")
    $null = $sb.AppendLine()
    $null = $sb.AppendLine('| Function | Binding | Reach |')
    $null = $sb.AppendLine('| --- | --- | --- |')
    foreach ($row in $group.Group | Sort-Object Name) {
        $null = $sb.AppendLine("| ``$($row.Name)`` | $($row.Binding) | $($row.Classification) |")
    }
    $null = $sb.AppendLine()
}

$rendered = $sb.ToString() -replace "`r`n", "`n"

if ($Check) {
    if (-not (Test-Path $OutputPath)) {
        throw "docs/api-coverage.md does not exist. Run: pwsh tools/api-coverage.ps1"
    }

    $existing = (Get-Content $OutputPath -Raw) -replace "`r`n", "`n"
    if ($existing -ne $rendered) {
        throw "docs/api-coverage.md is out of date. Run: pwsh tools/api-coverage.ps1"
    }

    Write-Host "API coverage is up to date: $total functions, $highLevel high-level, $nativeOnly native-only, $($unbound.Count) unbound."
    return
}

Set-Content -Path $OutputPath -Value $rendered -Encoding utf8 -NoNewline
Write-Host "Wrote $OutputPath"
Write-Host "  $total functions declared in the headers"
Write-Host "  $generatedCount generated, $handWrittenCount hand-written, $($unbound.Count) deliberately unbound"
Write-Host "  $highLevel HIGH_LEVEL, $nativeOnly NATIVE_ONLY"
