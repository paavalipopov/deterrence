# Changelog

## 0.14.0

- Misc is now **Options**. It also holds **Colony size scaling** and **Faction overrides**, moved from Formula, and the new quest, arrival and raider settings.
- Added **Apply to quests** under Options. When it is off, quests and quest sites size their threats and rewards from the game's own threat wealth.
- Added the built-in **Smart** preset. Waster pirates ignore pollution and toxic hazards, insects ignore remains and reputation, and mechanoids ignore hazards.
- Faction profiles now also size infestations (Insect) and mech clusters and crashed ship parts (Mechanoid).
- Remains pass the total value of kept bodies through the hazard growth curve, so large graveyards count less per body. Existing colonies see their remains total recalculated on load. A removed body fades its marginal contribution.
- Added **Arrival frequency** under Options. Raids, and traders and visitors, arrive at vanilla rate × (1 − s (1 − r)^p), where r = Threat / Base for the faction that is coming. Off by default.
- Added **Raider retreat**. Raiders flee after losing L × (1 − s (1 − r)^p) of their group, where L is their faction's usual 40–70%. Off by default.
- Added **Own dead weight** (w): a faction counts the remains of its own members w times when it raids, visits or decides to retreat.
- Bodies recorded before the mod was enabled now count by their status at death. Older bodies infer that status from faction relations when first seen.
- Maddened or scaria animals count only when they are wild.
- The hourly refresh no longer scans every item for prisoner organs or every thing for hives. The smoothed-surface scan uses floor values computed once.
- Each patch class is applied separately, so one missing game method no longer disables the rest.

## 0.13.1

- The buildup line on Curves now shows the long-run linear, power-law or logarithmic trend for the selected growth shape.
- Restored the original still landscape on the cover, with a bare tree, the skull by the grave, and the deer and its crow on the farther hill.

## 0.13.0

- Curves shows total hazard buildup directly under daily gain and gives the two fade definitions larger type.
- Hazards, Reputation and Combined previews have separate controls. The hive example now has arrival and clearance days and models clustered reproduction.
- Faction selectors show world names followed by their faction type. Raid overrides still apply by type, and burial choices still apply to individual factions.
- Updated the Workshop woodcut with wind, drizzle, carrion and two crows.

## 0.12.1

- Put daily gain and remaining balance first on the Curves tab. Growth and fading now share balance and time notation. Formula notes have a divider or wrap onto a separate line.
- Replaced the Workshop cover with a black-and-white woodcut.

## 0.12.0

- Renamed the mod folder, assembly, and package ID to Deterrence. Enable it once after upgrading. Existing colony data, burial rules, settings, and presets carry over.

## 0.11.1

- Reorganized the Curves tab around slowdown time, daily hazard gain, fade duration, and remaining balance. Equations now define their symbols before the related sliders. Gameplay calculations and saved settings are unchanged.
- Added a Workshop cover and updated the mod description.

## 0.11.0

- Added three per-storage humanlike corpse filters for individual status, current faction relations and named factions. The filters default to Any. Explicit grave assignments take priority.

## 0.10.1

- The Meters window shows a pie chart of the selected map’s Remains, Reputation, and Hazards. Hover a slice or legend entry for its share of total deterrence.

## 0.10.0

- Unified hazard growth with exposure or buildup-rate timing and a continuous shape control.
- Added hazard-history fade duration in point-days, alongside starting-balance duration. New fades use one smooth cubic.
- Updated the live ledger, meter rates, Summary, previews, presets and faction overrides to use the new controls. Older fades finish as saved.
- Hover a setting label, checkbox, slider or equation to read its help. The biome controls also explain settlement weight and show it for the current map.
- The factory defaults are 0 for smoothed surfaces, 0.2 for the island multiplier, and 180 world tiles for the island limit.
- Rasterize equations at their physical display size, with coverage antialiasing and pixel-aligned placement at each UI scale.

## 0.9.3

- The bottom-bar Meters window shows the active threat formula and both terrain factors with typeset subscripts beside their live percentages.
- Equations throughout the menus use the main formula's size where space permits. Sharper glyphs and width-based fitting preserve the notation in narrow panels.
- Renamed the Reputation source group to **Prisoner treatment**. The sources cover surgery and executions.
- Extreme outdoor heat and cold now each default to 1,000 deterrence per day per 10 °C beyond the threshold. Existing custom rates are preserved.

## 0.9.0

