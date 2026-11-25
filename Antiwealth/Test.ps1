$ErrorActionPreference = 'Stop'
& dotnet run --project (Join-Path $PSScriptRoot 'Tests\Antiwealth.Tests.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Tests failed ($LASTEXITCODE)." }
