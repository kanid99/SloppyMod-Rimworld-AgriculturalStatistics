using UnityEngine;
using Verse;

namespace AgriculturalStatistics
{
    public class AgriStatsSettings : ModSettings
    {
        /// <summary>Use the best grower's harvest yield; off = a flat 100%.</summary>
        public bool useBestGrower = true;

        /// <summary>List zones growing things that aren't food (cotton, smokeleaf, trees...).</summary>
        public bool showNonFood = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref useBestGrower, "useBestGrower", true);
            Scribe_Values.Look(ref showNonFood, "showNonFood", true);
        }
    }

    public class AgriStatsMod : Mod
    {
        public static AgriStatsSettings Settings;

        public AgriStatsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AgriStatsSettings>();
        }

        public override string SettingsCategory()
        {
            return "AgriStats_ModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);
            list.CheckboxLabeled("AgriStats_SettingBestGrower".Translate(), ref Settings.useBestGrower, "AgriStats_SettingBestGrowerDesc".Translate());
            list.CheckboxLabeled("AgriStats_SettingShowNonFood".Translate(), ref Settings.showNonFood, "AgriStats_SettingShowNonFoodDesc".Translate());
            list.End();
        }
    }
}
