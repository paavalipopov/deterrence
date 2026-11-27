# Used by the Build workflow: commit the rebuilt assembly if it changed and the branch hasn't moved on.
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..\..')

git add Deterrence/Assemblies/Deterrence.dll
git diff --cached --quiet
if ($LASTEXITCODE -eq 0) { Write-Output 'Assembly unchanged.'; exit 0 }

# A newer push gets its own run, which commits its own build.
git fetch -q origin next-version
if ((git rev-parse origin/next-version) -ne $env:GITHUB_SHA) { Write-Output 'Branch moved on; the newer run will commit.'; exit 0 }

git -c user.name='github-actions[bot]' -c user.email='41898282+github-actions[bot]@users.noreply.github.com' commit -q -m 'Rebuild assembly'
if ($LASTEXITCODE -ne 0) { throw 'Commit failed.' }
git push -q origin HEAD:next-version
if ($LASTEXITCODE -ne 0) { throw 'Push failed.' }
Write-Output 'Committed the rebuilt assembly.'
