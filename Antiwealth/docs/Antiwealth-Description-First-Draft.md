# Antiwealth

```text
Effective threat wealth = max(0, storyteller wealth − total antiwealth) × biome multiplier
```

**A rich colony in a dangerous place does not have to attract the same pressure as an equally rich colony in comfortable surroundings.** Antiwealth lets hostile remains, local hazards, and optional hardships offset the wealth used to calculate threats. Biome habitability provides a separate, immediate multiplier.

The rule for hardship credit is:

> Hardship builds or maintains antiwealth while it exists. After it ends, that antiwealth gradually fades.

Actual belongings keep their normal prices. Antiwealth changes the wealth supplied to the storyteller; it does not subtract silver from your stockpile or devalue items in trade.

This description follows the settings menu: **Formula → Habitat → Remains → Events → Illnesses → Curves → Compare**. Examples use the shipped defaults unless stated otherwise. Defaults are starting points for balancing, not claims of a finished difficulty balance.

## 1. Formula — the global calculation

### The three inputs

| Input | Meaning |
| --- | --- |
| **Storyteller wealth, W** | The wealth entering Antiwealth's adjustment. Vanilla normally counts items and pawns at full value and buildings at 50%. Other wealth mods can alter this input. |
| **Total antiwealth, A** | The sum of every active and recovering source balance on this home map. Each source is tracked separately. |
| **Biome multiplier, B** | An optional regional multiplier derived from the biome's settlement-selection weight. |

Antiwealth first subtracts A from W, with a floor of zero, then multiplies the result by B.

**Example:** 200,000 storyteller wealth − 20,000 antiwealth = 180,000. With a biome multiplier of 0.5, the game receives **90,000 effective threat wealth**.

The order matters: the biome multiplier scales the wealth remaining after hardship credit. It does not create an additional credit balance.

### Global adjustables

| Setting | Default | Effect |
| --- | --- | --- |
| **Apply antiwealth to threat wealth** | On | Enables both the antiwealth subtraction and biome multiplier. With it off, the input wealth passes through unchanged, while the ledger keeps tracking. |
| **Scale threat wealth by biome** | On | Enables the regional multiplier. Turn it off to use only source credits. |
| **Biome exponent** | 0.5 | Controls how strongly the biome weight changes wealth pressure. Zero is neutral, 0.25 is mild, 0.5 is square root, and 1 applies the weight directly. |

### Biome habitability: an immediate multiplier

```text
B = biome settlement-selection weight ^ biome exponent
```

The weight is clamped to 0–1. Weights above 1 are neutral; invalid values are treated as neutral. An exponent of zero gives a multiplier of 1, including for a zero-weight biome.

Vanilla already gives different biomes different preferences for placing faction settlements. Antiwealth reuses that preference as a simple measure of regional appeal. It does **not** move settlements, change settlement counts, or modify world generation.

| Biome in the installed vanilla game | Weight | Mild: exponent 0.25 | Default: exponent 0.5 | Direct: exponent 1 |
| --- | ---: | ---: | ---: | ---: |
| Temperate forest | 1.00 | 100% | 100% | 100% |
| Temperate swamp | 0.60 | 88% | 77% | 60% |
| Cold bog | 0.17 | 64% | 41% | 17% |
| Sea ice | 0.01 | 32% | 10% | 1% |

These are percentages of the remaining wealth input, **not percentages of final raid size**. Modded or patched biome weights can produce different results.

Biome scaling is immediate. It has no accumulation period and no recovery tail. Change the multiplier setting and apply it to change this part of the calculation immediately.

### What the formula does not replace

RimWorld still calculates threat points using its other factors: population, adaptation, difficulty, minimum threat points, and any incident-specific rules. A 50% wealth reduction does not necessarily halve a raid. Zero effective wealth does not guarantee no raids.

This mod does not change event frequency or duration. Wealth-independent difficulty mode is left to its normal rules. Adjustments apply to colony home maps, separately for each map; pocket maps and temporary encounter maps are excluded.

## 2. Habitat — credit from persistent local exposure

Habitat sources **accumulate over time**. Their initial daily gain is multiplied by how much of the hardship is present, then adjusted for population and diminishing returns.

The biome multiplier above is a separate mechanic. These sources measure the colony map more directly and are allowed to stack with biome scaling. Extreme temperature and terrain rules start disabled so you can choose how much additional habitat pressure to recognize. Enable **Show disabled** to find and configure those optional sources.

The category switch at the top is shared with **Events**. Turning it off starts recovery for both groups' existing balances; it does not disable the biome multiplier.

