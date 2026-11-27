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

Run these from `C:\RimTest\repo\Deterrence`. `Build.ps1` and `Tools\Inspect-Api.ps1` find `C:\RimTest\game` automatically, and Harmony in `game\Mods\2009463077`.

- Build: `.\Build.ps1`
- Unit tests: `.\Test.ps1`
- Integration tests against the game assemblies: `.\Tools\Test-Integration.ps1`
- Headless in-game storage test. It installs test mods into the game it is given, so use a fresh copy and a new profile folder for each run:
    ```
    robocopy C:\RimTest\game C:\RimTest\runs\game /E /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
    robocopy C:\RimTest\repo\Deterrence C:\RimTest\runs\game\Mods\Deterrence /E /R:1 /W:1 /XD Source Tests Tools docs /NFL /NDL /NJH /NJS /NP
    .\Tools\Test-CorpseStorageGame.ps1 -TestGame C:\RimTest\runs\game -Profile C:\RimTest\runs\profile-<name> -AdaptiveStorageMod C:\RimTest\game\Mods\3033901359
    ```
- On the main PC the same scripts use the Steam install and the Workshop copy of Harmony.

## Current state

- Committed: per-class Harmony patching, status-at-death body discovery, wild-only maddened animals, Apply to quests, faster hourly scans, docs.
- Not committed, and not yet built or tested. `Assemblies\Deterrence.dll` predates these changes until the next build:
    - The Misc tab is renamed Effects: smoothed surfaces, quests, arrivals and raiders.
    - Built-in Smart preset: waster pirates ignore pollution and toxic hazards, insects ignore remains and reputation, mechanoids ignore hazards.
    - Faction profiles also size infestations (Insect) and mech clusters and crashed ship parts (Mechanoid).
    - Remains pass the total of kept bodies through the hazard growth curve. A removed body fades its marginal contribution.
    - Turn away arrivals: traders, visitors, travelers and wanderers are skipped with chance (1 - r)^p, where r = Threat / Base.
    - Raid skip: human enemy raids are called off with chance s x (1 - r)^p, using the raiding faction's r.
    - Raiders retreat after fewer losses as Reputation approaches the reference balance.
    - A raiding faction's own dead can weigh more in Remains.
- `Tools\Link-DevelopmentMod.ps1` is not tracked by git yet.
- The full design and its open questions are in `claude/next-version-plan.md`, in the Deterrence project on claude.ai.

## Game facts checked in 1.6.4871

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
