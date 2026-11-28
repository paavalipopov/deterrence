# Deterrence

RimWorld 1.6 mod that lowers the wealth the storyteller uses to size threats. Three meters count toward deterrence: Remains, Reputation and Hazards. Harmony is the only dependency.

## Layout

- `Deterrence/` is the mod folder: `About`, `Assemblies`, `Defs`, `Textures`, `Source` (C#), `Tests`, `Tools`.
- `Deterrence/Source` compiles into `Assemblies/Deterrence.dll` with `Build.ps1`. There is no csproj for the mod.
- `Deterrence/Tests` is a .NET 10 console project. It compiles only the game-free math files from `Source`.
- `Deterrence/Tools` holds the integration, in-game and inspection scripts.

## Test machine (C:\RimTest)

- `game`: RimWorld 1.6.4871 with its DLCs. Every enabled mod is a local mod in `game\Mods\<Workshop ID>`.
- `game\Mods\Deterrence`: a junction to `repo\Deterrence`, made by `Tools\Link-DevelopmentMod.ps1`.
- `profile`: the save-data folder. Start the game with `Launch-RimWorld.cmd`, which passes `-savedatafolder` pointing here.
- `repo`: this checkout. `runs`: disposable game copies and test profiles. `installers`: the Claude Desktop package.
- The test account `rimtest` is a standard user. Run scripts with `powershell -ExecutionPolicy Bypass -File <script>`.

## Build and test

Run these from `C:\RimTest\repo\Deterrence`. `Build.ps1` and `Tools\Inspect-Api.ps1` use the `RIMWORLD_GAME` environment variable when set, otherwise find `C:\RimTest\game` beside the checkout. Harmony comes from `game\Mods\2009463077`.

- Build: `.\Build.ps1`
- Unit tests: `.\Test.ps1`
- Integration tests against the game assemblies, in PowerShell 7: `pwsh -ExecutionPolicy Bypass -File .\Tools\Test-Integration.ps1`. Windows PowerShell 5.1 can't load the game's `Queue<T>`.
- Headless in-game storage test. It installs test mods into the game it is given, so use a fresh copy and a new profile folder for each run:
    ```
    robocopy C:\RimTest\game C:\RimTest\runs\game /E /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
    robocopy C:\RimTest\repo\Deterrence C:\RimTest\runs\game\Mods\Deterrence /E /R:1 /W:1 /XD Source Tests Tools docs /NFL /NDL /NJH /NJS /NP
    .\Tools\Test-CorpseStorageGame.ps1 -TestGame C:\RimTest\runs\game -Profile C:\RimTest\runs\profile-<name> -AdaptiveStorageMod C:\RimTest\game\Mods\3033901359
    ```
- On the main PC the same scripts use the Steam install and the Workshop copy of Harmony.

## Automatic build

- `.github/workflows/build.yml` runs on a self-hosted GitHub Actions runner on the test machine, as the `rimtest` account, for every push to `next-version` that touches more than docs and textures. It can also be started by hand from the Actions tab.
- It builds, runs `Test.ps1` and `Tools\Test-Integration.ps1`, and on success commits **Rebuild assembly** as `github-actions[bot]` through `Tools\Commit-Assembly.ps1`. A failed run commits nothing; its log is on the Actions tab.
- So don't commit `Assemblies\Deterrence.dll` by hand. Pull after a push to get the rebuilt assembly.
- Pull requests never run on the runner. The repository requires approval for every workflow from external contributors; keep it that way.

## Current state

- Branch `next-version` holds all work after 0.13.1 and is pushed. GitHub's `main` is still 0.13.1.
- Committed on `next-version`:
    - Per-class Harmony patching, status-at-death body discovery, wild-only maddened animals, Apply to quests, faster hourly scans.
    - The Misc tab is renamed Options and holds colony size scaling, faction overrides, smoothed surfaces, quests, arrivals and raiders.
    - Built-in Smart preset: waster pirates ignore pollution and toxic hazards, insects ignore remains and reputation, mechanoids ignore hazards.
    - Faction profiles also size infestations (Insect) and mech clusters and crashed ship parts (Mechanoid).
    - Remains pass the total of kept bodies through the hazard growth curve. A removed body fades its marginal contribution.
    - Options define r = Threat / Base per arriving faction (profile plus Remains > Weight of own dead). Wanderers use the colony's r.
    - Deter outsider arrivals: rate = vanilla x (1 - s (1 - r)^p), with s for raids and s for traders and visitors, and a shared p.
    - Raiders retreat sooner: retreat threshold = vanilla threshold x (1 - s (1 - r)^p), where the vanilla threshold is 40-70%.
    - Test-bed support in `Build.ps1` and `Tools`.
- `Assemblies\Deterrence.dll` is current, and the Build workflow passes the unit and integration tests.
- Not yet verified: the headless storage test and the in-game checks below.
- In-game checks:
    - Load the Smart preset, then spawn an infestation and a waster pirate raid in Dev mode. Their points should differ from Global.
    - With Dev mode on and Deter outsider arrivals enabled, the log reports each skipped raid and arrival.
    - With Raiders retreat sooner enabled and heavy deterrence, a raid should flee after fewer losses than usual.
    - Harvest a prisoner organ, save and reload, and send it out and back with a caravan. It should keep counting.
    - The surface discount should match the value before the precomputed floor table.
- Open work:
    - Calibrate Remains and Hazards defaults on their shared growth curve.
    - Decide whether small threats (single maddened animals) should use unadjusted points.
    - When merging into `main`, trim this section.
- The full design and its open questions are in `claude/next-version-plan.md`, in the Deterrence project on claude.ai.

## Game facts checked in 1.6.4871

- Trader caravans, visitor groups and traveler groups pick their faction in `IncidentWorker_NeutralGroup.TryResolveParms`. WandererJoin and WandererJoinAbasia have no faction.
- Raid retreat: `Lord.SetJob` adds `Trigger_FractionPawnsLost` with the faction's `attackersDownPercentageRangeForAutoFlee` (0.4-0.7 by default). Factions with `autoFlee` false never retreat.
- Raid build order in `IncidentWorker_Raid.TryGenerateRaidInfo`: points, faction, strategy, arrival mode, then `AdjustedRaidPoints`, which floors points at the strategy minimum x 1.05.
- `Storyteller.TryFire(FiringIncident fi, bool queued)` runs every storyteller incident.
- Quests take points from `World.PlayerWealthForStoryteller`, the sum over all player maps.

## Conventions

- Keep changes targeted, adding as little code as possible. Ask before larger rewrites.
- Comments are short phrases with verbs, saying what a block does. Add the why only when it isn't obvious, in at most two lines.
- Leave a blank line between steps inside a function.
- Review notes may appear as TODO comments. Answer their questions before changing code.
- Git: inspect with `git status`, `git diff` and `git log` only. Don't run commands that write (add, commit, push, pull, checkout, stash, reset). Suggest commits as commands instead, with explicit paths and messages of five words or fewer.
