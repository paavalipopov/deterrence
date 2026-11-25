// Compile as a separate test mod. Tests run only in a -quicktest game with a result path.
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace DeterrenceTests
{
    public sealed class BurialGameTest : GameComponent
    {
        private static string Result => Environment.GetEnvironmentVariable("DETERRENCE_TEST_RESULT");
        private const string SaveName = "DeterrenceStorageRoundTrip";
        private static readonly Type Rules = typeof(Deterrence.DeterrenceMod).Assembly.GetType("Deterrence.CorpseStorageRules");
        private static readonly Type Records = Rules.Assembly.GetType("Deterrence.BurialRecord");
        private static readonly List<string> passes = new List<string>();
        private static int corpseId, graveId, stockpileId, legacyId, legacyFactionlessId, downedId;
        private static bool loadingAntiwealthSave;
        private static bool historyMatched;
        private static void InspectImportedHistory(Deterrence.DeterrenceGameComponent __instance)
        {
            // FinalizeInit refreshes the current day's sample. Compare before it runs.
            var xml = new XmlDocument();
            xml.Load(GenFilePaths.FilePathForSavedGame("AntiwealthBeforeRename"));
            var old = xml.SelectNodes("//antiwealthWealthHistory/li").Cast<XmlNode>().ToArray();
            var actual = (List<Deterrence.WealthSample>)Get(__instance, "wealthHistory");
            float Value(XmlNode node, string field) =>
                float.Parse(node[field]?.InnerText ?? "0", CultureInfo.InvariantCulture);
            historyMatched = old.Length > 0 && old.Length == actual.Count && old.Select((node, i) =>
                actual[i].MapId == Value(node, "mapId") && actual[i].Day == Value(node, "day")
                && actual[i].Economy == Value(node, "economy") && actual[i].Deterrence == Value(node, "antiwealth")
                && actual[i].AdjustedThreat == Value(node, "adjustedThreat")).All(equal => equal);
        }
        public BurialGameTest(Game game) { }
        public override void StartedNewGame()
        {
            if (!string.IsNullOrEmpty(Result) && GenCommandLine.CommandLineArgPassed("quicktest"))
                LongEventHandler.ExecuteWhenFinished(() => Run(false));
        }
        public override void LoadedGame()
        {
            if (!string.IsNullOrEmpty(Result) && graveId != 0)
                LongEventHandler.ExecuteWhenFinished(() => Run(true));
        }
        private static object Rule(StorageSettings settings) => AccessTools.Method(Rules,"For").Invoke(null,new object[]{settings});
        private static void Field(object obj, string name, object value) => AccessTools.Field(obj.GetType(),name).SetValue(obj,value);
        private static object Get(object obj,string name) => AccessTools.Field(obj.GetType(),name).GetValue(obj);
        private static object Record(Pawn pawn) => AccessTools.Method(Records,"ForCorpse").Invoke(null,new object[]{pawn});
        private static void RemoveRecord(Pawn pawn)
        {
            var table = AccessTools.Field(Records,"records").GetValue(null);
            table.GetType().GetMethod("Remove").Invoke(table,new object[]{pawn});
        }
        private static void Check(bool value,string label)
        {
            if (!value) throw new InvalidOperationException(label);
            passes.Add(label);
            Log.Message("[Deterrence test] PASS " + label);
        }
        private static void Finish(string state)
        {
            File.WriteAllLines(Result, passes.Concat(new[] {state}).ToArray());
            Application.Quit();
        }
        private static void Run(bool loaded)
        {
            try
            {
                var map = Find.CurrentMap;
                if (loaded && loadingAntiwealthSave)
                {
                    Check(historyMatched, "legacy history matches before the normal on-load refresh");
                    Check(Deterrence.DeterrenceMod.Settings.IslandMaxTiles == 137,
                        "legacy mod settings load from the old file and class name");
                    GameDataSaveLoader.SaveGame("DeterrenceAfterRename");
                    Finish("SUCCESS");
                    return;
                }
                if (loaded)
                {
                    var body = (Corpse)map.listerThings.AllThings.First(t => t.thingIDNumber == corpseId);
                    var grave = (Building_Grave)map.listerThings.AllThings.First(t => t.thingIDNumber == graveId);
                    var zone = (Zone_Stockpile)map.zoneManager.AllZones.First(z => z.ID == stockpileId);
                    var rules = Rule(zone.GetStoreSettings());
                    Check((int)Get(rules,"Status") == 2 && (int)Get(rules,"Diplomacy") == 1
                        && ((Dictionary<int,string>)Get(rules,"Factions")).ContainsKey(body.InnerPawn.Faction.loadID),
                        "all three restrictions survive a game save and reload");
                    Check(zone.Accepts(body) && !grave.Accepts(body) && Record(body.InnerPawn) != null
                        && Get(Record(body.InnerPawn),"Status").ToString() == "Neutral", "independent grave rules and individual record survive reload");
                    var legacy = (Corpse)map.listerThings.AllThings.First(t => t.thingIDNumber == legacyId);
                    var noFaction = (Corpse)map.listerThings.AllThings.First(t => t.thingIDNumber == legacyFactionlessId);
                    body.InnerPawn.Faction.RelationWith(Faction.OfPlayer).kind = FactionRelationKind.Neutral;
                    var inferred = Record(legacy.InnerPawn);
                    Check((bool)Get(inferred,"Inferred") && Get(inferred,"Status").ToString()=="Hostile",
                        "legacy status inferred during load and frozen before a later peace treaty");
                    Check(Get(Record(noFaction.InnerPawn),"Status").ToString()=="Neutral" && (int)Get(Record(noFaction.InnerPawn),"FactionId")==-1,
                        "legacy factionless corpse gets a neutral estimate and keeps its separate faction identity");
                    var downedRaider = (Pawn)map.listerThings.AllThings.First(t => t.thingIDNumber == downedId);
                    downedRaider.Kill(null);
                    Check(Get(Record(downedRaider),"Status").ToString()=="Hostile","bleeding-out raider retains evidence across a save while downed");
                    if (Environment.GetEnvironmentVariable("DETERRENCE_TEST_LEGACY") == "1")
                    {
                        loadingAntiwealthSave = true;
                        new Harmony("local.deterrence.renametest").Patch(
                            AccessTools.Method(typeof(Deterrence.DeterrenceGameComponent), "FinalizeInit"),
                            prefix: new HarmonyMethod(typeof(BurialGameTest), nameof(InspectImportedHistory)));
                        LongEventHandler.QueueLongEvent(() => GameDataSaveLoader.LoadGame("AntiwealthBeforeRename"),
                            "LoadingLongEvent", false, null);
                    }
                    else Finish("SUCCESS");
                    return;
                }
                var carrier = map.mapPawns.FreeColonistsSpawned.First();
                var enemy = Find.FactionManager.AllFactionsListForReading.First(f => f.def.humanlikeFaction && !f.Hidden && f.HostileTo(Faction.OfPlayer));
                // Graves and sarcophagi occupy multiple cells. Keep the fixtures apart
                // so spawning a later building cannot wipe a previous test destination.
                var cells = GenRadial.RadialCellsAround(carrier.Position, 35, true)
                    .Where(c => c.x % 5 == 0 && c.z % 5 == 0 && CellRect.CenteredOn(c, 1).Cells.All(
                        cell => cell.InBounds(map) && cell.Standable(map) && cell.GetEdifice(map) == null
                        && !cell.GetThingList(map).Any(t => t.def.EverHaulable) && map.zoneManager.ZoneAt(cell) == null))
                    .Take(15).ToArray();
                var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Villager, enemy);
                GenSpawn.Spawn(pawn, cells[0], map);
                pawn.Kill(null);
                var corpse = pawn.Corpse;
                Check(Get(Record(pawn),"Status").ToString() == "Neutral", "peaceful member of a hostile faction is individually neutral");
                var raider = PawnGenerator.GeneratePawn(PawnKindDefOf.Villager, enemy);
                GenSpawn.Spawn(raider, cells[1], map);
                LordMaker.MakeNewLord(enemy,new LordJob_AssaultColony(enemy),map,new[]{raider});
                raider.Kill(null);
                Check(Get(Record(raider),"Status").ToString() == "Hostile","raider status captured before lord loss");
                var graveBuilding = (Building_Grave)GenSpawn.Spawn(ThingDefOf.Grave,cells[2],map);
                graveBuilding.SetFaction(Faction.OfPlayer);
                var zoneStorage = new Zone_Stockpile(StorageSettingsPreset.CorpseStockpile,map.zoneManager);
                map.zoneManager.RegisterZone(zoneStorage);
                zoneStorage.AddCell(cells[4]);
                var settings = zoneStorage.GetStoreSettings();
                settings.filter.SetAllowAll(null);
                StorageSettingsClipboard.Copy(settings);
                StorageSettingsClipboard.PasteInto(graveBuilding.GetStoreSettings());
                var zoneRules = Rule(settings);
                var menu = Rules.Assembly.GetType("Deterrence.CorpseFilterMenu");
                Check(AccessTools.Method(menu,"Owner").Invoke(null,new object[]{settings.filter}) == settings,
                    "storage tree resolves the exact edited storage settings");
                Check(AccessTools.Method(menu,"Owner").Invoke(null,new object[]{new ThingFilter()}) == null,
                    "non-storage filters are excluded");
                Check(((string[])AccessTools.Field(menu,"Titles").GetValue(null)).SequenceEqual(new[]{
                    "Allowed status at death","Allowed faction status","Allowed factions"}),"exact branch names and order");
                var search = new QuickSearchFilter { Text = "Allowed status at death" };
                var tree = new Listing_TreeThingFilter(settings.filter,zoneStorage.GetParentStoreSettings()?.filter,null,null,null,search);
                var matches = AccessTools.Method(typeof(Listing_TreeThingFilter),"ThisOrDescendantsVisibleAndMatchesSearch");
                Check((bool)matches.Invoke(tree,new object[]{ThingCategoryDefOf.Corpses.treeNode}),"storage search finds a custom branch below Corpses");
                Check(zoneStorage.Accepts(corpse) && graveBuilding.Accepts(corpse), "Any permits an otherwise eligible corpse");
                Field(zoneRules,"Status",0);
                Check(!StoreUtility.IsValidStorageFor(cells[4],map,corpse) && !zoneStorage.Accepts(corpse),"None blocks the stockpile destination");
                Field(zoneRules,"Status",2);
                Field(zoneRules,"Diplomacy",1);
                Field(zoneRules,"Factions",new Dictionary<int,string>{{enemy.loadID,enemy.Name}});
                Check(zoneStorage.Accepts(corpse), "stockpile accepts neutral individual from selected hostile faction");
                Check(!zoneStorage.Accepts(raider.Corpse), "stockpile excludes hostile individual of the same faction");
                Check(StoreUtility.IsValidStorageFor(cells[4],map,corpse),"valid stockpile cell honors the matching rules");
                var relation = enemy.RelationWith(Faction.OfPlayer);
                var original = relation.kind;
                relation.kind = FactionRelationKind.Ally;
                Check(!zoneStorage.Accepts(corpse) && Get(Record(pawn),"Status").ToString()=="Neutral","diplomacy changes without rewriting status");
                relation.kind = original;
                StorageSettingsClipboard.Copy(settings);
                StorageSettingsClipboard.PasteInto(graveBuilding.GetStoreSettings());
                var copied = Rule(graveBuilding.GetStoreSettings());
                ((Dictionary<int,string>)Get(copied,"Factions")).Clear();
                Check(zoneStorage.Accepts(corpse) && !graveBuilding.Accepts(corpse),"clipboard deep copy and independent empty faction selection");
                var assign = graveBuilding.GetComp<CompAssignableToPawn_Grave>();
                assign.TryAssignPawn(pawn);
                Check(graveBuilding.Accepts(corpse) && !graveBuilding.Accepts(raider.Corpse),"assigned grave overrides restrictions for precisely one pawn");
                assign.TryUnassignPawn(pawn);
                Check(!graveBuilding.Accepts(corpse),"unassigning restores the filters");
                var sarcophagus = (Building_Grave)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Sarcophagus, ThingDefOf.WoodLog),cells[8],map);
                sarcophagus.SetFaction(Faction.OfPlayer);
                StorageSettingsClipboard.Copy(graveBuilding.GetStoreSettings());
                StorageSettingsClipboard.PasteInto(sarcophagus.GetStoreSettings());
                Check(!sarcophagus.Accepts(corpse),"sarcophagus enforces copied restrictions");
                sarcophagus.GetComp<CompAssignableToPawn_Grave>().TryAssignPawn(pawn);
                Check(sarcophagus.Accepts(corpse),"explicit sarcophagus assignment overrides filters");
                sarcophagus.GetComp<CompAssignableToPawn_Grave>().TryUnassignPawn(pawn);
                var unrestricted = new StorageSettings();
                unrestricted.filter.SetAllowAll(null);
                StorageSettingsClipboard.Copy(unrestricted);
                StorageSettingsClipboard.PasteInto(sarcophagus.GetStoreSettings());
                Check(sarcophagus.Accepts(corpse),"pasting Any clears prior restrictions");
                IntVec3 found;
                Check(StoreUtility.TryFindBestBetterStoreCellForIn(corpse,carrier,map,StoragePriority.Unstored,Faction.OfPlayer,zoneStorage.GetSlotGroup(),out found,true)
                    && found == cells[4],"hauling search selects the eligible stockpile cell");
                Field(zoneRules,"Status",0);
                Check(!StoreUtility.TryFindBestBetterStoreCellForIn(corpse,carrier,map,StoragePriority.Unstored,Faction.OfPlayer,zoneStorage.GetSlotGroup(),out found,true),
                    "hauling search skips the restricted stockpile");
                Field(zoneRules,"Status",2);
                var fixture = DefDatabase<ThingDef>.GetNamedSilentFail("DeterrenceTestCorpseShelf");
                var shelfDef = fixture ?? DefDatabase<ThingDef>.GetNamed("ShelfSmall");
                var shelf = (Building_Storage)GenSpawn.Spawn(ThingMaker.MakeThing(shelfDef,shelfDef.MadeFromStuff ? ThingDefOf.WoodLog : null),cells[12],map);
                shelf.SetFaction(Faction.OfPlayer);
                var second = (Building_Storage)GenSpawn.Spawn(ThingMaker.MakeThing(shelfDef,shelfDef.MadeFromStuff ? ThingDefOf.WoodLog : null),cells[13],map);
                second.SetFaction(Faction.OfPlayer);
                shelf.GetStoreSettings().CopyFrom(settings);
                if (ModsConfig.IsActive("adaptive.storage.framework"))
                    Check(fixture != null && shelf.GetType().Assembly != typeof(Building_Storage).Assembly && shelf.Accepts(corpse)
                        && !shelf.Accepts(raider.Corpse), "Adaptive Storage Framework building enforces individual restrictions");
                var group=map.storageGroups.NewGroup("Test group");
                group.InitFrom(shelf);
                ((IStorageGroupMember)shelf).SetStorageGroup(group);
                ((IStorageGroupMember)second).SetStorageGroup(group);
                Check(ReferenceEquals(shelf.GetStoreSettings(),second.GetStoreSettings()) && (int)Get(Rule(second.GetStoreSettings()),"Status")==2,
                    "linked storage shares its group restrictions");
                ((IStorageGroupMember)second).SetStorageGroup(null);
                Field(Rule(second.GetStoreSettings()),"Status",0);
                Check((int)Get(Rule(shelf.GetStoreSettings()),"Status")==2,"unlinking restores independent settings");
                var prisoner = PawnGenerator.GeneratePawn(PawnKindDefOf.Villager,enemy);
                GenSpawn.Spawn(prisoner,cells[6],map);
                LordMaker.MakeNewLord(enemy,new LordJob_AssaultColony(enemy),map,new[]{prisoner});
                prisoner.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);
                prisoner.Kill(null);
                Check(Get(Record(prisoner),"Status").ToString()=="Neutral","enemy raider in player custody is neutral at death");
                var visitorFaction = Find.FactionManager.AllFactionsListForReading.First(f => f.def.humanlikeFaction
                    && !f.IsPlayer && !f.Hidden && !f.HostileTo(Faction.OfPlayer));
                var guest = PawnGenerator.GeneratePawn(PawnKindDefOf.Villager,visitorFaction);
                GenSpawn.Spawn(guest,cells[7],map);
                guest.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Guest);
                guest.Kill(null);
                Check(Get(Record(guest),"Status").ToString()=="Friendly","player-hosted guest is individually friendly");
                if (ModsConfig.IdeologyActive)
                {
                    foreach (bool rebelling in new[]{false,true})
                    {
                        var slave = PawnGenerator.GeneratePawn(PawnKindDefOf.Villager,enemy);
                        GenSpawn.Spawn(slave,cells[5],map);
                        slave.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Slave);
                        Check(slave.IsSlaveOfColony, "Ideology colony slave created");
                        if (rebelling)
                            LordMaker.MakeNewLord(slave.Faction,new LordJob_SlaveRebellion(cells[5],cells[10],6000,true),map,new[]{slave});
                        slave.Kill(null);
                        Check(Get(Record(slave),"Status").ToString()==(rebelling ? "Hostile" : "Friendly"),
                            rebelling ? "active slave rebellion overrides colony allegiance" : "peaceful colony slave is friendly");
                    }
                }
                var outsider = PawnGenerator.GeneratePawn(PawnKindDefOf.Villager,enemy);
                outsider.SetFaction(null);
                GenSpawn.Spawn(outsider,cells[10],map);
                outsider.Kill(null);
                var factionlessRules=Rule(sarcophagus.GetStoreSettings());
                Field(factionlessRules,"Factions",new Dictionary<int,string>{{-1,"Factionless"}});
                Check(sarcophagus.Accepts(outsider.Corpse) && !sarcophagus.Accepts(corpse),"factionless is selectable independently");
                Field(factionlessRules,"Diplomacy",2);
                Check(!sarcophagus.Accepts(outsider.Corpse),"factionless does not satisfy neutral diplomacy");
                var downed = PawnGenerator.GeneratePawn(PawnKindDefOf.Villager,enemy);
                GenSpawn.Spawn(downed,cells[11],map);
                LordMaker.MakeNewLord(enemy,new LordJob_AssaultColony(enemy),map,new[]{downed});
                AccessTools.Method(typeof(Pawn_HealthTracker),"MakeDowned").Invoke(downed.health,new object[]{null,null});
                downedId=downed.thingIDNumber;
                legacyId=prisoner.Corpse.thingIDNumber; legacyFactionlessId=outsider.Corpse.thingIDNumber;
                RemoveRecord(prisoner); RemoveRecord(outsider);
                corpseId=corpse.thingIDNumber; graveId=graveBuilding.thingIDNumber; stockpileId=zoneStorage.ID;
                GameDataSaveLoader.SaveGame(SaveName);
                LongEventHandler.QueueLongEvent(() => GameDataSaveLoader.LoadGame(SaveName),"LoadingLongEvent",false,null);
            }
            catch (Exception e) { Log.Error(e.ToString()); Finish("FAILED: " + e); }
        }
    }
}
