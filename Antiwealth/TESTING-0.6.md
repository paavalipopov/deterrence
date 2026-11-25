# Deterrence 0.6.0: installation smoke test

Close RimWorld. Run Update-Deterrence.bat (the previous updater also works), then restart. Its --check option should report 0.6.0 without copying files. The installed folder remains Antiwealth; the game displays Deterrence. Keep a copy of your save.

| Done | Check | Expected |
| --- | --- | --- |
| [ ] | Startup and old save | Deterrence loads after Harmony. No new mod errors. Old settings and balances remain. Only one installed copy. |
| [ ] | Former blank panel | Settings > Remains > Animal remains shows amount, input, slider, presets, and help. Scroll and open foldouts; controls stay visible. |
| [ ] | Animal weight | Choose Low (1), Apply & save. Body contribution reflects 1 per combat power. Try 0.4 by typing; reopen/reload to confirm. Hostile-death gains remain separate. |
| [ ] | Three meters | Game tab shows Remains, Reputation, Hazards. Expand into contributors; leaf values add to their parent. Expand/collapse does not change totals. |
| [ ] | Locator arrows and burials | An unburied body arrow jumps to it. Bury it: same maintained amount, marked Buried, arrow targets grave. Exhume/rebury: no duplication. Organ arrow finds its stack/container. |
| [ ] | Removed body | Dispose of a tracked body, then let one game hour pass or Apply & save. Its row becomes Fading; repeated refresh does not restart the fade. Existing bodies stay maintained. |
| [ ] | Reputation attribution | Trigger a qualifying gain. It adds to the surviving shared total and renews its fade. Expand for remaining shares. Old unrecorded history shows Earlier reputation. |
| [ ] | Optional ritual (Ideology) | Select reputation boost as a ritual bonus. Positive outcome adds the configured reward (default 5,000). Poor/negative outcome adds nothing. Other rituals do not award it automatically. |
| [ ] | Smoothed surfaces | On a map with counted smoothed floors/walls, set Formula > Smoothed surface wealth to 0, Apply & save. Surface discount appears and adjusted wealth falls; actual wealth/prices stay unchanged. Set 0.5 for half that discount, then 1 to return to vanilla. Keep other sources constant. |
| [ ] | Curves and history | Change rise/fade controls: live plots respond. History shows actual wealth, threat wealth, adjusted wealth, deterrence. Old samples keep their timestamps. |
| [ ] | Save and maps | Save/reload and switch colonies. Contributions belong to the correct map; arrows and rows update. No added balances from expansion or reburial. |

Automated: 62 model checks and compiled integration checks pass. They include single-column overflow geometry, body disappearance/fade, human gear preview, ritual DLC gating, source IDs, settings/save migration, organ provenance, and native curve objects. Live Unity behavior and your other mods require the checks above. Earlier [organ checks](TESTING-0.5.1.md) and [gameplay checks](TESTING-0.5.md) still apply; terminology in those older documents is historical.
