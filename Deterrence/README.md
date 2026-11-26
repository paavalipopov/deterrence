# Deterrence 0.13.1

Danger and its lingering reputation reduce the wealth used to size outside threats. Actual asset prices stay unchanged.

    Threat = max(0, Base - Deterrence) × k_biome × k_island

Read the [illustrated guide](Deterrence-Guide.html), [editable guide](Deterrence-Guide.md), or [vocabulary](VOCABULARY.md). The [original first draft](docs/Antiwealth-Description-First-Draft.md) is unchanged and historical.

## Three meters

| Meter | Contribution |
| --- | --- |
| Remains | Hostile bodies, including burials, maintain combat-power value; each removed body fades separately. Kept locally harvested prisoner organs maintain current market value x 0.75 by default, with no extra stock-loss tail. |
| Reputation | Hostile deaths, prisoner surgeries and executions, and an optional positive Ideology ritual reward add one-time gains to one shared fading balance. Each positive gain renews its fade. |
| Hazards | Pollution, toxic hazards, hostile hives, and outdoor extremes accumulate separately with diminishing returns, then fade. Repeated fallout resumes its surviving typed balance. |

The pie chart shows each meter’s share of the current map’s total deterrence. Hover a slice or its legend entry to see the amount. Expand meters into contributors. Physical rows have map arrows; buried bodies point to their grave. Reputation shows the latest 40 acts plus Earlier reputation; attribution never adds another contribution.

Formula shows the enabled deterrence and terrain terms. **Biome strength** is the exponent in `k_biome = settlement weight ^ strength` and ranges from 0 to 3. `k_island` ranges from 0 to 1 and defaults to 0.2. A qualifying island is a water-enclosed landmass within the adjustable tile limit, which defaults to 180 world tiles. **Require sole settlement** is on by default and counts all factions. The terrain preview plots both factors as percentages. Smoothed-surface wealth is under Misc. Its default multiplier of 0 excludes smoothed floors and walls from Base before the formula.

Tabs follow the Formula help in this order: **Formula, Remains, Reputation, Hazards, Curves, Summary, Misc, Presets**. **Formula > Options > Colony size scaling** reveals scaling controls, sample colonist inputs, and extra Summary columns. Existing colony scaling applies with the controls hidden. Curves keeps the live plot visible while you scroll the controls. Hazards lists installed conditions. Acid Rain and Toxic Rain appear when a mod supplies their definitions. Toxic Fallout is a separate base-game event.

A qualifying animal death adds **3 reputation per combat power** by default. Its body contributes **1 deterrence per combat power** while kept or buried. Royalty's psychic droner and toxic spewer each start at **1,000 per day** while their regional condition affects the colony. Each device type has its own hazard balance.

With Ideology, choose **reputation boost** as a ritual's normal bonus. Positive outcomes award the configurable default 5,000 to Reputation. Other rituals do not award it automatically.

Illness, solar flares, eclipses, psychic drones, volcanic winter, poor soil, and swamp terrain earn nothing. Extreme heat and cold each start at 1,000 deterrence per day for every 10 °C beyond their respective outdoor thresholds. Named heat waves and cold snaps grant no separate benefit.

## Corpse storage

For burial and corpse hauling, open the destination’s **Storage → Corpses → Humanlike corpses**. The three expandable filters are **Allowed status at death**, **Allowed faction status**, and **Allowed factions**. Each starts on **Any**. Use **None** to exclude every humanlike corpse from ordinary storage. Selecting more than one entry allows any of those entries within the branch. A corpse must pass each branch and the vanilla storage rules.

Individual status is saved at death. A raider can be Hostile and a prisoner from the same faction Neutral. Corpses from older saves get a one-time estimate from their faction. Diplomatic status uses the current relationship with the player. Choose **Allowed factions → Factionless** and leave faction status on **Any** to accept factionless corpses. Assigning a grave or sarcophagus to a specific pawn reserves it for that pawn and overrides these filters. Storage copy/paste and linked storage use the same rules as their destination.

## Faction overrides

Enable **Faction overrides** under **Formula > Options**, then select a faction type from **Settings for**. In a loaded colony the selector shows world names followed by the shared type in parentheses. Each faction inherits Global values until you edit a control. This includes switches, individual source weights, growth and fade settings, the biome and island factors, and the surface multiplier. Summary follows the selected scope. **Reset to Global** clears all overrides for that faction. **Apply & save** refreshes the colony.

