#!/usr/bin/env pwsh
# SPDX-License-Identifier: MIT
#
# Generates the P/Invoke declarations of Box3D.NET.Native from the Box3D headers.
#
# The binding is a mechanical translation of ~500 C declarations. Generating it
# removes the risk of a mistyped parameter - a class of bug that does not fail
# at compile time and corrupts the stack at run time - and makes updating to a
# new Box3D release a matter of re-running this script and reading the diff.
#
# The Doxygen comments in the headers are converted to XML documentation, so the
# generated API carries Box3D's own prose rather than a paraphrase of it.
#
# Usage:
#   pwsh tools/generate-bindings.ps1
#
# Reads:  external/box3d/include/box3d/*.h
# Writes: src/Box3D.NET.Native/Generated/*.g.cs

[CmdletBinding()]
param(
    [string] $RepoRoot
)

$ErrorActionPreference = 'Stop'

if (-not $RepoRoot) {
    $RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}

$IncludeDir = Join-Path $RepoRoot 'external/box3d/include/box3d'
$OutputDir  = Join-Path $RepoRoot 'src/Box3D.NET.Native/Generated'

if (-not (Test-Path $IncludeDir)) {
    throw "Box3D headers not found at $IncludeDir. Run: git submodule update --init"
}

New-Item -ItemType Directory -Force $OutputDir | Out-Null

# ---------------------------------------------------------------- type mapping

# Scalar and struct types that map one to one.
$TypeMap = @{
    'void'             = 'void'
    'bool'             = 'NativeBool'
    'char'             = 'byte'
    'int'              = 'int'
    'float'            = 'float'
    'double'           = 'double'
    'int8_t'           = 'sbyte'
    'int16_t'          = 'short'
    'int32_t'          = 'int'
    'int64_t'          = 'long'
    'uint8_t'          = 'byte'
    'uint16_t'         = 'ushort'
    'uint32_t'         = 'uint'
    'uint64_t'         = 'ulong'
    'size_t'           = 'nuint'
    'b3Vec3'           = 'Vector3'
    'b3Pos'            = 'Vector3'
    'b3Quat'           = 'Quaternion'
    'b3WorldTransform' = 'b3Transform'
    'b3WorldCastOutput'= 'b3CastOutput'
}

# The function-pointer typedefs, as C# unmanaged function pointer types.
$CallbackMap = @{
    'b3TaskCallback'                  = 'delegate* unmanaged[Cdecl]<void*, void>'
    'b3EnqueueTaskCallback'           = 'delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<void*, void>, void*, void*, byte*, void*>'
    'b3FinishTaskCallback'            = 'delegate* unmanaged[Cdecl]<void*, void*, void>'
    'b3CreateDebugShapeCallback'      = 'delegate* unmanaged[Cdecl]<b3DebugShape*, void*, void*>'
    'b3DestroyDebugShapeCallback'     = 'delegate* unmanaged[Cdecl]<void*, void*, void>'
    'b3FrictionCallback'              = 'delegate* unmanaged[Cdecl]<float, ulong, float, ulong, float>'
    'b3RestitutionCallback'           = 'delegate* unmanaged[Cdecl]<float, ulong, float, ulong, float>'
    'b3CustomFilterFcn'               = 'delegate* unmanaged[Cdecl]<b3ShapeId, b3ShapeId, void*, NativeBool>'
    'b3PreSolveFcn'                   = 'delegate* unmanaged[Cdecl]<b3ShapeId, b3ShapeId, Vector3, Vector3, void*, NativeBool>'
    'b3OverlapResultFcn'              = 'delegate* unmanaged[Cdecl]<b3ShapeId, void*, NativeBool>'
    'b3CastResultFcn'                 = 'delegate* unmanaged[Cdecl]<b3ShapeId, Vector3, Vector3, float, ulong, int, int, void*, float>'
    'b3TreeQueryCallbackFcn'          = 'delegate* unmanaged[Cdecl]<int, ulong, void*, NativeBool>'
    'b3TreeQueryClosestCallbackFcn'   = 'delegate* unmanaged[Cdecl]<float, int, ulong, void*, float>'
    'b3TreeBoxCastCallbackFcn'        = 'delegate* unmanaged[Cdecl]<b3BoxCastInput*, int, ulong, void*, float>'
    'b3TreeRayCastCallbackFcn'        = 'delegate* unmanaged[Cdecl]<b3RayCastInput*, int, ulong, void*, float>'
    'b3PlaneResultFcn'                = 'delegate* unmanaged[Cdecl]<b3ShapeId, b3PlaneResult*, int, void*, NativeBool>'
    'b3MoverFilterFcn'                = 'delegate* unmanaged[Cdecl]<b3ShapeId, void*, NativeBool>'
    'b3CompoundQueryFcn'              = 'delegate* unmanaged[Cdecl]<b3CompoundData*, int, void*, NativeBool>'
    'b3MeshQueryFcn'                  = 'delegate* unmanaged[Cdecl]<Vector3, Vector3, Vector3, int, void*, NativeBool>'
    'b3AllocFcn'                      = 'delegate* unmanaged[Cdecl]<int, int, void*>'
    'b3FreeFcn'                       = 'delegate* unmanaged[Cdecl]<void*, void>'
    'b3AssertFcn'                     = 'delegate* unmanaged[Cdecl]<byte*, byte*, int, int>'
    'b3LogFcn'                        = 'delegate* unmanaged[Cdecl]<byte*, void>'
}

