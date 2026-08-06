#!/usr/bin/env pwsh
# SPDX-License-Identifier: MIT
#
# Draws the package icon.
#
# The icon is generated rather than committed as an opaque binary so that it can
# be reviewed as code, adjusted without a design tool, and regenerated at any
# size. NuGet wants 128x128; the same script emits the larger size used by the
# documentation site.
#
# Usage:
#   powershell -File tools/generate-icon.ps1
#
# Writes: assets/icon.png, assets/icon-256.png

[CmdletBinding()]
param(
    [string] $RepoRoot
)

$ErrorActionPreference = 'Stop'

if (-not $RepoRoot) {
    $RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}

Add-Type -AssemblyName System.Drawing

$assets = Join-Path $RepoRoot 'assets'
New-Item -ItemType Directory -Force $assets | Out-Null

function New-Icon {
    param([int] $Size, [string] $Path)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

        # The .NET purple, so the package reads as a .NET library at a glance.
        $background = [System.Drawing.Color]::FromArgb(255, 81, 43, 212)
        $g.Clear($background)

        # An isometric cube: the most direct way to say "3D rigid body" at
        # sixteen pixels, which is where a NuGet icon actually gets read.
        $cx = $Size * 0.5
        $cy = $Size * 0.50
        $s = $Size * 0.33
        $w = $s * 0.866   # cos(30 degrees), the isometric half-width

        # Three faces, lit from above so the form reads without an outline.
        $topColor    = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
        $leftColor   = [System.Drawing.Color]::FromArgb(255, 176, 158, 245)
        $rightColor  = [System.Drawing.Color]::FromArgb(255, 214, 205, 250)

        # Coordinates are worked out first. Inside a New-Object argument list the
        # comma binds tighter than the arithmetic, so PointF($cx, $cy - $s) parses
        # as ($cx, $cy) - $s and fails on an array subtraction.
        $apexY = [float]($cy - $s)
        $upperY = [float]($cy - ($s * 0.5))
        $lowerY = [float]($cy + ($s * 0.5))
        $baseY = [float]($cy + $s)
        $leftX = [float]($cx - $w)
        $rightX = [float]($cx + $w)
        $midX = [float]$cx
        $midY = [float]$cy

        $top = @(
            (New-Object System.Drawing.PointF $midX, $apexY),
            (New-Object System.Drawing.PointF $rightX, $upperY),
            (New-Object System.Drawing.PointF $midX, $midY),
            (New-Object System.Drawing.PointF $leftX, $upperY)
        )

        $left = @(
            (New-Object System.Drawing.PointF $leftX, $upperY),
            (New-Object System.Drawing.PointF $midX, $midY),
            (New-Object System.Drawing.PointF $midX, $baseY),
            (New-Object System.Drawing.PointF $leftX, $lowerY)
        )

        $right = @(
            (New-Object System.Drawing.PointF $rightX, $upperY),
            (New-Object System.Drawing.PointF $midX, $midY),
            (New-Object System.Drawing.PointF $midX, $baseY),
            (New-Object System.Drawing.PointF $rightX, $lowerY)
        )

        # Drawn one at a time rather than from a list of pairs, because
        # PowerShell flattens nested arrays and the pairs come apart.
        $brush = New-Object System.Drawing.SolidBrush($topColor)
        try { $g.FillPolygon($brush, [System.Drawing.PointF[]] $top) } finally { $brush.Dispose() }

        $brush = New-Object System.Drawing.SolidBrush($leftColor)
        try { $g.FillPolygon($brush, [System.Drawing.PointF[]] $left) } finally { $brush.Dispose() }

        $brush = New-Object System.Drawing.SolidBrush($rightColor)
        try { $g.FillPolygon($brush, [System.Drawing.PointF[]] $right) } finally { $brush.Dispose() }

        # No motion arc, no outline, no gradient.
        #
        # A NuGet icon is read at sixteen pixels in a package list far more often
        # than at full size. An earlier version drew a motion arc behind the cube
        # to suggest physics; at any real size the cube covered its middle and the
        # two protruding ends read as a rendering fault rather than as movement.
        # Three flat faces and a strong silhouette survive the downscale, which is
        # the only thing that matters here.

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $g.Dispose()
        $bitmap.Dispose()
    }
}

New-Icon -Size 128 -Path (Join-Path $assets 'icon.png')
New-Icon -Size 256 -Path (Join-Path $assets 'icon-256.png')

foreach ($file in @('icon.png', 'icon-256.png')) {
    $info = Get-Item (Join-Path $assets $file)
    "{0,-14} {1,6:N0} bytes" -f $info.Name, $info.Length
}
