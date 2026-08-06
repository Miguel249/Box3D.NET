#!/usr/bin/env pwsh
# SPDX-License-Identifier: MIT
#
# Points the project at your GitHub repository.
#
# The repository URL appears in the package metadata, the changelog, the DocFX
# "edit this page" links and the README. Editing them by hand means editing
# seven places and missing one, which shows up later as a broken link in a
# published package.
#
# Usage:
#   powershell -File tools/set-repository.ps1 -Owner myname -Repository Box3D.NET
#   powershell -File tools/set-repository.ps1 -Owner myname -Repository Box3D.NET -WhatIf
#
# The documentation site URL is derived as https://<owner>.github.io/<repository>/,
# which is where GitHub Pages publishes a project site.

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    # Your GitHub username or organisation.
    [Parameter(Mandatory = $true)]
    [string] $Owner,

    # The repository name.
    [Parameter(Mandatory = $true)]
    [string] $Repository,

    [string] $RepoRoot
)

$ErrorActionPreference = 'Stop'

if (-not $RepoRoot) {
    $RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}

$placeholderOwner = 'box3d-net'
$placeholderRepo = 'Box3D.NET'

$files = @(
    'Directory.Build.props'
    'CHANGELOG.md'
    'README.md'
    'docs/docfx.json'
)

$replacements = [ordered]@{
    "github.com/$placeholderOwner/$placeholderRepo"      = "github.com/$Owner/$Repository"
    "$placeholderOwner.github.io/$placeholderRepo/"      = "$Owner.github.io/$Repository/"
}

$encoding = New-Object System.Text.UTF8Encoding($false)
$changed = 0

foreach ($relative in $files) {
    $path = Join-Path $RepoRoot $relative

    if (-not (Test-Path $path)) {
        Write-Warning "skipped, not found: $relative"
        continue
    }

    $original = [System.IO.File]::ReadAllText($path)
    $updated = $original

    foreach ($from in $replacements.Keys) {
        $updated = $updated.Replace($from, $replacements[$from])
    }

    if ($updated -eq $original) {
        Write-Host ("{0,-26} no change" -f $relative)
        continue
    }

    if ($PSCmdlet.ShouldProcess($relative, 'rewrite repository URLs')) {
        [System.IO.File]::WriteAllText($path, $updated, $encoding)
    }

    Write-Host ("{0,-26} updated" -f $relative)
    $changed++
}

Write-Host ""
Write-Host "$changed file(s) updated."

# Nothing may still refer to the placeholder, or a published package carries a
# link to a repository that does not exist.
$remaining = Get-ChildItem $RepoRoot -Recurse -File -Include *.md, *.props, *.json, *.yml |
    Where-Object { $_.FullName -notmatch '\\(_site|obj|bin|artifacts|external)\\' } |
    Select-String -Pattern $placeholderOwner -SimpleMatch

if ($remaining) {
    Write-Host ""
    Write-Warning 'The placeholder still appears in:'
    $remaining | ForEach-Object { Write-Host "  $($_.Path):$($_.LineNumber)" }
    exit 1
}

Write-Host "No placeholder references remain."
