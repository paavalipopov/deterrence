# Deterrence 0.9.0 design decisions

The model combines current danger, reputation for danger, and regional appeal. Colony hardship alone earns nothing. The public mod name and total metric are **Deterrence**; its three meters are **Remains, Reputation, Hazards**. The mod uses pavel.deterrence. Legacy save keys and the ritual reward defName still resolve data written by Antiwealth.

## Three meters, different lifecycles

Remains groups human, mechanoid, and hostile/manhunter animal bodies with locally harvested prisoner organs. Bodies independently maintain combat-power-only value, including proper burials; removal starts that body's fade. Gear and rot do not affect maintained bodies. Organs maintain current tagged stock value (default 0.75x, adjustable 0-5), ending immediately on refresh after sale/use/removal. Organ loss does not create a second tail. Bought and older untagged stock do not count.

Reputation merges violence and atrocities into one balance per home map. A positive gain adds to the current remainder and renews one fade. It never restores lost value. Disabled/zero gains do not renew anything. The latest 40 acts and an Earlier reputation aggregate show remaining shares; they cannot duplicate the actual balance. Old saves preserve unrecorded totals as the aggregate.

Hostile deaths use combat power; original human weapons/apparel also count by default. Prisoner acts have separate weights: harvest 1,000; failed removal surgery 333; non-product amputation 500; execution 2,000. A surgery selects one outcome; prisoners do not also earn ordinary hostile-death gains. The optional Ideology reputation boost ritual reward adds 5,000 by default for positive outcomes, to this same meter.

Hazards is a visual total over independent typed balances per map. Pollution, toxic events, hostile hives, and outdoor temperature accumulate without a cap but with diminishing gains. Sequential fallout resumes surviving history; fully faded history starts fresh. Duplicate copies of one condition do not multiply its rate. Fade begins when that source's exposure ends. A lower but nonzero exposure lowers future gains without erasing the existing balance.

## Immediate wealth adjustments

Adjusted threat wealth = max(0, Game threat wealth - Deterrence) x biome factor x island factor. Biome uses clamped settlement weight raised to an exponent of 0–3, with a default of 0.5. Island factor is an independent multiplier from 0 to 1, with a default of 1. Both are available as faction overrides for raid sizing.

Island detection follows connected land on the world map. It caches water-enclosed landmasses up to 350 tiles. The user limit defaults to 10 and can be changed independently for each faction. With Require sole settlement enabled, the current settlement list is checked against the cached tiles. An additional settlement from any faction disqualifies the island. World edges leave the factor at 1.

Options > Base threat adjusts Game threat wealth before the formula, independently of its switches. Smoothed surface multiplier defaults to 0, which removes their counted contribution; 1 is vanilla. It applies to smoothed floors and player-owned smoothed walls, including child pocket-map wealth, using vanilla's half-value building weighting. Asset prices/economic wealth are unchanged. Surface scans run hourly or on Apply while the multiplier is below 1, using floor values computed once; no surface balance accumulates or fades.

## Interaction decisions

The 0.7 menu review applies one information order to all sources: scope switch and lifecycle, formula, amount, standard example, optional colony/scenario controls, and a precise reset action. Main sources stay visible when disabled. More conditions hides only additional unconfigured conditions; searching includes them, and configured custom conditions remain discoverable when off. Sources are grouped by mechanic rather than alphabetically.

Remains exposes independent body/organ switches. Reputation groups hostile deaths, prisoner acts, and rituals. Hazards groups temperature, pollution, infestation, and loaded conditions. A legacy Acid Rain or Toxic Rain rule appears only when its condition definition is available.

The tabs follow the Formula help order. **Colony size scaling** is one global display preference on the Options tab. It reveals colony-scaling controls, sample colonist inputs, and multi-size Summary. An amount reset in the basic view only resets the amount.

Summary displays the selected scope's formula above standard examples. Examples respect source switches and use starting-from-zero balances. Curves has controls and the live plot in separate columns at normal width, with a stacked view at narrower widths.

