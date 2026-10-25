# Runs the suite that backs the post's claims. Exits non-zero if any fail.
# Set $env:DOTNET to the full path of an 11.0.100-rc.2 dotnet if it isn't the one on PATH.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host 'dotnet11-async-validation-demo: checking the post''s claims'
Write-Host '  TakenEmailTests         a taken email is a 400 with the field error; Required and'
Write-Host '                          EmailAddress fail without a database lookup; case is ignored'
Write-Host '  RaceTests               two sign-ups held until both passed validation: two rows without'
Write-Host '                          the unique index, a 500 with it, the same 400 once it is caught'
Write-Host '  SharedDbContextTests    two async checks sharing the scoped DbContext throw once queries'
Write-Host '                          take time; a context per check from IDbContextFactory works'
Write-Host '  HowValidationRunsTests  async attributes run at the same time; attribute order; the'
Write-Host '                          IAsyncValidatableObject version; an MVC controller calls IsValid'
Write-Host ''

$dotnet = if ($env:DOTNET) { $env:DOTNET } else { 'dotnet' }
& $dotnet test --logger 'console;verbosity=normal'
exit $LASTEXITCODE
