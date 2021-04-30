using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    [StaticConstructorOnStartup]
    public static class FactionColoniesMilitary
    {
        private static List<SavedUnitFC> savedUnits = new List<SavedUnitFC>();
        private static List<SavedSquadFC> savedSquads = new List<SavedSquadFC>();

        public static string EmpireConfigFolderPath;
        public static string EmpireMilitaryUnitFolder;
        public static string EmpireMilitarySquadFolder;

        static FactionColoniesMilitary()
        {
            EmpireConfigFolderPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "Empire");
            EmpireMilitarySquadFolder = Path.Combine(EmpireConfigFolderPath, "Squads");
            EmpireMilitaryUnitFolder = Path.Combine(EmpireConfigFolderPath, "Units");
            if (!Directory.Exists(EmpireConfigFolderPath) ||
                !Directory.Exists(EmpireMilitarySquadFolder) ||
                !Directory.Exists(EmpireMilitaryUnitFolder))
            {
                Directory.CreateDirectory(EmpireConfigFolderPath);
                Directory.CreateDirectory(EmpireMilitarySquadFolder);
                Directory.CreateDirectory(EmpireMilitaryUnitFolder);
            }

            Read();
        }

        public static List<SavedUnitFC> getSavedUnits()
        {
            return savedUnits;
        }

        public static List<SavedSquadFC> getSavedSquads()
        {
            return savedSquads;
        }

        public static void RemoveSquad(SavedSquadFC squad)
        {
            savedSquads.Remove(squad);
            File.Delete(GetSquadPath(squad.name));
        }

        public static void RemoveUnit(SavedUnitFC unit)
        {
            savedUnits.Remove(unit);
            File.Delete(GetUnitPath(unit.name));
        }

        [DebugAction("Empire", "Reload Saved Military")]
        public static void Read()
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
                throw new Exception("Empire - Attempt to load saved military while scribe is active");

            foreach (string path in Directory.EnumerateFiles(EmpireMilitarySquadFolder))
            {
                try
                {
                    SavedSquadFC squad = new SavedSquadFC();
                    Scribe.loader.InitLoading(path);
                    squad.ExposeData();
                    savedSquads.Add(squad);
                }
                catch (Exception e)
                {
                    Log.Error("Failed to load squad at path " + path);
                }
                finally
                {
                    Scribe.loader.FinalizeLoading();
                }
            }

            foreach (string path in Directory.EnumerateFiles(EmpireMilitaryUnitFolder))
            {
                try
                {
                    SavedUnitFC unit = new SavedUnitFC();
                    Scribe.loader.InitLoading(path);
                    unit.ExposeData();
                    savedUnits.Add(unit);
                }
                catch (Exception e)
                {
                    Log.Error("Failed to load unit at path " + path);
                }
                finally
                {
                    Scribe.loader.FinalizeLoading();
                }
            }
        }

        public static string GetUnitPath(string name) => Path.Combine(EmpireMilitaryUnitFolder, name);
        public static string GetSquadPath(string name) => Path.Combine(EmpireMilitarySquadFolder, name);

        public static void SaveSquad(SavedSquadFC squad)
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
            {
                throw new Exception("Empire - Attempt to save squad while scribe is active");
            }

            string path = GetSquadPath(squad.name);
            try
            {
                Scribe.saver.InitSaving(path, "squad");
                int version = 0;
                Scribe_Values.Look(ref version, "version");
                squad.ExposeData();
            }
            catch (Exception e)
            {
                Log.Error("Failed to save squad " + squad.name + " " + e);
            }
            finally
            {
                Scribe.saver.FinalizeSaving();
            }
        }

        public static void SaveUnit(SavedUnitFC unit)
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
            {
                throw new Exception("Empire - Attempt to save unit while scribe is active");
            }

            string path = GetUnitPath(unit.name);
            try
            {
                Scribe.saver.InitSaving(path, "unit");
                int version = 0;
                Scribe_Values.Look(ref version, "version");
                unit.ExposeData();
            }
            catch (Exception e)
            {
                Log.Error("Failed to save unit " + unit.name + " " + e);
            }
            finally
            {
                Scribe.saver.FinalizeSaving();
            }
        }

        public static void SaveAllUnits() => savedUnits.ForEach(SaveUnit);
        public static void SaveAllSquads() => savedSquads.ForEach(SaveSquad);
    }

    public class SavedUnitFC : IExposable
    {
        public string name;
        public bool isTrader;
        public bool isCivilian;
        public PawnKindDef animal;
        public PawnKindDef pawnKind;
        public ThingDef weapon;
        public ThingDef weaponStuff;
        public Dictionary<ThingDef, ThingDef> apparel = new Dictionary<ThingDef, ThingDef>();

        public SavedUnitFC()
        {
        }

        public SavedUnitFC(MilUnitFC unit)
        {
            Pawn pawn = unit.defaultPawn;
            name = unit.name;
            weapon = pawn.equipment?.Primary?.def;
            weaponStuff = pawn.equipment?.Primary?.Stuff;
            isTrader = unit.isTrader;
            isCivilian = unit.isCivilian;
            animal = unit.animal;
            pawnKind = unit.pawnKind;
            pawn.apparel?.WornApparel?.ForEach(a => apparel.Add(a.def, a.Stuff));
        }

        public MilUnitFC CreateMilUnit()
        {
            MilUnitFC unit = new MilUnitFC
            {
                name = name,
                isCivilian = isCivilian,
                isTrader = isTrader,
                animal = animal,
                pawnKind = pawnKind
            };
            unit.generateDefaultPawn();

            if (weapon != null)
            {
                unit.equipWeapon(createThing<ThingWithComps>(weapon, weaponStuff));
            }

            foreach (KeyValuePair<ThingDef, ThingDef> entry in apparel)
            {
                unit.wearEquipment(createThing<Apparel>(entry.Key, entry.Value), true);
            }

            unit.changeTick();
            unit.updateEquipmentTotalCost();

            return unit;
        }

        private static T createThing<T>(ThingDef type, ThingDef stuff) where T : Thing
        {
            return (T) (stuff == null ? ThingMaker.MakeThing(type) : ThingMaker.MakeThing(type, stuff));
        }

        public bool IsValid => !(animal == null || pawnKind == null || weapon == null || apparel.Any(null));

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref isTrader, "isTrader");
            Scribe_Values.Look(ref isCivilian, "isCivilian");
            Scribe_Defs.Look(ref animal, "animal");
            Scribe_Defs.Look(ref pawnKind, "pawnKind");
            Scribe_Defs.Look(ref weapon, "weapon");
            Scribe_Collections.Look(ref apparel, "apparel", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.LoadingVars && !this.IsValid)
            {
                string message = $"Failed to load unit {name}. You are probably missing a mod for this unit.";
                Log.Message(message);
            }
        }
    }

    public class SavedSquadFC : IExposable
    {
        public string name;
        public List<SavedUnitFC> unitTemplates = new List<SavedUnitFC>();
        public List<int> units = new List<int>(30);
        public bool isTraderCaravan;
        public bool isCivilian;
        public double equipmentTotalCost;

        public SavedSquadFC()
        {
        }

        public SavedSquadFC(MilSquadFC squad)
        {
            name = squad.name;
            isTraderCaravan = squad.isTraderCaravan;
            isCivilian = squad.isCivilian;
            IEnumerable<MilUnitFC> squadTemplates = squad.units.Distinct();
            MilUnitFC[] milUnitFcs = squadTemplates as MilUnitFC[] ?? squadTemplates.ToArray();
            units = milUnitFcs.Select(unit => squad.units.IndexOf(unit)).ToList();
            unitTemplates = milUnitFcs.Select(unit => new SavedUnitFC(unit)).ToList();
            equipmentTotalCost = squad.equipmentTotalCost;
        }

        public MilSquadFC CreateMilSquad()
        {
            MilSquadFC squad = new MilSquadFC(true)
            {
                name = name,
                isCivilian = isCivilian,
                isTraderCaravan = isTraderCaravan,
                units = new List<MilUnitFC> {Capacity = 30},
                equipmentTotalCost = equipmentTotalCost
            };

            foreach (int i in units)
            {
                squad.units.Add(unitTemplates[i]?.CreateMilUnit());
            }

            return squad;
        }

        public bool IsValid => unitTemplates.All(u => u.IsValid);

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref isCivilian, "isCivilian");
            Scribe_Values.Look(ref isTraderCaravan, "isTraderCaravan");
            Scribe_Collections.Look(ref unitTemplates, "unitTemplates", LookMode.Deep);
            Scribe_Values.Look(ref units, "units");
            if (Scribe.mode == LoadSaveMode.LoadingVars && !IsValid) ;
            {
                string message = $"Failed to load squad {name}. You are probably missing a mod for this squad.";
                Log.Message(message);
            }
        }
    }
}