# Deterrence 0.6.1: settings simplification

Close RimWorld, run the existing updater, and restart. Its --check option should report 0.6.1. Open **Settings > Deterrence**.

| Done | Check | Expected |
| --- | --- | --- |
| [ ] | Formula labels | Exact formula uses Adjusted threat wealth, Game threat wealth, Deterrence, biome factor. Short descriptions explain the main categories. |
| [ ] | Independent switches | At steady wealth/credit, try both on, each on alone, and both off. Deterrence off stops its subtraction but meters keep tracking; biome off makes factor 1. |
| [ ] | Biome strength | Help explicitly says exponent. Slider updates the curve and current-map weight/factor. At strength 0 factor is 1; at 0.5 square root; at 1 the weight itself. Hover shows fractional values. |
| [ ] | Misc surfaces | Formula has no surface slider. Misc has it: 1 vanilla, 0 removes the counted contribution. It still applies with both Formula switches off. Actual wealth/prices stay unchanged. Apply & save after changes. |
| [ ] | Named weather removed | No Extra weather rules. Heat wave / Cold snap sources are absent even with Show inactive. Old separate named-event balances disappear; outdoor heat/cold remains and still reacts to temperatures during those events. |
| [ ] | Settings-only meter window | No bottom-bar Deterrence button. Formula > Open meters closes settings and opens the meters/history. Close X works; physical locator arrows can show the map. |
| [ ] | Save/load | Switch choices, biome strength, and surface multiplier survive reload. Old disabled master setting migrates to adjustments off once; later independent choices remain yours. |
| [ ] | History | New samples use Game threat wealth after Misc and Adjusted threat wealth after enabled formula factors. Older recorded values/timestamps remain intact. |

Automated: 64 model checks and compiled integration checks pass. They cover all four switch combinations, independent Misc correction, obsolete weather cleanup, migration idempotence, hidden-button definition/API support, history data, and previous model/layout/provenance checks. Native Unity interaction still needs the checks above.

The previous [0.6 checklist](TESTING-0.6.md) covers remains, rituals, arrows, and other existing mechanics. Use the new settings paths above when following it.