- Island qualification defaults to 10 tiles with an adjustable limit, its own enable switch, and an optional sole-settlement check.
- Formula reflects active switches, plots both terrain factors, and gives primary settings stronger typography. Options groups colony scaling, faction overrides, and meter access.
- Restored a compact Deterrence shield beside World.
- Renamed Compare to Summary and added the current formula. Curves keeps the plot visible beside the settings.
- Added named presets with save, load, rename, overwrite, and delete actions.

## 0.8.1

- Added the island multiplier on Formula and extended biome strength to 3. Faction profiles can override both.
- Matched the settings tab order to Formula help.
- Hid colony scaling and sample colonist inputs behind one Advanced checkbox. Basic Compare uses one example column.
- Hazards lists Acid Rain and Toxic Rain only when loaded mods define those conditions.

## 0.8.0

- Added opt-in faction settings on every tab. Individual fields inherit Global until customized. Reset to Global clears that faction's edits across all tabs.
- Ordinary enemy raids use the selected faction's resolved Formula, source, Curves, and Misc settings after faction selection. The original shared budget still governs eligibility. Explicit quest and event budgets keep their assigned points.
- Faction balances start from the current Global balance and evolve independently where customized. Global and faction ledgers share map scans. Existing fade clocks carry over.
- Added checks for field inheritance, balance isolation, budget provenance, and preserving storyteller multipliers. See TESTING-0.8.md for the live review.

## 0.7.0

- Reviewed all settings pages using Formula's scope/formula/example pattern. Gameplay calculations, saved keys, and default weights are preserved.
- Grouped sources by mechanic; primary disabled sources stay visible. More conditions reveals other unconfigured conditions, with searchable names/groups and visible availability requirements.
- Made body/organ switches explicit and independent. Added source lifecycle/formula text, standard examples, precise units and reset labels, direct organ inputs, and consistent colony/scenario foldouts.
- Split Curves into Hazard growth and Fading, clarified K and duration calibration, added three plot modes with optional example inputs.
- Replaced mixed-unit Compare weights with real-model standard examples in deterrence units at several colony sizes. Off/unavailable states remain visible; row links open their source.
- Added direct numeric controls that preserve valid custom values until edited. Reset amount/scaling preserves the enable switch. Maintained single-column scrolling and added a page-by-page review checklist.
- Build, 64 model checks, and expanded compiled integration checks pass. Native in-game review remains in MENU-REVIEW-0.7.md.

## 0.6.1

- Formula now reads: Adjusted threat wealth = max(0, Game threat wealth - Deterrence) x biome factor. Separate deterrence and biome switches work independently; tracking continues while subtraction is off.
- Explained biome strength as an exponent and added a live factor curve plus current-map example. Formula introduces the main settings categories.
- Moved smoothed-surface wealth to Misc, where it adjusts Game threat wealth independently of the formula switches. History uses the same names and records the surface correction with new samples.
- Removed named heat-wave/cold-snap contributions and settings, including their old balances. Actual outdoor temperature remains the only heat/cold source.
- Hid the bottom-bar button. Open meters is near the top of Formula; it closes settings dialogs and opens the existing window, which now has a close X.
- Preserved old disabled-master behavior on upgrade with a one-time migration. Build, 64 model checks, and compiled integration checks pass; in-game checks are in TESTING-0.6.1.md.

## 0.6.0

- Renamed the public mod/total metric to Deterrence. Three visible meters: Remains, Reputation, Hazards. Saved identifiers, folder, package ID, and assembly remain compatible.
- Expand meters into categories and individual bodies/stacks/acts. Reputation retains 40 recent acts plus Earlier reputation without duplicating credit. Existing fade rules remain unchanged.
- Added map locator arrows for physical contributors; buried bodies point to their grave/sarcophagus.
- Fixed blank settings panels: scrolling controls stay in one column. Added direct amount fields, minus/plus, fractions, and animal Off/Low/Standard presets. New animal-body default 1 per combat power; saved amounts and death weights are preserved.
- Added optional Ideology reputation boost ritual reward for positive outcomes, default 5,000, configurable under Reputation.
- Added smoothed-surface threat-wealth multiplier: 1 vanilla (default), 0 removes the counted contribution. Asset values stay unchanged; default skips the extra scan.
- Simplified labels/help and six settings tabs; optional population, individual preview, weather, and custom fade controls use foldouts. Curves keeps its live plot.
- Added cached/clipped meter drawing, an illustrated renamed guide, vocabulary, and renamed updater (old updater also works). Kept the original full first draft unchanged.
- Build, 62 model checks, and compiled integration checks pass, including human-gear preview, ritual DLC gate, disappearance reconciliation, and single-column layout regression. Native game smoke tests remain in TESTING-0.6.md.

## 0.5.1