# Functions already written by hand in B3.Base.cs, or that are not present in a
# release build of the library.
$Skip = @(
    # Declared in base.h and constants.h, hand-written with extra documentation.
    'b3SetAllocator', 'b3GetByteCount', 'b3SetAssertFcn', 'b3SetLogFcn',
    'b3GetVersion', 'b3IsDoublePrecision', 'b3GetTicks', 'b3GetMilliseconds',
    'b3GetMillisecondsAndReset', 'b3Yield', 'b3Sleep', 'b3Hash',
    'b3SetLengthUnitsPerMeter', 'b3GetLengthUnitsPerMeter',
    'b3SetStallThreshold', 'b3GetStallThreshold',
    'b3Atan2', 'b3ComputeCosSin', 'b3MakeQuatFromMatrix',
    'b3ComputeQuatBetweenUnitVectors', 'b3Steiner', 'b3PointToSegmentDistance',
    'b3LineDistance', 'b3SegmentDistance', 'b3IsValidFloat', 'b3IsValidVec3',
    'b3IsValidQuat', 'b3IsValidTransform', 'b3IsValidMatrix3', 'b3IsValidAABB',
    'b3IsBoundedAABB', 'b3IsSaneAABB', 'b3IsValidPlane', 'b3IsValidPosition',
    'b3IsValidWorldTransform', 'b3GetGraphColor',

    # Only compiled when NDEBUG is not defined, so it is absent from the shipped
    # release binaries and cannot be bound safely.
    'b3InternalAssert'
)

function ConvertTo-CSharpType {
    param([string] $CType, [string] $ParamName)

    $t = $CType.Trim()
    $t = $t -replace '\bconst\b', ''
    $t = $t -replace '\bstruct\b', ''
    $t = $t.Trim()

    # Count and strip pointer levels.
    $stars = ([regex]::Matches($t, '\*')).Count
    $t = ($t -replace '\*', '').Trim()

    # A pointer to a function-pointer typedef is just the function pointer.
    if ($CallbackMap.ContainsKey($t)) {
        return $CallbackMap[$t]
    }

    if ($TypeMap.ContainsKey($t)) {
        return $TypeMap[$t] + ('*' * $stars)
    }

    # An unmapped type is only safe when it names a Box3D aggregate, because
    # those have a hand-written mirror of the same name in Box3D.NET.Native and
    # AbiTests holds that mirror to the C layout.
    #
    # Anything else must not be passed through. A C keyword this script has not
    # been taught — long, unsigned, wchar_t — would be emitted verbatim as a C#
    # type name, and the two languages agree on far fewer of those than they
    # appear to. The failure would be a binding that compiles and reads the
    # wrong width, which is precisely what generating this file is meant to
    # prevent. Refuse instead, and add the type to $TypeMap.
    if ($t -notmatch '^b3[A-Za-z0-9_]*$') {
        throw "Unmapped C type '$CType'" + $(if ($ParamName) { " on parameter '$ParamName'" } else { '' }) +
              ". Add it to `$TypeMap in tools/generate-bindings.ps1 rather than letting it through: " +
              'an unmapped type is emitted verbatim and silently binds to whatever C# type shares its name.'
    }

    return $t + ('*' * $stars)
}