| Source | Initial default amount | Exposure used | Default |
| --- | ---: | --- | --- |
| **Polluted ground** | 4,000/day | The game's map pollution fraction; the amount is specified for 100% pollution. | On with Biotech |
| **Extreme outdoor heat** | 1,500/day | One unit per 10°C above the configured heat threshold. | Off |
| **Extreme outdoor cold** | 1,500/day | One unit per 10°C below the configured cold threshold. | Off |
| **Poor natural ground** | 2,000/day | The poor-ground fraction of surveyed natural terrain; the amount is specified for 100% coverage. | Off |
| **Swampy ground** | 2,000/day | The swamp fraction of surveyed natural terrain; the amount is specified for 100% coverage. | Off |

### Ground pollution

This counts Biotech's ground pollution, independently of toxic fallout. At **20% pollution**, a 4,000/day full-map setting produces an initial **800/day** before population scaling and diminishing returns.

Cleaning part of the map reduces future gains. It does not instantly erase credit already earned. Completely removing pollution starts recovery for that source.

### Outdoor extremes

These optional sources respond to actual outdoor temperature, even when there is no named heat wave or cold snap. Indoor heating and cooling do not create this exposure.

| Adjustable threshold | Default | Example |
| --- | ---: | --- |
| **Extreme heat begins above** | 40°C | At 55°C, the excess is 15°C: 1.5 exposure units. A 1,500/day setting starts at 2,250/day. |
| **Extreme cold begins below** | −10°C | At −30°C, the excess is 20°C: 2 exposure units. A 1,500/day setting starts at 3,000/day. |

The thresholds are beside their respective sources. These rules can stack with named heat-wave and cold-snap credits on **Events**. Enabling both is a deliberate additional discount.

### Poor ground and swamps

The survey uses visible natural base terrain beneath floors. Placing floors does not manufacture extra poor soil. Ordinary water is excluded from the surveyed denominator.

- **Poor ground** counts non-swamp natural ground below the editable fertility threshold, initially **0.7**, shown as 70%. Normal soil has fertility 1.0. Exposed barren and rocky ground can count.
- **Swampy ground** recognizes terrain definitions containing Marsh, Swamp, or Mud. These cells are not also counted as poor ground. Modded swamp terrain with other names may need an adapter.

The survey refreshes once per in-game day, or immediately with **Apply & save**. Drainage or other terrain changes can lower the earning rate. Complete removal of that exposure starts recovery.

Permanent habitat sources can continue accumulating indefinitely, with diminishing gains. There is no hard antiwealth cap.

## 3. Remains — a maintained balance while the evidence exists

Remains provide a **fixed contribution while present**. They do not earn an additional amount each day. When removed, their last contribution becomes a recovering balance.

Human, mechanoid, and animal/manhunter remains have independent settings. Their base amount defaults to **10 antiwealth per combat-power point**, with population scaling initially fixed.

### Combat power

Combat power is an authored **pawn-kind rating** used as a strength/budget estimate when generating groups. It is shared by that kind; it is not recalculated from a particular pawn's current skills, injuries, or equipment. The installed vanilla thrumbo definition uses 500. Mods can change these ratings.

For mechanoid and animal remains:

```text
Contribution = kind combat power × configured amount × population factor
```

For human remains:

```text
Contribution = (kind combat power × configured amount
                + original gear value × gear fraction) × population factor
```

Optional rot weighting applies afterward.

### Human equipment

| Setting | Default | Meaning |
| --- | --- | --- |
| **Include original human equipment** | On | Adds recorded weapons and worn apparel to the human-remains formula. |
| **Human equipment contribution** | 100% | The fraction of recorded gear market value to include. At 50%, 1,500 of gear adds 750 credit before population/rot factors. |

Weapons and worn apparel count; inventory goods do not. Gear is captured before downing or direct death and frozen when death is recorded. The downing snapshot preserves the loadout even when weapons drop or clothing is stripped before death. If the pawn recovers from downing, that pending snapshot clears so a later encounter can capture the new loadout.

**Example:** a human kind rated 50, an amount of 10, and 1,500 of recorded gear at 100% produce **2,000 antiwealth** with fixed population scaling and rot weighting off.

Stripping, selling, or moving the gear after death does not alter the corpse's recorded contribution. Existing corpses can include equipment still attached when this version first sees them; already-dropped weapons cannot reliably be reconstructed.

A zero combat-power amount can still leave a gear contribution. Disable the whole human-remains source to stop both parts.

### Preservation, disposal, and eligibility

