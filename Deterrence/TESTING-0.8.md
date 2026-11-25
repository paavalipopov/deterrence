# Faction overrides in 0.8.0

Close RimWorld and run the existing updater. During the first pass use a copy of a colony with at least two hostile faction types and a noticeable Global deterrence balance.

| Check | Expected result |
| --- | --- |
| Start with faction overrides off | Every faction follows Global. |
| Enable faction overrides on Formula | The Settings for selector appears on every tab. Choose a faction type. Its values initially match Global. |
| Edit the selected faction | Turn off Enable deterrence on Formula, change one Remains weight, and change a fade setting. The same faction remains selected across tabs. Global and other factions keep their own settings. |
| Change a Global field you left alone in that faction | The faction follows that change. The fields you edited for it keep their local values. |
| Check Compare and Misc | Compare uses the selected faction's weights. A surface multiplier or biome exponent edit stays in that faction. |
| Apply and reload | The faction's edits and any independent balances carry across reload. |
| Test an ordinary raid | With substantial deterrence, compare the same faction with its subtraction on and off. In Dev mode the ordinary **Do incident (map)** enemy-raid action uses storyteller-generated points. The manual **Execute raid with faction** fixed-points picker sets an explicit budget and is unsuitable for this check. Keep strategy, arrival mode, and faction comparable since these also affect raid size. |
| Reset to Global | The faction inherits all tabs again. Later Global edits carry through. |

Global continues to size other incidents and to provide faction-selection eligibility. The faction profile applies to raid sizing after selection. Scripted point budgets and wealth-independent difficulty use their original behavior.
