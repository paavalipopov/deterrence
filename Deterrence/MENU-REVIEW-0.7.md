# Deterrence 0.7: menu review

This patch reorganizes settings and their previews. Existing defaults, saved settings, and gameplay calculations are preserved. Close RimWorld and run the existing updater; --check should report 0.7.0.

The common source-page order is **scope and lifecycle → formula → amount → example → optional colony scaling / scenario → reset**. Main sources stay visible when off. Optional details close when you select another source.

| Done | Page | What changed | Check during your review |
| --- | --- | --- | --- |
| [ ] | Formula | Keeps the exact named formula, independent deterrence/biome switches, live biome curve, and settings-only Open meters. Numeric fields accompany sliders. | Are the two switches and exponent clear? Is Open meters easy to find? |
| [ ] | Hazards | Groups outdoor temperature, pollution, infestation, then conditions. Explains ongoing accumulation and source-specific fading. Thresholds sit beside temperature controls. | Does each amount state its exposure units? Disable a primary source: it should stay listed. |
| [ ] | Remains | Separate body and organ switches. Bodies and organs have their own list groups and lifecycle explanations. Body formula uses combat power; organ formula uses market value. | Turn bodies off: organs should remain independently configurable. Animal weight is easy to edit; organ multiplier has typing and +/- as well as a slider. |
| [ ] | Reputation | Groups hostile deaths, prisoner acts, rituals. Explains one shared renewed fade. Human gear sits with human death; harvesting points to kept organs on Remains. | Is the one-time gain clearly separate from maintained body/stock value? Gear-only death gains still work with combat weight 0. |
| [ ] | Curves | Separate Hazard growth and Fading sections. Explicit curve choices, K hint, duration calibration, and optional custom exponent. Plot modes: Hazard overlap, Repeated acts, Combined. | Can you distinguish growth speed, fade shape, and fade duration? Does each plot respond? Preview inputs change examples only. |
| [ ] | Compare | Grouped examples all use deterrence units at 1/5/10/20 colonists, with assumptions on each row. Off/unavailable sources remain visible. Rows open the correct source. | Can you compare amounts without confusing rates, pawn power, or multipliers? Do the row examples explain what is being compared? |
| [ ] | Misc | Explains its position before Formula; surface multiplier has a precise hint, direct input, and the current map discount from the last refresh. | 1 keeps vanilla, 0 removes the counted contribution. Apply & save refreshes it. |

## Quick consistency checks

| Done | Check | Expected |
| --- | --- | --- |
| [ ] | Edit a custom fractional amount/exponent, save, revisit pages | Merely viewing controls does not round valid saved values. |
| [ ] | Disable a source, then reset its amount/scaling | Its enable switch stays off. Animal Low/Standard presets intentionally enable the source; Off disables it. |
| [ ] | Open More conditions or search for an extra condition | Additional conditions are reachable; removed weather/illness sources do not return. A configured custom condition remains listed when off. |
| [ ] | Disable a category, expand a source scenario | No new source contribution is previewed; saved amounts are preserved. Kept organs remain independent of body/Reputation switches. |
| [ ] | Scroll through a long source and Curves page | Controls stay in one column. Switching sources resets scrolling and closes optional details. |
| [ ] | Follow a Compare row | The right tab and source open, including an off or optional source. |

Compare assumptions: one 100-power body/death; original human gear value 1,500 when gear is enabled; kept stock value 1,000; one act; or one day of a hazard at its labelled exposure. Hazard figures include diminishing returns during that day. Examples start from zero and do not represent existing colony balances or adjusted threat wealth.

Automated verification: build, 64 model checks, and compiled integration checks pass. New integration coverage includes disabled-source discovery, extra-condition search, category-aware standard examples, gear-only gains, preview copies preserving custom values, and all three plot modes. Native Unity layout/mouse behavior still needs the review above. Earlier mechanics checks remain in TESTING-0.6.1.md and TESTING-0.6.md.

Useful feedback format for the next pass: **page → source/control → confusing text or behavior → preferred change**.
