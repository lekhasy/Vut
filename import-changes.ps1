#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Applies a patch file to this repo, commits, and pushes.

.PARAMETER PatchFile
    Path to the patch file produced by export-changes.ps1.
    Defaults to Desktop\changes.patch

.PARAMETER CommitMessage
    Commit message for the applied changes.

.EXAMPLE
    .\import-changes.ps1 -CommitMessage "Add Copilot CLI agents"
    .\import-changes.ps1 -PatchFile "C:\tmp\my-changes.patch" -CommitMessage "Add Copilot CLI agents"
#>

param(
    [string]$PatchFile = (Join-Path $HOME "Desktop\changes.patch"),

    [Parameter(Mandatory = $true)]
    [string]$CommitMessage
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Success([string]$msg) { Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Fail([string]$msg) { Write-Host "`n    FAILED: $msg" -ForegroundColor Red }

Set-Location $PSScriptRoot

# ── Validate patch file ───────────────────────────────────────────────────────

Write-Step "Validating patch: $PatchFile"
if (-not (Test-Path $PatchFile)) {
    Write-Fail "Patch file not found: $PatchFile"
    exit 1
}

git apply --check $PatchFile 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Patch does not apply cleanly. Aborting."
    exit 1
}

Write-Success "Patch is valid"

# ── Apply patch ───────────────────────────────────────────────────────────────

Write-Step "Applying patch"
git apply --binary $PatchFile
if ($LASTEXITCODE -ne 0) { Write-Fail "git apply failed"; exit 1 }

Write-Success "Patch applied"

# ── Commit and push ───────────────────────────────────────────────────────────

Write-Step "Committing"
git add --all
git commit -m "$CommitMessage`n`nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
if ($LASTEXITCODE -ne 0) { Write-Fail "git commit failed"; exit 1 }

Write-Success "Committed"

Write-Step "Pushing"
git push
if ($LASTEXITCODE -ne 0) { Write-Fail "git push failed"; exit 1 }

Write-Success "Pushed"

Write-Host "`n✓ Done. Changes applied, committed, and pushed.`n" -ForegroundColor Green
