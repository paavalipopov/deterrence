# Antiwealth 0.5 checklist

Close RimWorld, run Update-Antiwealth.bat, and restart. Use a copy of your save with Development mode enabled. Bumps apply immediately; ongoing sources and body removal update hourly or with Apply & save. Advance the simulation rather than just changing its clock.

| Done | Test | Expected |
| --- | --- | --- |
| [ ] | Startup and old save | No new patch/load errors. Existing supported balances survive. Removed-source credits disappear. Past history is not rewritten. |
| [ ] | Removed sources | No illness, solar flare, eclipse, psychic drone, poor-soil, or swamp rule. Those events/terrain do not add credit. Volcanic winter stays excluded. |
| [ ] | Armed hostile death | One immediate violence bump uses combat power and captured human gear. A separate body entry uses combat power alone. |
| [ ] | Down, strip, then kill | Human death bump uses gear captured before downing. Saving/reloading while downed retains the snapshot. |
| [ ] | Body switch | Turn maintained bodies off: their existing credit starts fading; later deaths can still add violence. Turn reputation off: new bumps stop while bodies can still count. |
| [ ] | Body removal and rot | Fresh, rotting, and dessicated bodies maintain equal credit. Destroying a body starts its own fade; it does not renew violence. |
| [ ] | Prisoner execution | A completed standard execution adds its configured fixed weight to violence. It does not also add ordinary hostile-death credit. |
| [ ] | Successful organ harvest | One fixed harvest bump. A lethal harvest still earns the harvest weight, without an extra execution/death bump. |
| [ ] | Failed removal surgery | A failed attempt earns only the configured failed-removal weight (default 333), including failures that damage/remove the part. No harvest/amputation bump is added on top. |
| [ ] | Amputation without harvested organ | A successful removal earns the amputation weight (default 500), once. Unrelated medical treatments and operations that remove nothing earn no surgery credit. |
| [ ] | Shared violence | Mix deaths, harvests, amputations, and executions. They appear as one reputation row. Each adds to its remaining balance and restarts its fade. Zero/disabled sources cannot refresh it. |
| [ ] | Repeat fallout | End fallout, then start another before its credit reaches zero. It resumes the same typed meter and diminishing-return history. Different environmental types remain separate. |
| [ ] | Temperature | Outdoor extremes build scaled credit, including those caused by named events. Flat named heat/cold credit adds nothing by default. Optional stacking on Formula enables it. |
| [ ] | No information model | Bodies in fog/storage and map-wide pollution count without scouts or discovery checks. No attacker-specific exceptions apply. |
| [ ] | Live laboratory | Open Curves. Change rise function/slowdown, fade shape, reference timing, duration exponent, and example bump interval/count. Four curves update immediately and show renewed violence tails. |
| [ ] | Saved fades | Start a fade, change curve settings, reload. That existing fade retains its saved shape/duration. A later violence bump renews that meter with the new controls. |
| [ ] | Actual wealth | Prices and economic wealth stay normal. Adjusted wealth matches max(0, ordinary wealth input - total credit) times biome multiplier. |

The surgery/execution hooks cover the installed vanilla removal-surgery and execution utilities, including subclasses that use those paths. Mods that replace these flows need a separate compatibility check. Earlier condition instances can retain separate legacy tails if multiple old entries cannot merge without duplication; new incidents share typed meters.

Automated validation: Test.ps1 runs 48 model tests. Tools/Test-Integration.ps1 checks compiled settings/ledger migration, optional named-temperature behavior, actual game curve objects, and installed patch targets. These do not replace testing the live Unity menu and surgery/execution flows.
