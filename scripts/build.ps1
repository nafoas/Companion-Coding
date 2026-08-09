#!/usr/bin/env pwsh
# Builds the full solution. Run from the repository root or anywhere; this script
# resolves paths relative to its own location.
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

dotnet restore (Join-Path $repoRoot "CompanionCore.slnx")
dotnet build (Join-Path $repoRoot "CompanionCore.slnx") --no-restore --configuration Release
