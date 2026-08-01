<#
.SYNOPSIS
    Reads one version's section out of CHANGELOG.md.

.DESCRIPTION
    The release workflow calls this to build the GitHub Release body. It fails when the
    requested version has no section, or when the section is empty, which stops an
    undocumented tag before it reaches nuget.org.

    Run it locally to see what a tag would publish:

        ./.github/scripts/Get-ReleaseNotes.ps1 -Version v1.0.0-preview.2

.PARAMETER Version
    Version to extract. A leading "v" is stripped, so a tag name works unchanged.

.PARAMETER ChangelogPath
    Path to the changelog. Defaults to CHANGELOG.md in the working directory.

.PARAMETER OutputPath
    When set, the notes are written here as well as to the pipeline.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,

    [string] $ChangelogPath = 'CHANGELOG.md',

    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$targetVersion = $Version.TrimStart('v')
$header = "## [$targetVersion]"

if (-not (Test-Path -LiteralPath $ChangelogPath)) {
    Write-Error "No changelog at $ChangelogPath."
    exit 1
}

$lines = @(Get-Content -LiteralPath $ChangelogPath)

# StartsWith rather than a regex: version strings are full of . and - metacharacters.
$start = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i].StartsWith($header)) {
        $start = $i + 1
        break
    }
}

if ($start -lt 0) {
    Write-Error "$ChangelogPath has no '$header' section. Rename [Unreleased] to $targetVersion, date it, then re-tag."
    exit 1
}

# Stop at the next version heading, or at the link definitions that close the file.
$body = @()
for ($i = $start; $i -lt $lines.Count; $i++) {
    if ($lines[$i].StartsWith('## ') -or $lines[$i] -match '^\[[^\]]+\]:\s') {
        break
    }

    $body += $lines[$i]
}

$notes = ($body -join "`n").Trim()

if (-not $notes) {
    Write-Error "The '$header' section in $ChangelogPath is empty. Write the notes, then re-tag."
    exit 1
}

if ($OutputPath) {
    $directory = Split-Path -Parent $OutputPath
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    Set-Content -LiteralPath $OutputPath -Value $notes -Encoding utf8
}

$notes
