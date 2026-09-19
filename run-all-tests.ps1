[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projects = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Recurse -File | Where-Object { $_.Extension -in '.vbproj', '.csproj' } | Sort-Object FullName
if ($projects.Count -eq 0) { throw 'No test programs found.' }
$failed = @()
foreach ($project in $projects) {
    Write-Host "Running $($project.Name)..."
    & dotnet run --project $project.FullName --configuration Release
    if ($LASTEXITCODE -ne 0) { $failed += $project.Name }
}
if ($failed.Count -gt 0) { throw "Release blocked. Failed tests: $($failed -join ', ')" }
Write-Host "PASS: all $($projects.Count) test programs."
