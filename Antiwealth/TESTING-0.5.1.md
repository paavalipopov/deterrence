# Antiwealth 0.5.1: organ stock and updater checks

Close RimWorld, run the existing Update-Antiwealth.bat, and restart. Use a copied test save. That BAT is unchanged; --check performs a read-only preflight. Other features use the [0.5 checklist](TESTING-0.5.md).

| Done | Test | Expected |
| --- | --- | --- |
| [ ] | Startup/update | Updater reports source/installed version 0.5.1. No new comp/patch errors on loading an old save. |
| [ ] | Successful local prisoner harvest | The new natural organ's inspect text says it was harvested locally. Violence earns its normal configured bump. A separate kept-organs row maintains 0.75 times current market value. |
| [ ] | Imported and existing organs | Purchased, debug-spawned, and pre-update untagged organs add no stock credit. Colonist organ removals and surgeries outside home maps do not qualify. |
| [ ] | Failures/amputations | Failed surgery and non-organ amputation add their existing weighted violence outcome, without producing eligible organ stock. |
| [ ] | Independent controls | Remains > Locally harvested prisoner organs has an enable switch and 0-5 multiplier. Body/violence switches do not stop provenance tagging. Disabling stock credit does not disable harvest violence. |
| [ ] | Multiplier | For eligible stock worth 1,000: 0x gives 0, 0.75x gives 750, 5x gives 5,000. No population scaling; actual prices/wealth stay normal. |
| [ ] | Storage and hauling | Eligible organs continue counting in stockpiles, storage containers, and colonist inventory on a home map. |
| [ ] | Stacking mods | Tagged organs combine with tagged organs; ordinary and tagged stock cannot mix. Splitting a tagged stack preserves its origin. Total credit does not change from splitting/merging alone. Vanilla organs normally do not stack. |
| [ ] | Save/load | A tagged organ remains tagged and contributes after saving/reloading. Untagged old/imported items stay untagged. |
| [ ] | Sale/use/destruction | After refresh, sold organs in a foreign trader's inventory, implanted organs, and destroyed organs stop contributing. The harvest violence meter continues its own fade. |
| [ ] | Transport | Organs away from home maps do not count. Moving between home maps moves the stock contribution without duplicate tails. |
| [ ] | Partial stock loss | Keeping half the stock keeps half its value-based contribution. Stock-loss credit has no extra fade. |

Normal inventory updates occur hourly or with Apply & save. A qualifying new harvest refreshes immediately. Automated checks cover the valuation, multiplier limits, source routing, provenance comp installation, mixed-origin stack rejection, and split copying. Live Unity storage/trade/save flows still need checking.

Compatibility scope: natural parts produced through the installed vanilla removal-surgery path, using ThingWithComps items. Mods replacing surgery or item classes may need an adapter. No extra per-tick work or trader/scouting simulation is introduced.
