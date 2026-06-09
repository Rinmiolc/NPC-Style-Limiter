// Copyright (c) 2026 rinmiolc
// Licensed under the GNU General Public License v3.0.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace NPCStyleLimiter
{
    public class CustomizerMod : Mod
    {
        public static CustomizerSettings Settings;
        public static CustomizerMod Instance;

        public CustomizerMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<CustomizerSettings>();
            Settings.InitializeSets();
        }

        public override string SettingsCategory() => "NPCStyleLimiter_Category".Translate();

        private Vector2 scrollPosition = Vector2.zero;
        private string hairSearchQuery = "";
        private string beardSearchQuery = "";
        private string apparelSearchQuery = "";
        private int activeTab = 0; // 0: Hair, 1: Beard, 2: Apparel, 3: Body Types
        
        private string selectedModName = "All";
        private Gender editingGender = Gender.Male;

        private readonly List<Def> cachedFilteredDefs = new List<Def>();
        private int lastActiveTab = -1;
        private string lastModFilter = null;
        private string lastHairSearch = null;
        private string lastBeardSearch = null;
        private string lastApparelSearch = null;
        private bool cacheInitialized = false;

        private List<HairDef> allHairs = null;
        private List<BeardDef> allBeards = null;
        private List<ThingDef> allApparels = null;
        private List<BodyTypeDef> allBodyTypes = null;

        // Modern Theme
        private static readonly Color AccentColor = new Color(0.78f, 0.55f, 0.15f);
        private static readonly Color InactiveTextColor = new Color(0.55f, 0.6f, 0.62f);
        private static readonly Color SliderTrackColor = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color PanelBgColor = new Color(1f, 1f, 1f, 0.03f);
        private static readonly Color HoverRowColor = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color CardBgColor = new Color(0f, 0f, 0f, 0.15f);

        private void RebuildFilterCache()
        {
            cachedFilteredDefs.Clear();
            string search = (activeTab == 0) ? hairSearchQuery : (activeTab == 1 ? beardSearchQuery : apparelSearchQuery);

            if (activeTab == 0)
            {
                if (allHairs == null) allHairs = DefDatabase<HairDef>.AllDefsListForReading.Where(d => d != null && d.defName != "Bald").OrderBy(d => d.label).ToList();
                ApplyFilters(allHairs, search);
            }
            else if (activeTab == 1)
            {
                if (allBeards == null) allBeards = DefDatabase<BeardDef>.AllDefsListForReading.Where(d => d != null && d.defName != "NoBeard").OrderBy(d => d.label).ToList();
                ApplyFilters(allBeards, search);
            }
            else if (activeTab == 2)
            {
                if (allApparels == null) allApparels = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d != null && d.IsApparel).OrderBy(d => d.label).ToList();
                ApplyFilters(allApparels, search);
            }
            else if (activeTab == 3)
            {
                if (allBodyTypes == null) allBodyTypes = DefDatabase<BodyTypeDef>.AllDefsListForReading.Where(d => d != null && d.defName != "Baby" && d.defName != "Child").OrderBy(d => d.label ?? d.defName).ToList();
                cachedFilteredDefs.AddRange(allBodyTypes);
            }
        }

        private void ApplyFilters<T>(List<T> source, string search) where T : Def
        {
            foreach (var def in source)
            {
                if (selectedModName != "All" && (def.modContentPack?.Name ?? "Core") != selectedModName) continue;
                if (!search.NullOrEmpty())
                {
                    if ((def.label?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) < 0 && 
                        (def.defName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) < 0) continue;
                }
                cachedFilteredDefs.Add(def);
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.InitializeSets();
            Gender activeGender = Settings.useGenderConfig ? editingGender : Gender.None;

            if (!cacheInitialized || activeTab != lastActiveTab || selectedModName != lastModFilter || 
                hairSearchQuery != lastHairSearch || beardSearchQuery != lastBeardSearch || apparelSearchQuery != lastApparelSearch)
            {
                RebuildFilterCache();
                lastActiveTab = activeTab; lastModFilter = selectedModName;
                lastHairSearch = hairSearchQuery; lastBeardSearch = beardSearchQuery; lastApparelSearch = apparelSearchQuery;
                cacheInitialized = true;
            }

            // --- HEADER: DASHBOARD CARD ---
            float headerHeight = Settings.adjustGenderRatio ? 72f : 44f;
            Rect headerCard = new Rect(inRect.x, inRect.y, inRect.width, headerHeight);
            Widgets.DrawRectFast(headerCard, CardBgColor);

            // Row 1: Primary Controls
            float curX = headerCard.x + 12f;
            float curY = headerCard.y + 10f;

            // Profile Badge
            string profileLabel = "NPCStyleLimiter_ProfileLabel".Translate() + ": ";
            string profileName = Settings.currentProfileName ?? "Default";
            float labelWidth = Text.CalcSize(profileLabel).x;
            float nameWidth = Text.CalcSize(profileName).x;
            
            Rect profileBadge = new Rect(headerCard.xMax - labelWidth - nameWidth - 12f, curY, labelWidth + nameWidth, 24f);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = InactiveTextColor;
            Widgets.Label(new Rect(profileBadge.x, profileBadge.y, labelWidth, 24f), profileLabel);
            GUI.color = AccentColor;
            Widgets.Label(new Rect(profileBadge.x + labelWidth, profileBadge.y, nameWidth, 24f), profileName);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Gender Config Toggle
            Rect checkRect = new Rect(curX, curY, 160f, 24f);
            bool useGender = Settings.useGenderConfig;
            Widgets.CheckboxLabeled(checkRect, "NPCStyleLimiter_UseGenderConfig".Translate(), ref useGender);
            if (useGender != Settings.useGenderConfig) Settings.useGenderConfig = useGender;
            curX += 170f;

            if (Settings.useGenderConfig)
            {
                Rect maleBtn = new Rect(curX, curY, 60f, 24f);
                Rect femaleBtn = new Rect(maleBtn.xMax + 4f, curY, 60f, 24f);
                if (Widgets.ButtonText(maleBtn, "NPCStyleLimiter_Male".Translate(), editingGender == Gender.Male, true, true)) editingGender = Gender.Male;
                if (Widgets.ButtonText(femaleBtn, "NPCStyleLimiter_Female".Translate(), editingGender == Gender.Female, true, true)) editingGender = Gender.Female;
                curX = femaleBtn.xMax + 15f;
            }

            // Gender Ratio Toggle
            Rect ratioToggleRect = new Rect(curX, curY, 150f, 24f);
            bool adjRatio = Settings.adjustGenderRatio;
            Widgets.CheckboxLabeled(ratioToggleRect, "NPCStyleLimiter_AdjustGenderRatio".Translate(), ref adjRatio);
            if (adjRatio != Settings.adjustGenderRatio) Settings.adjustGenderRatio = adjRatio;
            curX += 160f;

            // Debug Mode Toggle
            Rect debugRect = new Rect(curX, curY, 100f, 24f);
            Widgets.CheckboxLabeled(debugRect, "DEBUG", ref Settings.debugMode);

            // Row 2: Ratio Slider
            if (Settings.adjustGenderRatio)
            {
                curY += 28f;
                Text.Anchor = TextAnchor.MiddleLeft;
                
                Rect sliderRect = new Rect(headerCard.x + 12f, curY, 200f, 24f);
                float newRatio = DrawCustomSlider(sliderRect, Settings.maleRatio, 0f, 1f, 0.01f);
                if (newRatio != Settings.maleRatio) Settings.maleRatio = newRatio;
                
                Rect valLabel = new Rect(sliderRect.xMax + 15f, curY, 150f, 24f);
                Widgets.Label(valLabel, "NPCStyleLimiter_MaleRatioLabel".Translate(Settings.maleRatio.ToString("P0"), (1f - Settings.maleRatio).ToString("P0")));
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // --- TABS ---
            curY = headerCard.yMax + 12f;
            Rect tabsRect = new Rect(inRect.x, curY, inRect.width, 38f);
            float tW = tabsRect.width / 4f;
            if (DrawModernTab(new Rect(tabsRect.x, curY, tW, 38f), "NPCStyleLimiter_HairStyles".Translate(), activeTab == 0)) activeTab = 0;
            if (DrawModernTab(new Rect(tabsRect.x + tW, curY, tW, 38f), "NPCStyleLimiter_BeardStyles".Translate(), activeTab == 1)) activeTab = 1;
            if (DrawModernTab(new Rect(tabsRect.x + tW * 2, curY, tW, 38f), "NPCStyleLimiter_Apparel".Translate(), activeTab == 2)) activeTab = 2;
            if (DrawModernTab(new Rect(tabsRect.x + tW * 3, curY, tW, 38f), "NPCStyleLimiter_BodyTypes".Translate(), activeTab == 3)) activeTab = 3;

            // --- FILTER BAR ---
            curY = tabsRect.yMax + 10f;
            if (activeTab != 3)
            {
                Rect filterBar = new Rect(inRect.x, curY, inRect.width, 40f);
                Widgets.DrawRectFast(filterBar, PanelBgColor);
                
                Rect searchRect = new Rect(filterBar.x + 10f, filterBar.y + 6f, 180f, 28f);
                string s = (activeTab == 0) ? hairSearchQuery : (activeTab == 1 ? beardSearchQuery : apparelSearchQuery);
                string ns = Widgets.TextField(searchRect, s);
                if (activeTab == 0) hairSearchQuery = ns; else if (activeTab == 1) beardSearchQuery = ns; else apparelSearchQuery = ns;

                Rect modBtn = new Rect(searchRect.xMax + 10f, filterBar.y + 6f, 150f, 28f);
                string modLabel = selectedModName == "All" ? "NPCStyleLimiter_AllMods".Translate().ToString() : selectedModName;
                if (Widgets.ButtonText(modBtn, modLabel))
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption> { new FloatMenuOption("NPCStyleLimiter_AllMods".Translate(), () => selectedModName = "All") };
                    HashSet<string> mods = new HashSet<string>();
                    if (activeTab == 0) foreach (var hd in DefDatabase<HairDef>.AllDefsListForReading) if (hd != null) mods.Add(hd.modContentPack?.Name ?? "Core");
                    else if (activeTab == 1) foreach (var bd in DefDatabase<BeardDef>.AllDefsListForReading) if (bd != null) mods.Add(bd.modContentPack?.Name ?? "Core");
                    else if (activeTab == 2) foreach (var td in DefDatabase<ThingDef>.AllDefsListForReading) if (td != null && td.IsApparel) mods.Add(td.modContentPack?.Name ?? "Core");
                    foreach (var m in mods.OrderBy(x => x)) opts.Add(new FloatMenuOption(m, () => selectedModName = m));
                    Find.WindowStack.Add(new FloatMenu(opts));
                }

                Rect bulkAllow = new Rect(filterBar.xMax - 180f, filterBar.y + 6f, 85f, 28f);
                Rect bulkBlock = new Rect(bulkAllow.xMax + 4f, filterBar.y + 6f, 85f, 28f);
                if (Widgets.ButtonText(bulkAllow, "NPCStyleLimiter_AllowAll".Translate())) foreach (var cdef in cachedFilteredDefs) Settings.SetWeight(cdef, activeGender, 1.0f);
                if (Widgets.ButtonText(bulkBlock, "NPCStyleLimiter_BlockAll".Translate())) foreach (var cdef in cachedFilteredDefs) Settings.SetWeight(cdef, activeGender, 0.0f);
                
                curY = filterBar.yMax + 5f;
            }

            // --- MAIN LIST ---
            DrawModernList(cachedFilteredDefs, curY, inRect, activeGender);

            // --- BOTTOM BAR ---
            Rect bottomBar = new Rect(inRect.x, inRect.yMax - 40f, inRect.width, 40f);
            float bW = (bottomBar.width - 6f) / 2f;
            if (Widgets.ButtonText(new Rect(bottomBar.x, bottomBar.y + 5f, bW, 32f), "NPCStyleLimiter_SaveProfile".Translate())) Find.WindowStack.Add(new Dialog_ManageConfigs(true));
            if (Widgets.ButtonText(new Rect(bottomBar.xMax - bW, bottomBar.y + 5f, bW, 32f), "NPCStyleLimiter_LoadManageProfiles".Translate())) Find.WindowStack.Add(new Dialog_ManageConfigs());
        }

        private void DrawModernList(List<Def> defs, float y, Rect inRect, Gender gender)
        {
            float h = inRect.yMax - y - 45f;
            Rect outer = new Rect(inRect.x, y, inRect.width, h);
            if (defs.Count == 0) { Widgets.Label(outer, "NPCStyleLimiter_NoResultsFound".Translate()); return; }

            float rH = 40f;
            Rect view = new Rect(0, 0, outer.width - 16f, defs.Count * rH);
            Widgets.BeginScrollView(outer, ref scrollPosition, view);
            
            int start = Mathf.FloorToInt(scrollPosition.y / rH);
            int end = Mathf.CeilToInt((scrollPosition.y + h) / rH);
            start = Mathf.Clamp(start, 0, defs.Count - 1);
            end = Mathf.Clamp(end, 0, defs.Count - 1);

            for (int i = start; i <= end; i++)
            {
                Def d = defs[i];
                Rect row = new Rect(0, i * rH, view.width, rH);
                if (Mouse.IsOver(row)) Widgets.DrawRectFast(row, HoverRowColor);
                else if (i % 2 == 1) Widgets.DrawLightHighlight(row);

                float w = Settings.GetWeight(d, gender);
                
                // Toggle
                Rect tRect = new Rect(8f, row.y + (rH - 20f) / 2f, 36f, 20f);
                bool active = DrawCustomCheckbox(tRect, w > 0f);
                if (active != (w > 0f)) Settings.SetWeight(d, gender, active ? 1.0f : 0.0f);

                // Icon
                Texture2D tex = (d is StyleItemDef s) ? s.Icon : (d is ThingDef t ? t.uiIcon : null);
                if (tex != null) {
                    GUI.color = (d is ThingDef t2) ? t2.uiIconColor : Color.white;
                    Widgets.DrawTextureFitted(new Rect(50f, row.y + 5f, 30f, 30f), tex, 1f);
                    GUI.color = Color.white;
                }

                // Label
                Text.Anchor = TextAnchor.MiddleLeft;
                string label = d.LabelCap.NullOrEmpty() ? d.defName : (string)d.LabelCap;
                if (Settings.debugMode) label += " [" + d.defName + "]";
                Widgets.Label(new Rect(90f, row.y, view.width * 0.45f, rH), label);
                
                // Weight Slider
                Rect sRect = new Rect(view.width * 0.55f, row.y + (rH - 24f) / 2f, 120f, 24f);
                float nw = DrawCustomSlider(sRect, w, 0f, 5f, 0.1f);
                if (nw != w) Settings.SetWeight(d, gender, nw);
                
                Widgets.Label(new Rect(sRect.xMax + 8f, row.y, 40f, rH), nw.ToString("0.0") + "x");
                
                // Mod Name
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = InactiveTextColor;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(view.width - 150f, row.y, 140f, rH), d.modContentPack?.Name ?? "Core");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            Widgets.EndScrollView();
        }

        private bool DrawModernTab(Rect rect, string label, bool selected)
        {
            if (Mouse.IsOver(rect)) Widgets.DrawRectFast(rect, HoverRowColor);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = selected ? AccentColor : InactiveTextColor;
            Widgets.Label(rect, label);
            if (selected) Widgets.DrawRectFast(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), AccentColor);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            return Widgets.ButtonInvisible(rect);
        }

        private float DrawCustomSlider(Rect rect, float val, float min, float max, float step = -1f)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            Event ev = Event.current;

            if (ev.type == EventType.MouseDown && Mouse.IsOver(rect)) { GUIUtility.hotControl = id; ev.Use(); }
            if (GUIUtility.hotControl == id)
            {
                if (ev.type == EventType.MouseDrag || ev.type == EventType.MouseDown)
                {
                    val = Mathf.Clamp(min + (max - min) * ((ev.mousePosition.x - rect.x) / rect.width), min, max);
                    if (step > 0) val = Mathf.Round(val / step) * step;
                    ev.Use();
                }
                else if (ev.type == EventType.MouseUp) { GUIUtility.hotControl = 0; ev.Use(); }
            }

            float tH = 4f; Rect tR = new Rect(rect.x, rect.y + (rect.height - tH) / 2f, rect.width, tH);
            Widgets.DrawRectFast(tR, SliderTrackColor);
            float p = Mathf.Clamp01((val - min) / (max - min));
            if (p > 0) Widgets.DrawRectFast(new Rect(tR.x, tR.y, tR.width * p, tH), AccentColor);
            
            Rect kR = new Rect(tR.x + tR.width * p - 5f, rect.y + (rect.height - 14f) / 2f, 10f, 14f);
            Widgets.DrawRectFast(kR, (GUIUtility.hotControl == id || Mouse.IsOver(rect)) ? Color.white : new Color(0.8f, 0.8f, 0.8f));

            return val;
        }

        private bool DrawCustomCheckbox(Rect rect, bool active)
        {
            Widgets.DrawRectFast(rect, active ? AccentColor : new Color(0.2f, 0.2f, 0.2f));
            float kS = 14f;
            Rect kR = new Rect(active ? (rect.xMax - kS - 3f) : (rect.x + 3f), rect.y + (rect.height - kS) / 2f, kS, kS);
            Widgets.DrawRectFast(kR, Color.white);
            return Widgets.ButtonInvisible(rect) ? !active : active;
        }
    }

    [StaticConstructorOnStartup]
    public static class ModStartup
    {
        static ModStartup()
        {
            new Harmony("rinmiolc.npcstylelimiter").PatchAll();
            if (CustomizerMod.Settings != null) CustomizerMod.Settings.ResolveRuntimeWeights();
        }
    }
}