**Reduce credit as remains decay** defaults **off**. Fresh, rotting, and dessicated remains then maintain the same value. Enable it to use shared retained fractions, initially 50% for rotting and 25% for dessicated remains. These controls are on the Remains page and apply to all remains categories.

Disposal, butchering, shredding, removal from the home map, or resurrection starts recovery. Moving a body between home maps transfers the same entry. A resurrected pawn dying again replaces its previous remains entry instead of stacking another.

Hostile-faction pawns and qualifying manhunter/scaria animals count. Ordinary wildlife, friendly visitors, and player-faction deaths do not. Discoverable corpses in spawned storage containers or graves can count outside map fog. Old non-scaria manhunter corpses may no longer be identifiable after their mental state has cleared.

## 4. Events — credit from episodes and active threats

Events **accumulate while present**, using the same growth and recovery rules as Habitat. Each actual condition instance has its own balance. Infestation pressure uses the visible hostile-hive count.

| Source | Initial default | Exposure | Default |
| --- | ---: | --- | --- |
| **Toxic fallout** | 4,000/day | One active condition instance. | On |
| **Toxic rain / acid rain** | 4,000/day each | Matching installed GameCondition instances. | On if installed |
| **Heat wave / cold snap** | 1,500/day each | One active condition instance. | On |
| **Solar flare** | 2,500/day | One active condition instance. | On |
| **Eclipse** | 750/day | One active condition instance. | On |
| **Psychic drone** | 2,000/day | One active condition instance. | On |
| **Hostile infestation** | 2,000/hive/day | Number of visible hostile Hive objects, including subclasses. | On |

All listed conditions have independent amounts. Overlapping distinct instances can stack. Protection against a condition does not cancel its credit: a well-sheltered colony can still choose to live in a hostile environment.

Removing every hostile hive starts infestation recovery. Remaining insect corpses can continue to provide their own separate contributions.

**Volcanic winter is excluded**, including from the selectable source list. Older volcanic-winter credit enters recovery.

Additional installed GameConditions appear under **Show disabled**, initially off and worth zero. Positive conditions do not automatically earn credit. A weather effect implemented only as a WeatherDef, rather than a GameCondition, needs a dedicated adapter. Exact matching matters for modded toxic or acid rain.

## 5. Illnesses — optional per-patient recovery credit

Illness tracking is **off by default**. It is available for players who want the storyteller to recognize recovery needs alongside visible environmental danger. Explicitly saved choices remain readable when updating.

Each supported disease on each affected spawned free colonist earns its own balance. The initial default is **750 per affected colonist per day**, independently configurable for each disease.

Supported diseases must be visible, non-chronic, immunizable hediffs that create a sick thought. Credit continues through visible recovery after immunity, then begins fading when the illness clears. Injuries, drug effects, chronic conditions, generic toxic buildup, prisoners, slaves, animals, and off-map pawns are outside this rule.

Population scaling is an **additional** multiplier. With fixed scaling, two colonists suffering the same disease generate two balances. Enabling linear colony-size scaling increases each patient's rate as well.

## 6. Curves — shared population, accumulation, and recovery rules

### Population scaling on each source

Every source page has a base amount, reference colony size, and population exponent:

```text
Population factor = (colonists on this map / reference colony size) ^ exponent
Initial daily gain = base amount × population factor × exposure
```

For remains, the population factor instead multiplies the maintained contribution.

| Control | Default | Interpretation |
| --- | ---: | --- |
| **Base amount** | Source-specific | The amount in the displayed units, before diminishing returns. |
| **Reference colony size** | 5 | The population at which the base amount applies. It is not a population limit. |
| **Population exponent** | 0 | Fixed scaling. Presets also provide square root (0.5) and linear (1); custom values run from 0 to 2. |

Population counts free colonists on the source's map; prisoners and slaves are excluded. Fixed rules keep their rate regardless of colony size. Scaled rules earn zero at zero population.

**Example:** a base rate of 4,000/day at five colonists gives the following initial gains, before exposure and diminishing returns:

| Population | Fixed | Square root | Linear |
| ---: | ---: | ---: | ---: |
| 1 | 4,000 | 1,789 | 800 |
| 5 | 4,000 | 4,000 | 4,000 |
| 10 | 4,000 | 5,657 | 8,000 |
| 20 | 4,000 | 8,000 | 16,000 |

### Unbounded accumulation with diminishing returns

The Curves page selects **Logarithmic** growth, the default, or **Square root**. Both keep growing while a source remains active; neither has a hard cap. Logarithmic growth slows more strongly.

The **Diminishing-returns scale**, K, defaults to **20,000**. Larger values let the initial earning rate persist longer before slowing substantially. K is not a maximum balance.