Curves separates Hazard growth and Fading, explicitly names K and the duration exponent, and displays calibration examples. Hazard overlap, Repeated acts, and Combined plot modes use the existing calculation model. Scenario inputs are optional and do not edit actual event weights. Source-specific previews clone the configured rule and respect availability/category switches.

Numeric controls accept typing, +/- and sliders, preserving valid non-round values on passive display. Reset amount and colony scaling preserves the independent enable switch. Source selection closes optional details and resets its scroll position. Single-column scrolling is retained throughout; Options shows the last map refresh's surface discount.


Formula renders the active terms and plots the biome curve together with the qualifying-island multiplier. Its bottom section holds Colony size scaling, Faction overrides, and Open meters. The dock uses the native minimized button style with a shield badge after World. Its existing definition still opens the meter and history panel.

Presets store editable rules and faction overrides as independent copies in mod settings. Loading replaces the editable settings, then refreshes the colony. Saving a preset does not include ledger balances, maps, or wealth history.

Three expandable root meters expose categories and individual contributors. Physical contributors have map arrows; a buried body points to its grave or sarcophagus. Abstract historical acts have no locator. Cached trees rebuild after ledger updates and draw only visible rows.

The blank details panel was caused by Listing wrapping overflow into another column outside a scroll view. All scrolling settings now stay in one column. Amounts support direct typing and minus/plus buttons; animal body presets are Off (0), Low (1), Standard (5). New animal defaults are 1; saved values and independent death weights are preserved. Technical identifiers and optional population/preview controls are hidden from the main flow.

## Deliberate simplifications and player choices

Bodies, pollution, and hives are colony observations. Players can customize their effect using the faction overrides below.

## Faction overrides

Profiles are keyed by faction definition so they carry between worlds with the same faction type. Store only the fields changed by the player. Formula controls, individual rule fields, Curves, and Options inherit independently. Returning a value to Global clears its override. Reset to Global removes the whole profile across all tabs. Turning the feature off gives every faction the Global behavior and retains the profile settings.

Profiles with custom source or growth/fade fields get independent balances. They start from the existing Global snapshot, measure the same colony sources, and process new events using their resolved rules. Existing fade clocks are copied. Profile histories are saved with the colony while the choices are saved in mod settings. Global-only wealth controls and faction profiles with only Formula or Options differences reuse the Global ledger.

RimWorld chooses a faction using its ordinary starting budget. Once that choice succeeds, the mod compares the Global and faction budgets using RimWorld's own point calculation, applies their ratio, and returns control before strategy and arrival mode resolution. This preserves multipliers already applied to the storyteller budget, including Randy's. Explicit quest or event budgets keep their assigned points. Custom raid callers need to use the recognized storyteller path for the override to apply.

Preserving or burying bodies maintains deterrence. Clearing hives or pollution stops future gains once exposure ends. Timed acts can sustain the shared Reputation balance. These consequences are intended; vanilla medical, diplomatic, mood, and material costs remain unchanged. Biome plus actual temperature can both apply. Separate named heat-wave/cold-snap bonuses and their configuration are removed, including old typed balances. Actual outdoor temperature still counts during those events.

Illness, solar flares, eclipses, psychic drones, volcanic winter, poor soil, and swamp terrain are excluded. No substitute penalty is added for weakness. Default weights still require gameplay calibration.

## Verification boundary

The build, 64 model checks, and compiled integration checks pass against the installed game APIs. The checks cover fractional weights, migration, shared gains, contribution conservation, provenance, independent formula switches, legacy disabled-master migration, removed weather balances, hidden dock target, single-column geometry, physical target IDs, human-gear previews, ritual DLC gating, missing-body reconciliation, disabled-source discovery, standard example values, preview immutability, gear-only gains, and all three native plot modes. The reported missing corpse was found; no new destruction hook was added. Burial/exhumation identity and normal hourly refresh behavior are preserved.

Native UI interaction, ritual selection/reward, source arrows, smoothed wealth on a live map, and other mods replacing surgery/execution flows still need in-game checks. Existing fading timers keep their saved shape/duration. Identifiable removed-source balances are discarded; historical legacy condition labels can be ambiguous if game language changed between saves.
