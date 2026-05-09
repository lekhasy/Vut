#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Exports committed code changes on the current branch (compared to local main)
    to a diff file. Only the diff is included — no commit messages. Staged or
    unstaged working-tree changes are ignored. The local repo is left untouched.

.PARAMETER OutputFile
    Path for the output diff file. Defaults to changes.patch next to the repo.

.EXAMPLE
    .\export-changes.ps1
    .\export-changes.ps1 -OutputFile "C:\tmp\my-changes.patch"
#>

param(
    [string]$OutputFile = (Join-Path (Split-Path $PSScriptRoot -Parent) "changes.patch")
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Success([string]$msg) { Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Fail([string]$msg) { Write-Host "`n    FAILED: $msg" -ForegroundColor Red }

Set-Location $PSScriptRoot

# ── Validate branch ───────────────────────────────────────────────────────────

Write-Step "Checking current branch"
$currentBranch = git rev-parse --abbrev-ref HEAD
if ($LASTEXITCODE -ne 0) { Write-Fail "Not a git repository"; exit 1 }

if ($currentBranch -eq "main") {
    Write-Fail "Already on 'main'. Switch to a feature branch first."
    exit 1
}

Write-Success "Current branch: $currentBranch"

# ── Check for committed differences against main ─────────────────────────────

Write-Step "Comparing '$currentBranch' against local 'main'"
$mergeBase = git merge-base main HEAD
if ($LASTEXITCODE -ne 0) { Write-Fail "Could not find common ancestor with 'main'. Does 'main' branch exist?"; exit 1 }

$diffStat = git diff --stat $mergeBase HEAD
if (-not $diffStat) {
    Write-Fail "No committed changes found between 'main' and '$currentBranch'."
    exit 1
}

Write-Host "    Changed files:"
$diffStat | ForEach-Object { Write-Host "      $_" }

# ── Export diff ───────────────────────────────────────────────────────────────

Write-Step "Creating diff: $OutputFile"
git diff --binary --output=$OutputFile $mergeBase HEAD
if ($LASTEXITCODE -ne 0) { Write-Fail "git diff failed"; exit 1 }

$patchSize = (Get-Item $OutputFile).Length
if ($patchSize -eq 0) {
    Write-Fail "Diff file is empty."
    exit 1
}

Write-Success "Diff created ($patchSize bytes) at: $OutputFile"

Write-Host "`n✓ Done. Diff saved to: $OutputFile" -ForegroundColor Green
Write-Host "  Copy this file to the other repo and run import-changes.ps1`n"
