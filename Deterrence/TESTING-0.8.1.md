# Deterrence 0.8.1 in-game checks

| Check | Expected result |
| --- | --- |
| Open Formula | Tabs read Formula, Remains, Reputation, Hazards, Curves, Compare, Misc. |
| Set Biome strength above 1 | Both the plot and the current biome factor update. A settlement weight of 0.5 with strength 3 gives 0.125. |
| Set Island factor to 0.5 | The Formula status and meters show 0.5 on a water-enclosed island of up to 350 world tiles. A mainland coast shows 1. Use Apply & save to refresh. |
| Select a faction and edit Island factor | It applies as an override on ordinary raids from that faction. Global keeps its own value. |
| Inspect Hazards | Acid Rain and Toxic Rain require loaded condition definitions. Toxic Fallout remains available. |
| Check source pages, Curves, and Compare | The basic view hides colony scaling and example colonist inputs. Compare shows one deterrence column. |
| Enable Show advanced colony scaling | Source details reveal their scaling controls and sample colonist inputs. Curves reveals its sample colonist input, and Compare shows four population columns. |
| Hide the controls and reset a source amount | The saved colony scaling still applies and survives the amount reset. |