For calculation details, let E be cumulative raw exposure: the integral of the source's initial daily gain over active time.

```text
Logarithmic: F(E) = K × ln(1 + E/K)
Square root: F(E) = K × (sqrt(1 + 2E/K) − 1)
Credit added this interval = F(E + new exposure) − F(E)
```

Diminishing returns are tracked **per source**, rather than applied once to the combined antiwealth total. A source resumed during recovery keeps its remaining balance and exposure history. A genuinely new incident or disease instance is a new source.

With default logarithmic growth, a new uninterrupted 4,000/day source earns approximately **3,646 after one day** and **21,972 after ten days**, assuming fixed population and exposure.

### Cubic recovery with value-scaled duration

When a source ends, freeze its balance as H. Its duration is calculated independently of the colony's other balances:

```text
Duration = reference days × sqrt(H / reference antiwealth)
u = clamp(days since the source ended / duration, 0, 1)
Remaining credit = H × (1 − 3u² + 2u³)
```

| Shared control | Default | Effect |
| --- | ---: | --- |
| **Recovery days at the reference amount** | 10 days | Time for exactly the reference credit to reach zero. |
| **Reference amount for recovery** | 10,000 | The ending balance corresponding to those ten days. Increasing it shortens recovery for the same balance. |

| A source's ending balance | Default recovery duration |
| ---: | ---: |
| 2,500 | 5 days |
| 10,000 | 10 days |
| 40,000 | 20 days |

The cubic begins gently, declines fastest through the middle, and ends gently at exactly zero. Halfway through its duration, half the credit remains. There is no 100-silver cutoff and no upper duration cap.

### Applying changes

Rates, population scaling, and accumulation settings affect future gains without rewriting credit already earned. Active remains respond to changes in their maintained-value formula. Recovery controls apply when a source ends; changing them does not restart recovery already underway.

Disabling a source or its category starts normal recovery. The master switch on Formula stops applying adjustments while the ledger continues tracking, making paused before/after comparisons possible.

## 7. Compare — previews, live balances, and history

The Compare page puts enabled, installed source rules side by side at **1, 5, 10, and 20 colonists**, even if their shared category switch is temporarily off. Click a row to edit it. Units stay visible: an event/day, a hive/day, full pollution coverage/day, an illness/day, and a combat-power point are different quantities.

Each source also has an interactive **growth and recovery preview**. Adjust the example population, active duration, exposure, combat power, or equipment value to see the curves change. These preview controls do not change your colony. The graphs use the ledger's calculation functions and assume one new uninterrupted source at constant population; an older source can earn more slowly because of its previous exposure.

In the game's **Antiwealth** tab:

- **Sources** shows live entries, balances, earning rates, recovery time, and recorded gear information.
- **Wealth history** plots economic wealth, vanilla threat wealth, actual adjusted threat wealth, and antiwealth on the same timeline for the selected map.

History is saved with the game and records every half-day, plus when settings are applied. It begins when recording starts for that map; earlier antiwealth is marked unavailable rather than reconstructed. Applying settings while paused updates the point at that timestamp. Original vanilla History graphs remain available separately.

The adjusted line includes the biome multiplier and other installed wealth patches. The antiwealth line shows only the credit balance. Different maps are not mixed.

## Using the mod

Requires **RimWorld 1.6** and **Harmony**. No DLC is required for the core system; ground pollution requires Biotech. Enable Antiwealth after Harmony and restart after installing or updating. Existing games can start tracking immediately, but past hardship durations and already-dropped equipment cannot be recovered.

Settings are global across saves. **Apply & save** writes them and refreshes the ledger immediately. Normal source sampling happens once per in-game hour, so discovery and ending detection can lag by that interval. Terrain surveys refresh daily. Pausing does not accrue credit, and merely querying threat points does not spend or create it.

For a simple starting configuration, keep biome scaling and the shipped event/remains rates, leave source population scaling fixed, and leave extra terrain/temperature rules and illness tracking disabled. Then tune one mechanic at a time using Compare and the wealth-history view.

For testing, use a separate save and Development mode. Compare **Current threat points** while paused under Cassandra, with the Formula master switch on and off. A manually specified raid-point budget bypasses ordinary scaling; use it to create test enemies rather than measure this adjustment. See **TESTING-0.3.md** for the detailed checklist.

**Balance and compatibility:** this is a configurable change to wealth pressure, not a complete replacement storyteller. It does not schedule recovery periods between incidents or prevent harsh events from overlapping. Other mods that replace threat calculations or bypass the patched wealth input can alter its effect.
