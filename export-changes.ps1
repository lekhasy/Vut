#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Exports all uncommitted changes in this repo to a patch file on the Desktop,
    then reverts the working directory to match origin.

.PARAMETER OutputFile
    Path for the output patch file. Defaults to Desktop\changes.patch
    NOTE: Must be outside the repo to survive git clean.

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

# ── Check for changes ─────────────────────────────────────────────────────────

Write-Step "Checking for changes"
$status = git status --porcelain
if (-not $status) {
    Write-Fail "No changes detected. Nothing to export."
    exit 1
}

Write-Host "    Changes found:"
$status | ForEach-Object { Write-Host "      $_" }

# ── Stage and export ──────────────────────────────────────────────────────────

Write-Step "Creating patch: $OutputFile"
git add --all
if ($LASTEXITCODE -ne 0) { Write-Fail "git add failed"; exit 1 }

git diff --cached --binary > $OutputFile
if ($LASTEXITCODE -ne 0) { Write-Fail "git diff failed"; exit 1 }

$patchSize = (Get-Item $OutputFile).Length
if ($patchSize -eq 0) {
    Write-Fail "Patch file is empty — nothing was staged."
    git reset HEAD | Out-Null
    exit 1
}

Write-Success "Patch created ($patchSize bytes) at: $OutputFile"

# Unstage — leave working directory intact until patch is confirmed saved
git reset HEAD | Out-Null

# ── Revert this repo ──────────────────────────────────────────────────────────

Write-Step "Reverting this repo to match origin"
git checkout -- . 2>&1 | Out-Null
git clean -fd   2>&1 | Out-Null
git pull
if ($LASTEXITCODE -ne 0) { Write-Fail "git pull failed"; exit 1 }

Write-Success "Reverted and pulled"

Write-Host "`n✓ Done. Patch saved to: $OutputFile" -ForegroundColor Green
Write-Host "  Copy this file to the other repo and run import-changes.ps1`n"
