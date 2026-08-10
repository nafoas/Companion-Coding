#!/usr/bin/env pwsh
# Runs the automated test suite. Requires no API key, network access beyond package
# restore, capture hardware, or a live desktop session.
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

dotnet restore (Join-Path $repoRoot "CompanionCore.slnx") --locked-mode
dotnet test (Join-Path $repoRoot "CompanionCore.slnx") --configuration Release
