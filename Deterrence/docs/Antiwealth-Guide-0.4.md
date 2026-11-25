# Antiwealth - a short illustrated guide

**Design rule: reduce outside pressure only when there is a grounded reason. A hardship is not automatically a reason for relief.**

Illness has been removed: Antiwealth adds neither a discount nor a penalty for a sick colony. Old illness balances are discarded. RimWorld's own behavior continues.

## The whole calculation

`Effective threat wealth = max(0, ordinary threat wealth - antiwealth) x biome multiplier`

Your belongings keep their prices. This changes a wealth input to the storyteller, not raid frequency or final raid size directly. Population and other game factors still matter.

## Three different mechanics

| Mechanic | While present | After it ends |
| --- | --- | --- |
| Biome appeal | Immediately scales wealth. No credit builds up. | No fading balance. |
| Hostile remains | Maintain a fixed balance, including optional original human gear. | Their last balance fades. |
| Ongoing hazards | Add credit progressively more slowly. | Earned credit fades. |

Biome scaling is on by default: square root of the biome's settlement weight. A temperate swamp therefore uses about 77% of the remaining wealth. Exponent 0.25 is milder; 0 disables this reduction.

## Two hazards overlap

Example: fallout runs on days 0-6 at an initial 4,000/day. One hostile hive is present on days 3-11 at 2,000/day. Population scaling is fixed. No extra corpse credit, loot, or changing colony wealth is included.

![Two sources rise separately, then fade separately. The total is their sum.](docs/assets/two-hazards.svg)

Solid source lines mean the hazard is present; dashed source lines mean only its fading balance remains. The dark line adds both balances at every moment.

| Day | What happens | Fallout | Hive | Total |
| ---: | --- | ---: | ---: | ---: |
| 3 | Hive appears | 9,400 | 0 | 9,400 |
| 6 | Fallout ends | 15,769 | 5,247 | 21,016 |
| 9 | Fallout fades; hive keeps earning | 13,499 | 9,400 | 22,899 |
| 11 | Hive is cleared | 10,260 | 11,756 | 22,016 |
| 22 | Both balances have faded | 0 | 0 | 0 |

**Ending one hazard does not erase its credit or stop another source.** The total can still rise while one balance is fading. Each source keeps its own history and end time; only their current balances are added.

## What this does to wealth pressure

![Adjusted threat wealth falls as the combined balance grows and returns to the biome-only line after both sources fade.](docs/assets/wealth-effect.svg)

Here ordinary threat wealth stays at 100,000. The biome alone reduces it to about 77,460. Both hazard balances lower the input further, to about 59,700 at their combined peak. Once both reach zero, the input returns to the **biome-only** line.

These curves show the current arithmetic, not a guarantee about enemy numbers.

## The few controls that matter most

| Control | Plain meaning |
| --- | --- |
| Source amount | How much it initially earns, or how much remains maintain. |
| Population scaling | Whether that amount changes with colony size. Fixed is the default. |
| Growth slowdown | How soon daily gains diminish. Larger values delay the slowdown; they are not a cap. |
| Fade timing | Default: a 10,000 balance fades over 10 days. Four times the balance lasts twice as long. |
| Biome exponent | How strongly the region changes the wealth input. Separate from all accumulated credit. |

Growth never hits a hard cap. Fading starts gently, accelerates, then ends gently at zero. Halfway through a source's fade duration, half its ending balance remains.

In this example fallout earns about 15,769, so its tail lasts about 12.6 days after day 6. The hive earns about 11,756, so its tail lasts about 10.8 days after day 11. The durations are calculated separately.

Menu order: **Formula -> Habitat -> Remains -> Events -> Curves -> Compare**. Each source has a help button and preview graph. The Antiwealth tab shows live balances and wealth history.

## Groundedness review: what is still open

Only illness has been removed in this update. Other rules and defaults remain as before.

| Candidate | Why it needs another look |
| --- | --- |
| Solar flares and eclipses | They can weaken a colony without making an attack more dangerous. |
| Poor soil and swamps | Stable land traits may belong in an immediate appeal multiplier, rather than earn credit forever. |
| Long accumulation and fading tails | The danger itself does not necessarily grow over time. A lingering balance needs a credible deterrence/memory explanation. |
| The same discount for every attacker | Toxic pollution deters humans differently from immune attackers. Current credits are not attacker-specific. |

The next question for any rule is whether it represents outside danger or lower appeal. If it represents only colony weakness, leave Antiwealth neutral rather than rewarding or punishing it.

This remains a storyteller-input approximation; it does not simulate enemy scouting. The full original description is retained unchanged as the first draft. This shorter guide describes 0.4.0, including illness removal.
