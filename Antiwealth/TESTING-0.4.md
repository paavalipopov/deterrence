# Antiwealth 0.4 in-game checklist

Close RimWorld, run the supplied updater, then restart. Use a copy of an existing save. Normal source updates occur hourly; Apply & save also refreshes them. Disease no longer has an Antiwealth setting.

| Done | Check | Expected |
| --- | --- | --- |
| [ ] | Startup | No new load/patch errors; version 0.4.0 is installed. |
| [ ] | Menu | Six tabs: Formula, Habitat, Remains, Events, Curves, Compare. No Illnesses tab or disease rules. Help buttons work. |
| [ ] | New illness | With other balances stable, add a disease through Development mode. No illness credit or Antiwealth penalty appears. Vanilla storyteller effects can still change overall threat points. |
| [ ] | Old active illness | Load a copied pre-0.4 save containing illness credit. That credit disappears immediately. Other balances remain. |
| [ ] | Old fading illness | Load a copied pre-0.4 save with recovering illness credit. It is discarded rather than continuing to fade. |
| [ ] | Save again | Save/reload after migration. No illness balances or options return. Past wealth samples remain a record of the old version. |
| [ ] | Other sources | Existing remains, fallout, hive, pollution, and recovery timers survive migration. |
| [ ] | Overlap | Start fallout, later spawn a hostile hive. Both balances grow. End fallout: its balance fades while the hive keeps accumulating. Remove the hive: both fade separately. |
| [ ] | Wealth | Adjusted wealth matches max(0, ordinary threat wealth - total credit) times biome multiplier. Actual wealth/prices remain unchanged. |
| [ ] | Graphs | Preview graphs respond to settings. Live history records illness-credit removal without rewriting old samples. |

The guide uses logarithmic growth, fixed population scaling, fallout at 4,000/day on days 0-6, and one hive at 2,000/day on days 3-11. Its smooth lines illustrate the formula; the game samples hourly. Extra corpses, changing wealth, or different settings change the result.

For other systems use the historical [0.3 checklist](TESTING-0.3.md), excluding its illness row. For comparisons use Development mode > Debug output > Current threat points. A forced raid-point budget bypasses normal wealth scaling.