function Format-XmlText {
    param([string] $Text)
    $t = $Text -replace '&', '&amp;'
    $t = $t -replace '<', '&lt;'
    $t = $t -replace '>', '&gt;'
    return $t
}

# Turns the Doxygen block above a declaration into XML documentation.
function ConvertTo-XmlDoc {
    param([string[]] $DocLines, [string[]] $ParamNames, [bool] $HasReturn)

    $summary = [System.Collections.Generic.List[string]]::new()
    $remarks = [System.Collections.Generic.List[string]]::new()
    $returns = [System.Collections.Generic.List[string]]::new()
    $params  = [ordered]@{}

    $current = 'summary'
    $currentParam = $null

    foreach ($raw in $DocLines) {
        $line = $raw -replace '^\s*///\s?', ''
        $line = $line -replace '^\s*//!\s?', ''
        $line = $line.TrimEnd()

        if ($line -match '^\s*[@\\]param(?:\[[^\]]*\])?\s+(\w+)\s*(.*)$') {
            $current = 'param'
            $currentParam = $Matches[1]
            $params[$currentParam] = [System.Collections.Generic.List[string]]::new()
            if ($Matches[2]) { $params[$currentParam].Add($Matches[2]) }
            continue
        }
        if ($line -match '^\s*[@\\]returns?\s*(.*)$') {
            $current = 'returns'
            if ($Matches[1]) { $returns.Add($Matches[1]) }
            continue
        }
        if ($line -match '^\s*[@\\](warning|note|see|deprecated|ingroup|defgroup|brief|code|endcode|\{|\})\b\s*(.*)$') {
            $tag = $Matches[1]
            $rest = $Matches[2]
            switch ($tag) {
                'brief'    { $current = 'summary'; if ($rest) { $summary.Add($rest) }; continue }
                'ingroup'  { continue }
                'defgroup' { continue }
                'code'     { continue }
                'endcode'  { continue }
                '{'        { continue }
                '}'        { continue }
                default {
                    $current = 'remarks'
                    $label = switch ($tag) {
                        'warning'    { 'Warning:' }
                        'note'       { 'Note:' }
                        'see'        { 'See' }
                        'deprecated' { 'Deprecated.' }
                    }
                    $text = if ($rest) { "$label $rest" } else { $label }
                    $remarks.Add($text)
                    continue
                }
            }
        }

        if ([string]::IsNullOrWhiteSpace($line)) { continue }

        switch ($current) {
            'summary' { $summary.Add($line) }
            'remarks' { $remarks.Add($line) }
            'returns' { $returns.Add($line) }
            'param'   { if ($currentParam) { $params[$currentParam].Add($line) } }
        }
    }

    $out = [System.Collections.Generic.List[string]]::new()

    $summaryText = ($summary -join ' ').Trim()
    if (-not $summaryText) { $summaryText = 'See the Box3D documentation.' }
    $out.Add('    /// <summary>')
    foreach ($chunk in (Split-Wrapped (Format-XmlText $summaryText))) {
        $out.Add("    /// $chunk")
    }
    $out.Add('    /// </summary>')

    foreach ($p in $ParamNames) {
        $desc = if ($params.Contains($p)) { (($params[$p]) -join ' ').Trim() } else { '' }
        if (-not $desc) { $desc = 'See the Box3D documentation.' }
        $safeName = $p
        $out.Add("    /// <param name=`"$safeName`">$(Format-XmlText $desc)</param>")
    }

    if ($HasReturn) {
        $r = ($returns -join ' ').Trim()
        if (-not $r) { $r = 'See the Box3D documentation.' }
        $out.Add("    /// <returns>$(Format-XmlText $r)</returns>")
    }

    if ($remarks.Count -gt 0) {
        $out.Add('    /// <remarks>')
        foreach ($chunk in (Split-Wrapped (Format-XmlText (($remarks -join ' ').Trim())))) {
            $out.Add("    /// $chunk")
        }
        $out.Add('    /// </remarks>')
    }

    return $out
}

function Split-Wrapped {
    param([string] $Text, [int] $Width = 88)
    $words = $Text -split '\s+' | Where-Object { $_ }
    $lines = [System.Collections.Generic.List[string]]::new()
    $cur = ''
    foreach ($w in $words) {
        if ($cur.Length -eq 0) { $cur = $w }
        elseif (($cur.Length + 1 + $w.Length) -le $Width) { $cur = "$cur $w" }
        else { $lines.Add($cur); $cur = $w }
    }
    if ($cur) { $lines.Add($cur) }
    if ($lines.Count -eq 0) { $lines.Add('') }
    return $lines
}

# ---------------------------------------------------------------- header parse

function Get-Declarations {
    param([string] $Path)

    $text = Get-Content $Path -Raw
    # Normalise line endings and join declarations that span several lines.
    $lines = ($text -replace "`r`n", "`n") -split "`n"

    $results = [System.Collections.Generic.List[object]]::new()
    $doc = [System.Collections.Generic.List[string]]::new()

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]

        if ($line -match '^\s*(///|//!)') {
            $doc.Add($line)
            continue
        }

        if ($line -match '^\s*B3_API\s') {
            # Accumulate until the closing semicolon.
            $decl = $line
            while ($decl -notmatch ';' -and $i + 1 -lt $lines.Count) {
                $i++
                $decl = $decl + ' ' + $lines[$i].Trim()
            }
            $results.Add([pscustomobject]@{
                Declaration = ($decl -replace '\s+', ' ').Trim()
                Doc         = @($doc)
            })
            $doc.Clear()
            continue
        }

        # A blank line or unrelated code detaches the pending comment block.
        if ($line -notmatch '^\s*$') { $doc.Clear() }
        elseif ($doc.Count -gt 0) { $doc.Clear() }
    }

    return $results
}

function ConvertTo-Binding {
    param([object] $Decl)

    $d = $Decl.Declaration
    if ($d -notmatch '^B3_API\s+(.+?)\s*\(\s*(.*?)\s*\)\s*;$') { return $null }

    $head = $Matches[1].Trim()
    $argsText = $Matches[2].Trim()

    # Split the return type from the function name.
    if ($head -notmatch '^(.*?[\s\*])(\w+)$') { return $null }
    $retType = $Matches[1].Trim()
    $name    = $Matches[2].Trim()

    if ($Skip -contains $name) { return $null }

    $csReturn = ConvertTo-CSharpType $retType ''
    $hasReturn = $csReturn -ne 'void'

    $paramNames = [System.Collections.Generic.List[string]]::new()
    $csParams   = [System.Collections.Generic.List[string]]::new()

    if ($argsText -and $argsText -ne 'void') {
        foreach ($arg in ($argsText -split ',')) {
            $a = $arg.Trim()
            if (-not $a) { continue }

            # An array parameter such as "uint32_t values[3]".
            $isArray = $false
            if ($a -match '^(.*?)\s*\[\s*\d*\s*\]$') { $a = $Matches[1].Trim(); $isArray = $true }

            if ($a -notmatch '^(.*?[\s\*])(\w+)$') { return $null }
            $pType = $Matches[1].Trim()
            $pName = $Matches[2].Trim()

            $csType = ConvertTo-CSharpType $pType $pName
            if ($isArray) { $csType += '*' }

            # Escape C# keywords used as parameter names.
            if ($pName -in @('base','ref','out','in','params','object','string','event','lock','fixed','value')) {
                $pName = '@' + $pName
            }

            $paramNames.Add($pName.TrimStart('@'))
            $csParams.Add("$csType $pName")
        }
    }

    return [pscustomobject]@{
        Name       = $name
        Return     = $csReturn
        HasReturn  = $hasReturn
        Params     = $csParams
        ParamNames = $paramNames
        Doc        = $Decl.Doc
    }
}

# ---------------------------------------------------------------- emit

$headerGroups = @(
    @{ File = 'box3d.h';     Out = 'B3.Box3D.g.cs' }
    @{ File = 'collision.h'; Out = 'B3.Collision.g.cs' }
    @{ File = 'types.h';     Out = 'B3.Types.g.cs' }
)

$total = 0

foreach ($group in $headerGroups) {
    $path = Join-Path $IncludeDir $group.File
    $decls = Get-Declarations $path

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine('// SPDX-License-Identifier: MIT')
    [void]$sb.AppendLine('//')
    [void]$sb.AppendLine("// <auto-generated>")
    [void]$sb.AppendLine("//     Generated from include/box3d/$($group.File) by tools/generate-bindings.ps1.")
    [void]$sb.AppendLine('//     Do not edit. Re-run the script after updating the Box3D submodule.')
    [void]$sb.AppendLine('// </auto-generated>')
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('using System.Numerics;')
    [void]$sb.AppendLine('using System.Runtime.CompilerServices;')
    [void]$sb.AppendLine('using System.Runtime.InteropServices;')
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('namespace Box3D.Native;')
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('public static unsafe partial class B3')
    [void]$sb.AppendLine('{')

    $count = 0
    $first = $true

    foreach ($decl in $decls) {
        $b = ConvertTo-Binding $decl
        if ($null -eq $b) { continue }

        if (-not $first) { [void]$sb.AppendLine('') }
        $first = $false

        foreach ($line in (ConvertTo-XmlDoc $b.Doc $b.ParamNames $b.HasReturn)) {
            [void]$sb.AppendLine($line)
        }

        [void]$sb.AppendLine("    [LibraryImport(Box3DLibrary.Name)]")
        [void]$sb.AppendLine("    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]")

        $paramList = ($b.Params -join ', ')
        $signature = "    public static partial $($b.Return) $($b.Name)($paramList);"
        if ($signature.Length -le 120) {
            [void]$sb.AppendLine($signature)
        }
        else {
            [void]$sb.AppendLine("    public static partial $($b.Return) $($b.Name)(")
            for ($k = 0; $k -lt $b.Params.Count; $k++) {
                $sep = if ($k -lt $b.Params.Count - 1) { ',' } else { ');' }
                [void]$sb.AppendLine("        $($b.Params[$k])$sep")
            }
        }

        $count++
    }

    [void]$sb.AppendLine('}')

    $outPath = Join-Path $OutputDir $group.Out
    $content = $sb.ToString() -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText($outPath, $content, (New-Object System.Text.UTF8Encoding($false)))

    Write-Host ("{0,-16} -> {1,-22} {2,4} functions" -f $group.File, $group.Out, $count)
    $total += $count
}

Write-Host "total: $total generated bindings"

# --------------------------------------------------------- record the source
#
# Which Box3D produced these declarations is not recoverable from the output
# otherwise. The submodule pointer answers it for a checkout, but not for a
# package someone downloaded, and "regenerate and read the diff" is no help
# when the question is what the assembly in front of you was built against.
#
# Emitting it also gives CI's up-to-date check something to catch: bumping the
# submodule without regenerating changes this constant, so the build fails
# rather than shipping bindings from the previous version.

$commit = & git -C (Join-Path $RepoRoot 'external/box3d') rev-parse HEAD 2>$null
if ($LASTEXITCODE -ne 0 -or -not $commit) {
    throw 'Could not read the Box3D submodule commit. Run: git submodule update --init --recursive'
}

$commit = $commit.Trim()

$describe = & git -C (Join-Path $RepoRoot 'external/box3d') describe --tags --always 2>$null
$describe = if ($LASTEXITCODE -eq 0 -and $describe) { $describe.Trim() } else { $commit.Substring(0, 12) }

$versionSource = @"
// SPDX-License-Identifier: MIT
//
// <auto-generated>
//     Generated by tools/generate-bindings.ps1.
//     Do not edit. Re-run the script after updating the Box3D submodule.
// </auto-generated>

namespace Box3D.Native;

/// <summary>
/// Identifies the Box3D revision these bindings were generated from.
/// </summary>
/// <remarks>
/// This is a compile-time fact about the declarations, not a run-time one about
/// the loaded library. <c>B3.b3GetVersion</c> reports what the native binary
/// says it is; this reports what the headers said when the P/Invokes were
/// written. They disagree exactly when a binary has been swapped for one built
/// from different sources, which is worth being able to tell.
/// </remarks>
public static class BindingSource
{
    /// <summary>The Box3D commit the declarations were generated from.</summary>
    public const string Commit = "$commit";

    /// <summary>The same revision as <c>git describe</c> renders it.</summary>
    public const string Description = "$describe";
}
"@

$versionPath = Join-Path $OutputDir 'BindingSource.g.cs'
$versionSource = $versionSource -replace "`r`n", "`n"
[System.IO.File]::WriteAllText($versionPath, $versionSource, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "recorded Box3D source: $describe ($($commit.Substring(0, 12)))"
