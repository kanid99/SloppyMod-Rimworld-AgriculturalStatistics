using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AgriculturalStatistics
{
    /// <summary>The Farming tab: one row per growing zone or kind of plant grower, with totals against what the colony eats.</summary>
    public class MainTabWindow_Farming : MainTabWindow
    {
        const float RowHeight = 28f;
        const float HeaderHeight = 30f;
        const int RefreshTicks = 250;
        const int RefreshFrames = 120;   // also refresh while paused, roughly every two seconds

        static readonly Color GoodColor = new Color(0.55f, 0.85f, 0.55f);
        static readonly Color BadColor = new Color(0.95f, 0.5f, 0.45f);
        static readonly Color WarnColor = new Color(1f, 0.8f, 0.4f);

        // Column widths; the notes column takes what's left.
        static readonly float[] Widths = { 210f, 190f, 60f, 80f, 70f, 105f, 90f, 100f, 90f };

        FarmReport report;
        Map reportMap;
        int reportTick = -99999;
        int framesSinceRefresh;
        bool yearView;
        Vector2 scroll;

        public override Vector2 RequestedTabSize
        {
            get { return new Vector2(1180f, 640f); }
        }

        public override void PreOpen()
        {
            base.PreOpen();
            report = null;
        }

        void Refresh()
        {
            Map map = Find.CurrentMap;
            reportMap = map;
            reportTick = Find.TickManager.TicksGame;
            framesSinceRefresh = 0;
            report = map != null ? FarmCalc.Build(map) : null;
        }

        public override void DoWindowContents(Rect inRect)
        {
            framesSinceRefresh++;
            if (report == null || reportMap != Find.CurrentMap || Find.TickManager.TicksGame - reportTick >= RefreshTicks
                || framesSinceRefresh >= RefreshFrames)
                Refresh();
            if (report == null)
                return;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, 300f, 34f), "AgriStats_Title".Translate());
            Text.Font = GameFont.Small;

            // View switch: right now, or averaged over a year.
            Rect nowRect = new Rect(inRect.xMax - 330f, inRect.y + 2f, 160f, 28f);
            Rect yearRect = new Rect(inRect.xMax - 165f, inRect.y + 2f, 160f, 28f);
            if (Widgets.ButtonText(nowRect, (yearView ? "" : "> ") + "AgriStats_ViewNow".Translate()))
                yearView = false;
            if (Widgets.ButtonText(yearRect, (yearView ? "> " : "") + "AgriStats_ViewYear".Translate()))
                yearView = true;
            TooltipHandler.TipRegion(nowRect, "AgriStats_ViewNowDesc".Translate());
            TooltipHandler.TipRegion(yearRect, "AgriStats_ViewYearDesc".Translate());

            float y = inRect.y + 38f;
            GUI.color = Color.gray;
            string harvester = report.harvesterName != null
                ? "AgriStats_HarvesterBest".Translate(report.harvesterName, report.harvesterFactor.ToStringPercent())
                : "AgriStats_HarvesterFlat".Translate(report.harvesterFactor.ToStringPercent());
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 24f), harvester);
            GUI.color = Color.white;
            y += 26f;

            DrawHeader(new Rect(inRect.x, y, inRect.width, HeaderHeight));
            y += HeaderHeight;

            float footerHeight = 4 * RowHeight + 8f;
            Rect outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - footerHeight);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, report.rows.Count * RowHeight));
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            if (report.rows.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(8f, 4f, viewRect.width, 28f), "AgriStats_NoFarms".Translate());
                GUI.color = Color.white;
            }
            for (int i = 0; i < report.rows.Count; i++)
                DrawRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight), report.rows[i], i);
            Widgets.EndScrollView();

            DrawFooter(new Rect(inRect.x, inRect.yMax - footerHeight + 8f, inRect.width, footerHeight - 8f));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        static Rect Col(Rect row, int index)
        {
            float x = row.x;
            for (int i = 0; i < index; i++)
                x += Widths[i];
            float w = index < Widths.Length ? Widths[index] : row.xMax - x;
            return new Rect(x + 4f, row.y, w - 8f, row.height);
        }

        void DrawHeader(Rect rect)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            string[] keys = { "AgriStats_ColZone", "AgriStats_ColCrop", "AgriStats_ColCells", "AgriStats_ColFertility",
                "AgriStats_ColGrowth", "AgriStats_ColNutrition", "AgriStats_ColRaw", "AgriStats_ColCooked",
                "AgriStats_ColPaste", "AgriStats_ColNotes" };
            for (int i = 0; i < keys.Length; i++)
            {
                Rect c = Col(rect, i);
                Widgets.Label(c, keys[i].Translate());
                TooltipHandler.TipRegion(c, (keys[i] + "Desc").Translate());
            }
            GUI.color = Color.gray;
            Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);
            GUI.color = Color.white;
        }

        void DrawRow(Rect rect, FarmRow row, int index)
        {
            if (index % 2 == 1)
                Widgets.DrawLightHighlight(rect);
            Widgets.DrawHighlightIfMouseover(rect);
            Text.Anchor = TextAnchor.MiddleLeft;

            Widgets.Label(Col(rect, 0), row.label);

            Rect crop = Col(rect, 1);
            Widgets.DefIcon(new Rect(crop.x, crop.y + 2f, 24f, 24f), row.plant);
            Widgets.Label(new Rect(crop.x + 28f, crop.y, crop.width - 28f, crop.height), row.plant.LabelCap);

            Widgets.Label(Col(rect, 2), row.cells.ToString());
            Widgets.Label(Col(rect, 3), row.avgFertility.ToStringPercent());
            Widgets.Label(Col(rect, 4), row.avgGrowth >= 0f ? row.avgGrowth.ToStringPercent() : "-");

            float nutrition = yearView ? row.nutritionYear : row.nutritionNow;
            if (row.kind == CropKind.Other)
            {
                GUI.color = Color.gray;
                Widgets.Label(Col(rect, 5), "-");
                GUI.color = Color.white;
            }
            else
            {
                Widgets.Label(Col(rect, 5), nutrition.ToString("0.00"));
                if (row.kind == CropKind.Food)
                {
                    Widgets.Label(Col(rect, 6), (nutrition / report.perPersonNeed).ToString("0.0"));
                    Widgets.Label(Col(rect, 7), (nutrition * report.cookedFactor / report.perPersonNeed).ToString("0.0"));
                    Widgets.Label(Col(rect, 8), (nutrition * report.pasteFactor / report.perPersonNeed).ToString("0.0"));
                }
                else
                {
                    GUI.color = Color.gray;
                    Widgets.Label(new Rect(Col(rect, 6).x, rect.y, Widths[6] + Widths[7] + Widths[8] - 8f, rect.height), "AgriStats_AnimalFeed".Translate());
                    GUI.color = Color.white;
                }
            }

            if (row.warnings.Count > 0)
            {
                GUI.color = WarnColor;
                Rect notes = Col(rect, 9);
                string text = string.Join(", ", row.warnings.ToArray());
                Widgets.Label(notes, text.Truncate(notes.width));
                TooltipHandler.TipRegion(notes, text);
                GUI.color = Color.white;
            }

            if (Widgets.ButtonInvisible(rect))
                JumpTo(row);
        }

        void JumpTo(FarmRow row)
        {
            if (!row.jumpCell.IsValid || reportMap == null)
                return;
            Find.MainTabsRoot.EscapeCurrentTab(false);
            CameraJumper.TryJump(row.jumpCell, reportMap);
            if (row.zone != null)
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(row.zone);
            }
        }

        void DrawFooter(Rect rect)
        {
            GUI.color = Color.gray;
            Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleLeft;

            float food = yearView ? report.foodYear : report.foodNow;
            float raw = food / report.perPersonNeed;
            float cooked = raw * report.cookedFactor;
            float paste = raw * report.pasteFactor;

            // Totals.
            Rect line = new Rect(rect.x, rect.y + 2f, rect.width, RowHeight);
            Widgets.Label(Col(line, 0), "AgriStats_TotalFood".Translate());
            Widgets.Label(Col(line, 5), food.ToString("0.00"));
            Widgets.Label(Col(line, 6), raw.ToString("0.0"));
            Widgets.Label(Col(line, 7), cooked.ToString("0.0"));
            Widgets.Label(Col(line, 8), paste.ToString("0.0"));

            // What the colony eats.
            line.y += RowHeight;
            Widgets.Label(new Rect(Col(line, 0).x, line.y, Widths[0] + Widths[1] + Widths[2] + Widths[3] + Widths[4] - 8f, line.height),
                "AgriStats_ColonyEats".Translate(report.people));
            Widgets.Label(Col(line, 5), report.peopleNeed.ToString("0.00"));
            Widgets.Label(Col(line, 6), report.people.ToString());
            Widgets.Label(Col(line, 7), report.people.ToString());
            Widgets.Label(Col(line, 8), report.people.ToString());

            // Surplus or deficit, in people.
            line.y += RowHeight;
            Widgets.Label(Col(line, 0), "AgriStats_Surplus".Translate());
            Balance(Col(line, 6), raw - report.people);
            Balance(Col(line, 7), cooked - report.people);
            Balance(Col(line, 8), paste - report.people);

            // Animal feed against the animals that eat it.
            line.y += RowHeight;
            float feed = yearView ? report.feedYear : report.feedNow;
            GUI.color = Color.gray;
            string animals = report.animals > 0
                ? "AgriStats_AnimalLine".Translate(feed.ToString("0.00"), report.animals, report.animalNeed.ToString("0.00"))
                : "AgriStats_AnimalLineNone".Translate(feed.ToString("0.00"));
            Widgets.Label(new Rect(Col(line, 0).x, line.y, rect.width - 8f, line.height), animals);
            GUI.color = Color.white;
        }

        static void Balance(Rect rect, float people)
        {
            GUI.color = people >= 0f ? GoodColor : BadColor;
            Widgets.Label(rect, (people >= 0f ? "+" : "") + people.ToString("0.0"));
            GUI.color = Color.white;
        }
    }
}
