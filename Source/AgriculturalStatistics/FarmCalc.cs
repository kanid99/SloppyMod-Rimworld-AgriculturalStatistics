using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace AgriculturalStatistics
{
    public enum CropKind
    {
        Food,
        AnimalFeed,
        Other
    }

    /// <summary>One line of the tab: a growing zone, or all plant growers of one kind growing one crop.</summary>
    public class FarmRow
    {
        public string label;
        public Zone_Growing zone;
        public ThingDef growerDef;
        public int growerCount;
        public ThingDef plant;
        public CropKind kind;
        public int cells;
        public float avgFertility;
        public int plantCount;
        public float avgGrowth = -1f;   // -1: nothing planted yet
        public float nutritionNow;      // nutrition per day at today's conditions
        public float nutritionYear;     // nutrition per day averaged over a year
        public List<string> warnings = new List<string>();
        public IntVec3 jumpCell = IntVec3.Invalid;
    }

    public class FarmReport
    {
        public List<FarmRow> rows = new List<FarmRow>();
        public int people;
        public float peopleNeed;        // nutrition per day eaten by colonists, slaves and prisoners
        public float perPersonNeed;     // average, or a vanilla adult when there is nobody
        public int animals;
        public float animalNeed;        // nutrition per day eaten by colony animals that can eat feed crops
        public float cookedFactor;      // people fed per nutrition, cooked vs raw
        public float pasteFactor;
        public float harvesterFactor;
        public string harvesterName;
        public float foodNow, foodYear, feedNow, feedYear;
    }

    /// <summary>
    /// Works out, from the game's own numbers, how much food each growing zone and plant grower produces.
    ///
    /// A crop grows (1 - harvestAfterGrowth) of the way to harvest in growDays days of growing at a 100% growth
    /// rate, but plants only grow in daylight (vanilla rests them before 25% and after 80% of the day), so one
    /// harvest takes growDays / 0.55 / rate real days. Each cell gives harvestYield items per harvest (times the
    /// difficulty's crop yield and the harvester's yield stat), each worth the harvested item's nutrition.
    /// Sowing and harvesting time, blight and plant damage are ignored, so these are best-case figures.
    /// </summary>
    public static class FarmCalc
    {
        const float GrowingDayFraction = 0.55f;
        const float TicksPerDay = 60000f;

        static readonly Dictionary<ThingDef, float> yearTempFactorCache = new Dictionary<ThingDef, float>();

        public static FarmReport Build(Map map)
        {
            FarmReport report = new FarmReport();
            yearTempFactorCache.Clear();
            accs.Clear();
            CountMouths(map, report);
            report.cookedFactor = CookedFactor();
            report.pasteFactor = PasteFactor();
            HarvesterFactor(map, report);

            List<Zone> zones = map.zoneManager.AllZones;
            for (int i = 0; i < zones.Count; i++)
            {
                Zone_Growing zone = zones[i] as Zone_Growing;
                if (zone == null)
                    continue;
                ThingDef plant = zone.GetPlantDefToGrow();
                if (plant == null || plant.plant == null)
                    continue;
                FarmRow row = new FarmRow();
                row.label = zone.label;
                row.zone = zone;
                row.plant = plant;
                AddCells(row, zone.Cells, map, report);
                if (!zone.allowSow)
                    row.warnings.Add("AgriStats_WarnSowingOff".Translate());
                Finish(row, report);
            }

            Dictionary<string, FarmRow> growerRows = new Dictionary<string, FarmRow>();
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building_PlantGrower grower = buildings[i] as Building_PlantGrower;
                if (grower == null)
                    continue;
                ThingDef plant = grower.GetPlantDefToGrow();
                if (plant == null || plant.plant == null)
                    continue;
                string key = grower.def.defName + "|" + plant.defName;
                FarmRow row;
                if (!growerRows.TryGetValue(key, out row))
                {
                    row = new FarmRow();
                    row.growerDef = grower.def;
                    row.plant = plant;
                    growerRows.Add(key, row);
                }
                row.growerCount++;
                List<IntVec3> cells = new List<IntVec3>();
                foreach (IntVec3 c in grower.OccupiedRect())
                    cells.Add(c);
                AddCells(row, cells, map, report);
            }
            foreach (FarmRow row in growerRows.Values)
            {
                row.label = row.growerDef.LabelCap + " ×" + row.growerCount;
                Finish(row, report);
            }

            if (!AgriStatsMod.Settings.showNonFood)
                report.rows.RemoveAll(delegate (FarmRow r) { return r.kind == CropKind.Other; });

            for (int i = 0; i < report.rows.Count; i++)
            {
                FarmRow row = report.rows[i];
                if (row.kind == CropKind.Food)
                {
                    report.foodNow += row.nutritionNow;
                    report.foodYear += row.nutritionYear;
                }
                else if (row.kind == CropKind.AnimalFeed)
                {
                    report.feedNow += row.nutritionNow;
                    report.feedYear += row.nutritionYear;
                }
            }
            return report;
        }

        public static CropKind KindOf(ThingDef plant)
        {
            ThingDef crop = plant.plant.harvestedThingDef;
            if (crop == null || crop.ingestible == null || plant.plant.harvestYield <= 0f)
                return CropKind.Other;
            if (crop.GetStatValueAbstract(StatDefOf.Nutrition) <= 0f)
                return CropKind.Other;
            FoodPreferability pref = crop.ingestible.preferability;
            if (pref >= FoodPreferability.RawBad)
                return CropKind.Food;
            if (pref == FoodPreferability.DesperateOnlyForHumanlikes)
                return CropKind.AnimalFeed;
            return CropKind.Other;
        }

        // Accumulators while adding cells; turned into averages in Finish.
        class Acc
        {
            public float fertility, growth, poorSoil, noLight, tooCold, tooHot;
        }

        static readonly Dictionary<FarmRow, Acc> accs = new Dictionary<FarmRow, Acc>();

        static void AddCells(FarmRow row, List<IntVec3> cells, Map map, FarmReport report)
        {
            ThingDef plant = row.plant;
            row.kind = KindOf(plant);
            Acc acc;
            if (!accs.TryGetValue(row, out acc))
            {
                acc = new Acc();
                accs.Add(row, acc);
            }

            float perGrowthDay = NutritionPerCellPerGrowingDay(plant, report);
            float yearTemp = YearTempFactor(plant, map);

            for (int i = 0; i < cells.Count; i++)
            {
                IntVec3 c = cells[i];
                if (!c.InBounds(map))
                    continue;
                if (!row.jumpCell.IsValid)
                    row.jumpCell = c;
                row.cells++;

                float fertility = map.fertilityGrid.FertilityAt(c);
                acc.fertility += fertility;
                bool poor = fertility < plant.plant.fertilityMin;
                if (poor)
                    acc.poorSoil++;

                bool roofed = c.Roofed(map);
                float glow = roofed ? map.glowGrid.GroundGlowAt(c, false, true) : 1f;
                float light = PlantUtility.GrowthRateFactorFor_Light(plant, glow);
                if (light < 0.5f)
                    acc.noLight++;

                float temp = GenTemperature.GetTemperatureForCell(c, map);
                float tempNow = TempFactor(plant, temp);
                if (temp <= plant.plant.minGrowthTemperature)
                    acc.tooCold++;
                else if (temp >= plant.plant.maxGrowthTemperature)
                    acc.tooHot++;

                // Indoors (roofed, in a room that doesn't take the outdoor temperature) the climate is assumed to be
                // held where it is now; outdoors the year follows the map's seasons.
                float tempYear = yearTemp;
                if (roofed)
                {
                    Room room = c.GetRoom(map);
                    if (room != null && !room.UsesOutdoorTemperature)
                        tempYear = tempNow;
                }

                float soil = poor ? 0f : PlantUtility.GrowthRateFactorFor_Fertility(plant, fertility);
                row.nutritionNow += perGrowthDay * soil * light * tempNow;
                row.nutritionYear += perGrowthDay * soil * light * tempYear;

                Plant growing = c.GetPlant(map);
                if (growing != null && growing.def == plant)
                {
                    row.plantCount++;
                    acc.growth += growing.Growth;
                }
            }
        }

        static void Finish(FarmRow row, FarmReport report)
        {
            Acc acc;
            if (accs.TryGetValue(row, out acc) && row.cells > 0)
            {
                row.avgFertility = acc.fertility / row.cells;
                if (row.plantCount > 0)
                    row.avgGrowth = acc.growth / row.plantCount;
                float half = row.cells * 0.5f;
                if (acc.poorSoil > half)
                    row.warnings.Add("AgriStats_WarnPoorSoil".Translate());
                if (acc.noLight > half)
                    row.warnings.Add("AgriStats_WarnNoLight".Translate());
                if (acc.tooCold > half)
                    row.warnings.Add("AgriStats_WarnTooCold".Translate());
                else if (acc.tooHot > half)
                    row.warnings.Add("AgriStats_WarnTooHot".Translate());
            }
            accs.Remove(row);
            if (row.kind == CropKind.Other)
                row.warnings.Add("AgriStats_WarnNotFood".Translate());
            report.rows.Add(row);
        }

        /// <summary>Nutrition per day from one cell growing at a 100% growth rate.</summary>
        static float NutritionPerCellPerGrowingDay(ThingDef plant, FarmReport report)
        {
            PlantProperties p = plant.plant;
            ThingDef crop = p.harvestedThingDef;
            if (crop == null || p.growDays <= 0f || p.harvestYield <= 0f)
                return 0f;
            float cycle = Mathf.Max(0.05f, 1f - p.harvestAfterGrowth);
            float daysPerHarvest = p.growDays * cycle / GrowingDayFraction;
            float yield = p.harvestYield * report.harvesterFactor;
            if (p.harvestYieldAffectedByDifficulty && Find.Storyteller != null && Find.Storyteller.difficulty != null)
                yield *= Find.Storyteller.difficulty.cropYieldFactor;
            return yield * crop.GetStatValueAbstract(StatDefOf.Nutrition) / daysPerHarvest;
        }

        static float TempFactor(ThingDef plant, float temp)
        {
            if (temp <= plant.plant.minGrowthTemperature || temp >= plant.plant.maxGrowthTemperature)
                return 0f;
            return PlantUtility.GrowthRateFactorFor_Temperature(plant, temp);
        }

        /// <summary>The plant's outdoor temperature factor averaged over the twelve months of this map.</summary>
        static float YearTempFactor(ThingDef plant, Map map)
        {
            float cached;
            if (yearTempFactorCache.TryGetValue(plant, out cached))
                return cached;
            float sum = 0f;
            for (int i = 0; i < 12; i++)
                sum += TempFactor(plant, GenTemperature.AverageTemperatureAtTileForTwelfth(map.Tile, (Twelfth)i));
            float factor = sum / 12f;
            yearTempFactorCache[plant] = factor;
            return factor;
        }

        static void CountMouths(Map map, FarmReport report)
        {
            ThingDef hay = ThingDefOf.Hay;
            List<Thing> pawns = map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i] as Pawn;
                if (pawn == null || pawn.Dead || pawn.needs == null || pawn.needs.food == null)
                    continue;
                float perDay = pawn.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed, true) * TicksPerDay;
                if (pawn.RaceProps.Humanlike)
                {
                    if (pawn.Faction == Faction.OfPlayer || pawn.HostFaction == Faction.OfPlayer)
                    {
                        report.people++;
                        report.peopleNeed += perDay;
                    }
                }
                else if (pawn.RaceProps.Animal && pawn.Faction == Faction.OfPlayer)
                {
                    if (hay != null && pawn.RaceProps.CanEverEat(hay))
                    {
                        report.animals++;
                        report.animalNeed += perDay;
                    }
                }
            }
            if (report.people > 0)
                report.perPersonNeed = report.peopleNeed / report.people;
            else
                report.perPersonNeed = Need_Food.BaseHungerRate(LifeStageDefOf.HumanlikeAdult, ThingDefOf.Human) * TicksPerDay;
            if (report.perPersonNeed <= 0f)
                report.perPersonNeed = 1.6f;
        }

        /// <summary>How many more people a unit of raw food feeds when cooked into simple meals.</summary>
        static float CookedFactor()
        {
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail("CookMealSimple");
            if (recipe != null && recipe.ingredients != null && recipe.ingredients.Count > 0
                && recipe.products != null && recipe.products.Count > 0 && recipe.products[0].thingDef != null)
            {
                float input = 0f;
                for (int i = 0; i < recipe.ingredients.Count; i++)
                    input += recipe.ingredients[i].GetBaseCount();
                float output = recipe.products[0].thingDef.GetStatValueAbstract(StatDefOf.Nutrition) * recipe.products[0].count;
                if (input > 0f && output > 0f)
                    return output / input;
            }
            return 1.8f;
        }

        /// <summary>How many more people a unit of raw food feeds through a nutrient paste dispenser.</summary>
        static float PasteFactor()
        {
            ThingDef dispenser = ThingDefOf.NutrientPasteDispenser;
            ThingDef meal = ThingDefOf.MealNutrientPaste;
            if (dispenser != null && dispenser.building != null && meal != null)
            {
                float cost = dispenser.building.nutritionCostPerDispense;
                float output = meal.GetStatValueAbstract(StatDefOf.Nutrition);
                if (cost > 0f && output > 0f)
                    return output / cost;
            }
            return 3f;
        }

        static void HarvesterFactor(Map map, FarmReport report)
        {
            report.harvesterFactor = 1f;
            report.harvesterName = null;
            if (!AgriStatsMod.Settings.useBestGrower)
                return;
            float best = -1f;
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Growing))
                    continue;
                float yield = pawn.GetStatValue(StatDefOf.PlantHarvestYield);
                if (yield > best)
                {
                    best = yield;
                    report.harvesterName = pawn.LabelShortCap;
                }
            }
            if (best > 0f)
                report.harvesterFactor = best;
        }
    }
}
