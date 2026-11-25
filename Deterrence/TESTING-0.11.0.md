# Corpse storage filters

In a test colony, select an empty grave or a stockpile and expand **Storage → Corpses → Humanlike corpses**. Expand **Allowed status at death**, **Allowed faction status**, and **Allowed factions**, in that order. Each begins on **Any**. The ordinary Colonist and Stranger options must permit the test corpse.

| Check | Expected result |
| --- | --- |
| Allow only Hostile under status at death | A raider qualifies. A noncombatant from the same faction and an ordinary prisoner do not. A captured and subsequently recruited raider is Friendly. |
| Kill a downed raider after saving and reloading | The corpse retains the individual status recorded before the raid group lost the pawn. Recovery, resurrection, or changed custody clears the old downed evidence. |
| Compare a player-hosted guest, a slave and a rebel | A peaceful guest and colony slave are Friendly. Active rebellion or an attack on the player counts as Hostile. Verify slave cases with Ideology enabled. |
| Choose Factionless with faction status on Any | A factionless humanlike corpse qualifies. Setting faction status to Neutral excludes it. The player faction similarly has no bilateral diplomatic status. |
| Change the deceased pawn’s faction from hostile to allied | The Allowed faction status result changes. The stored individual status stays fixed, including for corpses inferred when upgrading a save. |
| Use None, then Any, on each branch | None rejects all ordinary humanlike corpse storage. Any clears that restriction and includes future factions. Animals and mechanoids still use their ordinary filters. |
| Compare an unassigned grave, sarcophagus and stockpile | Give nearby destinations conflicting settings and watch burial and hauling. Change diplomacy or settings during a carry and check the final destination. |
| Assign and unassign a grave or sarcophagus | Its assigned pawn can use it even with None selected. Other corpses cannot. Clearing the assignment restores the configured rules. |
| Copy and paste in both directions, then save and load | Masks and faction selections survive, including an explicitly empty faction list. Pasting Any clears restrictions. Editing the copy does not change the original. Linked storage shares the group’s rules. |
| Search for a branch, a status and a faction | Expandable rows appear under Humanlike corpses in the storage tree. Compare a bill ingredient filter. Check a modded humanlike race and a modded building that uses RimWorld storage settings. |

On old saves, compare corpses on the ground, in graves and in storage. Their inspection text identifies the one-time faction estimate. Change diplomacy after loading and check again. Explicitly selected factions retain their world IDs after a rename or removal. A storage rule set to Any includes newly added factions.

For a specific hauling question, open the developer actions menu and choose **Deterrence → Inspect corpse storage rules**. Click a humanlike corpse on the ground and then a destination. The game log reports the recorded or inferred status, faction ID, current diplomacy, first failed rule or assignment, and the destination’s own acceptance result.

## Implementation

The storage tree adds the three rows when the expanded category is `CorpsesHumanlike` and the edited filter belongs to a storage destination. An optional record on `StorageSettings` holds the two status masks and a set of faction IDs. `StorageSettings.AllowedToAccept(Thing)` evaluates them after vanilla accepts the actual corpse. The game’s clipboard, groups and save system use `CopyFrom` and `ExposeData`. Explicit grave assignments follow RimWorld’s own acceptance path.

Individual status and faction identity travel with the inner pawn. The mod samples conduct before downing or death can remove the pawn from its group. For older corpses, it resolves the one-time estimate after loading. The diplomatic check looks up the saved faction ID and reads its current relationship each time it matters.

## Automated checks

Run `Test.ps1` and `Tools/Test-Integration.ps1`. To run the storage test in a real disposable game, use `Tools/Test-CorpseStorageGame.ps1 -TestGame <isolated RimWorld copy> -Profile <new empty directory>`. The copy needs Core, Harmony and a Deterrence installation in `Mods/Deterrence`. The runner copies the built DLL there and compiles the probe as a separate test mod. It uses a fresh quick-test colony, writes a save, reloads it, and returns the individual check results.

Supply `-AdaptiveStorageMod <framework folder>` to test an Adaptive Storage Framework shelf. Supply `-Ideology` if the test copy includes that expansion to exercise slavery and rebellion.

For the 0.12.0 name migration, pass -LegacySave with an unmodified round-trip save produced by the previous version of this test. Use the same expansions, Adaptive Storage Framework, and a fresh profile. The extra checks cover settings, meter history, ledger balances, corpse records, and storage rules.