Overrides size ordinary enemy raids after faction selection. Global still supplies the starting budget used to select the faction, as well as the shared meters and history. Scripted raid budgets keep their assigned points. Faction balances begin with the current Global balances when a source or curve is first customized. From then on they use their own weights and timing as new events occur.

Open meters from the shield beside **World**, or use **Formula > Options > Open meters**.

## Curves

**Hazard growth** uses one curve with two timing options. **By total exposure** reaches a given slowdown at a fixed raw exposure; stronger hazards reach it sooner. **By daily rate** gives stronger hazards more time at their starting pace. Its reference is 2,000 points/day. With a 10-day slowdown setting and logarithmic growth, a 1,000/day hazard reaches half speed in 7.1 days and a 4,000/day hazard in 14.1 days. **Growth shape** ranges from 0 (linear) through 0.5 (square root) to 1 (logarithmic).

**Fading** uses the same smooth cubic for every new fade. **Starting balance** sets duration from ending points. **Hazard history** uses the integral of an ongoing hazard's active balance in point-days. A new active episode starts a fresh area at its surviving balance. Instant Reputation gains and removed bodies use the balance calibration. At the reference value a fade takes 10 days by default. At exponent 0 every fade takes the reference duration; at 0.5, four times the balance or area gives twice the duration.

H is hazard buildup in points. The line beneath daily gain shows the long-run trend for a steady hazard. At p = 0, H = Rt. Between 0 and 1, H grows like (t/T)^(1 − p). At p = 1, it grows like ln(t/T). The graph gives the exact point totals. During growth, t counts active days and T sets the slowdown time. During fading, H₀ is the starting balance, t counts days since fading began, and T is the time to zero. The fraction elapsed is x = t/T. Each mode uses D for its own reference duration.

Existing settings and presets keep their exposure scale, growth shape and reference duration. A fade in progress completes on its saved clock and shape. Hazards already active in older saves use starting balance for their first fade because their past area was never recorded. The buildup-rate clock starts tracking from the upgrade. **Preview inputs** change only the example graph. Hazards and Combined let you choose the day the first hive appears and the day the cluster is cleared. A single hive starts the example. Successive generations double at the midpoint of the vanilla reproduction interval, slowed by crowding and capped at 30. Reputation has its own timeline.

## Presets

**Defaults (built-in)** restores the current factory settings, including source weights, and clears faction overrides. Save current settings under a new name to keep a customized copy.

**Save current settings** takes a named snapshot of Global settings and faction overrides. Select a saved preset on the right to see its formula and description, load it, rename it, or delete it. Colony balances and history stay with the current save.

**Open folder** opens `Config/Deterrence/Presets` inside RimWorld's user data folder. Save a preset to get an editable XML example. Copy or edit these files and click **Refresh files** to reload the list. For a new preset, give the copy a unique filename and edit its `Name` element. The presets from 0.9.0 move to this folder when you first open the tab.

## Install / update

Requires RimWorld 1.6 and Harmony. Core mechanics need no DLC; pollution uses Biotech and the ritual reward uses Ideology.

Close RimWorld and run **Update-Deterrence.bat** beside the Deterrence folder. Use `--check` for a read-only preflight. The updater backs up the previous local copy outside Mods and installs the compiled mod.

For manual installation, move the old **Antiwealth** folder outside Mods and copy **Deterrence** into Mods. Enable **Deterrence** after Harmony and load the colony with the current mod list. Version 0.12.0 uses package ID `pavel.deterrence`. Older save data and settings are read automatically. Named presets stay in the same Deterrence presets folder.

## Verification

Build.ps1 compiles against installed RimWorld and Harmony assemblies. Test.ps1 checks island geography and factor math. Tools/Test-Integration.ps1 also checks the compact button definition, preset isolation, and storyteller budget handling. See [0.9.0 checks](TESTING-0.9.md) for the in-game pass.

The [corpse storage checklist](TESTING-0.11.0.md) includes the disposable-colony test for hauling, storage groups, grave assignments, and save/load.

[Release changes](CHANGELOG.md), [design decisions](Deterrence-Design-Review.md), and earlier [organ checks](TESTING-0.5.1.md) are included. Generate chart data using the Tests project's --export-example and --export-violence options, then run Tools/Build-Guide.py. Charts use the actual C# calculation functions.