- New locally harvested prisoner organs maintain current market value times a configurable 0-5 multiplier (default 0.75), independently of body/violence switches.
- Added saved item provenance; imported/existing untagged organs do not qualify. Tagged and ordinary stock stay separate during stacking; splits copy provenance.
- Counts kept stock on home maps, including colonist inventory and containers. Sale, implantation, destruction, or removal ends stock credit on refresh; the harvest violence meter fades separately. Stock transfers do not create loss tails.
- Added Remains controls, help, and an example calculation. Updated guide and in-game checklist.
- 50 model checks plus compiled migration/graph/provenance checks. Existing updater works unchanged.

## 0.5.0

- Removed solar flare, eclipse, psychic drone, poor-soil, and swamp accumulation, including identifiable legacy credits. Illness/volcanic winter stay excluded.
- Added one shared violence meter per map. Hostile deaths and prisoner acts add to remaining credit and renew its fade; zero/disabled acts cannot renew it.
- Successful prisoner organ harvest, failed removal surgery, non-organ amputation, and standard execution have separate configurable weights. One operation earns one outcome.
- Split hostile-death bumps from maintained bodies. Only death bumps use human gear; bodies use combat power alone, with no rot weighting. Legacy combat coefficients split once between the two rules. No retroactive bumps.
- Environmental incidents share one typed meter per map; repeat fallout resumes surviving history. Simultaneous duplicates do not multiply gains.
- Actual outdoor temperature is enabled by default. Flat named heat/cold contributions require optional stacking. Removed discovery/fog gates for bodies and hives; pollution is map-wide.
- Added cubic/linear fade choice, configurable duration exponent, and a live curve laboratory with two environmental sources, repeated violence bumps, and total credit. Old fades retain saved settings.
- Added surgery-outcome, renewal, migration, and plot checks. 48 model checks and compiled integration checks pass; live UI/operations still need in-game verification.

## 0.4.0

- Reframed the design around outside deterrence and regional appeal. Colony hardship alone is not grounds for a discount.
- Removed illness observation, source settings, and the Illnesses menu tab. Old active and recovering illness balances are discarded on load/refresh, with no replacement penalty.
- Added a shorter illustrated guide in self-contained HTML and editable Markdown. Two real-model examples show independent accumulation/fading and the resulting storyteller wealth.
- Preserved the full original description unchanged as the first draft. Added a separate design review; remaining rules and defaults are unchanged.
- Added migration/menu checks and a reproducible example-data exporter. All 35 calculation/lifecycle/menu checks pass.

## 0.3.1

- Added a player-facing description organized from the global formula through each contribution mechanic.
- Settings now follow the same order: Formula, Habitat, Remains, Events, Illnesses, Curves, Compare.
- Moved temperature/fertility thresholds beside their sources and rot options beside remains.
- Moved shared accumulation/recovery controls into Curves. Gameplay calculations/defaults are unchanged.

## 0.3.0

- Human remains include configurable original weapons/apparel value, captured before downing/direct death and frozen at death.
- Downing snapshots are saved; repeated death replaces the same pawn's entry.
- Added ground-pollution exposure and hostile-hive infestation pressure.
- Added opt-in extreme outdoor heat/cold, poor natural ground, and swamp coverage.
- Excluded volcanic winter; heat waves and cold snaps remain supported.
- Added optional biome threat-wealth multiplier, default square root of vanilla settlement-selection weight. No passive biome credit or settlement-generation changes.
- Illness tracking and rot-stage reduction are off by default.
- Added clickable help, native growth/recovery curves, and saved per-map wealth/antiwealth history with explicit timestamps.

## 0.2.0

- Added General, Conditions, Remains, Illnesses, and Compare settings pages.
- Independent enable/amount/population controls for each event and disease, and separate human/mechanoid/animal remains.
- Fixed, square-root, linear, and custom population exponents with configurable reference population.
- Live 1/5/10/20-colonist comparisons and 1/10-day accumulation/recovery previews.
- Installed game conditions are discoverable and can be enabled individually; unknown conditions default off.
- Existing 0.1 settings migrate into the corresponding independent rules; population scaling defaults off.
- Rotting and dessicated preservation factors are configurable.
- Existing saved ledger entries retain their identity and earned credit.

## 0.1.0

- Initial RimWorld 1.6 implementation.
- Unbounded logarithmic or square-root accumulation from ongoing hardships.
- Hostile remains provide a preservation-dependent fixed credit.
- Cubic recovery with square-root-scaled duration.
- Saved ledger, source identity tracking, settings, and Antiwealth tab.
- Data-driven extension point for additional game conditions.
