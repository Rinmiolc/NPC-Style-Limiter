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
        private Vector2 raceScrollPos = Vector2.zero;
        private Vector2 homeScrollPos = Vector2.zero;
        private string hairSearchQuery = "";
        private string beardSearchQuery = "";
        private string apparelSearchQuery = "";
        private int activeTab = 0; // 0: Hair, 1: Beard, 2: Apparel, 3: Body Types
        
        private string selectedModName = "All";
        private Gender editingGender = Gender.Male;
        private string selectedRaceDefName = "Home";

        private readonly List<Def> cachedFilteredDefs = new List<Def>();
        private int lastActiveTab = -1;
        private string lastModFilter = null;
        private string lastHairSearch = null;
        private string lastBeardSearch = null;
        private string lastApparelSearch = null;
        private string lastRaceDefName = null;
        private bool cacheInitialized = false;

        private List<HairDef> allHairs = null;
        private List<BeardDef> allBeards = null;
        private List<ThingDef> allApparels = null;
        private List<BodyTypeDef> allBodyTypes = null;
        private List<ThingDef> allRaces = null;

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

            if (allRaces == null)
            {
                allRaces = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.category == ThingCategory.Pawn && d.race != null && d.race.Humanlike)
                    .OrderBy(d => d.label)
                    .ToList();
            }

            // --- LAYOUT ---
            float sidebarWidth = 200f;
            Rect sidebarRect = new Rect(inRect.x, inRect.y, sidebarWidth, inRect.height);
            Rect contentRect = new Rect(sidebarRect.xMax + 10f, inRect.y, inRect.width - sidebarWidth - 10f, inRect.height);

            // --- SIDEBAR: RACE SELECTOR ---
            DrawRaceSidebar(sidebarRect);

            // --- CONTENT AREA ---
            DrawMainContent(contentRect);
        }

        private void DrawRaceSidebar(Rect rect)
        {
            Widgets.DrawRectFast(rect, CardBgColor);
            
            Rect viewRect = new Rect(0, 0, rect.width - 16f, 48f + (allRaces.Count + 2) * 32f);
            Widgets.BeginScrollView(rect, ref raceScrollPos, viewRect);
            
            float curY = 0;

            // Group 1: General/Home
            Text.Font = GameFont.Tiny;
            GUI.color = InactiveTextColor;
            Widgets.Label(new Rect(8f, curY + 6f, viewRect.width - 16f, 20f), "NPCStyleLimiter_SidebarGroupHome".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            curY += 24f;

            // Home / Guide Option
            if (DrawRaceRow(new Rect(0, curY, viewRect.width, 30f), "NPCStyleLimiter_HomeTab".Translate(), selectedRaceDefName == "Home", false))
            {
                selectedRaceDefName = "Home";
            }
            curY += 32f;

            // Group 2: Configurations
            Text.Font = GameFont.Tiny;
            GUI.color = InactiveTextColor;
            Widgets.Label(new Rect(8f, curY + 6f, viewRect.width - 16f, 20f), "NPCStyleLimiter_SidebarGroupConfig".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            curY += 24f;

            // Global Option
            if (DrawRaceRow(new Rect(0, curY, viewRect.width, 30f), "NPCStyleLimiter_GlobalConfig".Translate(), selectedRaceDefName == CustomizerSettings.GlobalKey, true))
            {
                selectedRaceDefName = CustomizerSettings.GlobalKey;
            }
            curY += 32f;

            foreach (var race in allRaces)
            {
                bool hasSpecific = Settings.raceSettings.TryGetValue(race.defName, out var s) && s.useSpecificConfig;
                
                string displayLabel = race.LabelCap;
                if (race.defName != "Human" && (race.label == "Human" || race.label == "人类" || race.defName == "CreepJoiner"))
                {
                    displayLabel += " (" + race.defName + ")";
                }

                if (DrawRaceRow(new Rect(0, curY, viewRect.width, 30f), displayLabel, selectedRaceDefName == race.defName, hasSpecific))
                {
                    selectedRaceDefName = race.defName;
                }
                curY += 32f;
            }

            Widgets.EndScrollView();
        }

        private bool DrawRaceRow(Rect rect, string label, bool selected, bool active)
        {
            if (Mouse.IsOver(rect)) Widgets.DrawRectFast(rect, HoverRowColor);
            if (selected) Widgets.DrawRectFast(new Rect(rect.x + 8f, rect.y, 3f, rect.height), AccentColor);
            
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = selected ? Color.white : InactiveTextColor;
            Widgets.Label(new Rect(rect.x + 20f, rect.y, rect.width - 36f, rect.height), label);
            
            if (active)
            {
                GUI.color = AccentColor;
                Widgets.Label(new Rect(rect.xMax - 15f, rect.y, 15f, rect.height), "●");
            }
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            return Widgets.ButtonInvisible(rect);
        }

        private void DrawMainContent(Rect inRect)
        {
            if (selectedRaceDefName == "Home")
            {
                DrawHomeContent(inRect);
                return;
            }

            var raceSettings = Settings.GetSettingsForRaceRaw(selectedRaceDefName);
            bool isGlobal = selectedRaceDefName == CustomizerSettings.GlobalKey;
            
            Gender activeGender = raceSettings.useGenderConfig ? editingGender : Gender.None;

            if (!cacheInitialized || activeTab != lastActiveTab || selectedModName != lastModFilter || 
                hairSearchQuery != lastHairSearch || beardSearchQuery != lastBeardSearch || apparelSearchQuery != lastApparelSearch ||
                selectedRaceDefName != lastRaceDefName)
            {
                RebuildFilterCache();
                lastActiveTab = activeTab; lastModFilter = selectedModName;
                lastHairSearch = hairSearchQuery; lastBeardSearch = beardSearchQuery; lastApparelSearch = apparelSearchQuery;
                lastRaceDefName = selectedRaceDefName;
                cacheInitialized = true;
            }

            // --- HEADER: DASHBOARD CARD ---
            float headerHeight = 95f; // Fixed height supporting better titles & 2x2 layout
            Rect headerCard = new Rect(inRect.x, inRect.y, inRect.width, headerHeight);
            Widgets.DrawRectFast(headerCard, CardBgColor);

            // 1. Title and Subtitle (Left Area)
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;
            GUI.color = AccentColor;
            string titleText = isGlobal ? "NPCStyleLimiter_GlobalConfig".Translate().ToString() : "";
            string subText = isGlobal ? "NPCStyleLimiter_GlobalConfigSub".Translate().ToString() : "";

            if (!isGlobal)
            {
                var raceDef = allRaces?.FirstOrDefault(r => r.defName == selectedRaceDefName);
                if (raceDef != null)
                {
                    titleText = raceDef.LabelCap;
                    if (raceDef.defName != "Human" && (raceDef.label == "Human" || raceDef.label == "人类" || raceDef.defName == "CreepJoiner"))
                    {
                        titleText += " (" + raceDef.defName + ")";
                    }
                    subText = "(" + selectedRaceDefName + ")";
                }
                else
                {
                    titleText = selectedRaceDefName;
                    subText = "";
                }
            }

            Widgets.Label(new Rect(headerCard.x + 16f, headerCard.y + 16f, 200f, 30f), titleText);
            
            Text.Font = GameFont.Tiny;
            GUI.color = InactiveTextColor;
            Widgets.Label(new Rect(headerCard.x + 16f, headerCard.y + 46f, 200f, 24f), subText);
            
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // 2. 2x2 Control Grid (Right Area)
            float rightAreaX = headerCard.x + 225f;
            float rightWidth = headerCard.width - 241f; // Margin on right
            
            float col1X = rightAreaX;
            float col2X = rightAreaX + 165f;
            
            float row1Y = headerCard.y + 14f;
            float row2Y = headerCard.y + 48f;

            // Row 1, Col 1: useSpecificConfig or applyToPlayerPawns
            if (!isGlobal)
            {
                Rect specificRect = new Rect(col1X, row1Y, 150f, 24f);
                bool specific = raceSettings.useSpecificConfig;
                bool nextSpecific = DrawCustomLabeledCheckbox(specificRect, "NPCStyleLimiter_UseSpecificConfig".Translate(), specific);
                if (nextSpecific != specific)
                {
                    raceSettings.useSpecificConfig = nextSpecific;
                    Settings.ResolveRuntimeWeights();
                }
            }
            else
            {
                Rect applyPlayerRect = new Rect(col1X, row1Y, 150f, 24f);
                bool applyPlayer = Settings.applyToPlayerPawns;
                bool nextApplyPlayer = DrawCustomLabeledCheckbox(applyPlayerRect, "NPCStyleLimiter_ApplyToPlayerPawns".Translate(), applyPlayer);
                if (nextApplyPlayer != applyPlayer) Settings.applyToPlayerPawns = nextApplyPlayer;
            }

            // Row 1, Col 2: useGenderConfig & gender tabs
            Rect genderRect = new Rect(col2X, row1Y, 120f, 24f);
            bool useGender = raceSettings.useGenderConfig;
            bool nextUseGender = DrawCustomLabeledCheckbox(genderRect, "NPCStyleLimiter_UseGenderConfig".Translate(), useGender);
            if (nextUseGender != useGender) 
            {
                raceSettings.useGenderConfig = nextUseGender;
                Settings.ResolveRuntimeWeights();
            }

            if (raceSettings.useGenderConfig)
            {
                float genderBtnStartX = col2X + 122f;
                Rect maleBtn = new Rect(genderBtnStartX, row1Y, 45f, 24f);
                Rect femaleBtn = new Rect(maleBtn.xMax + 4f, row1Y, 45f, 24f);
                if (Widgets.ButtonText(maleBtn, "NPCStyleLimiter_Male".Translate(), editingGender == Gender.Male, true, true)) editingGender = Gender.Male;
                if (Widgets.ButtonText(femaleBtn, "NPCStyleLimiter_Female".Translate(), editingGender == Gender.Female, true, true)) editingGender = Gender.Female;
            }

            // Row 2, Col 1: adjustGenderRatio
            Rect ratioToggleRect = new Rect(col1X, row2Y, 150f, 24f);
            bool adjRatio = raceSettings.adjustGenderRatio;
            bool nextAdjRatio = DrawCustomLabeledCheckbox(ratioToggleRect, "NPCStyleLimiter_AdjustGenderRatio".Translate(), adjRatio);
            if (nextAdjRatio != adjRatio) raceSettings.adjustGenderRatio = nextAdjRatio;

            // Row 2, Col 2: maleRatio slider (if adjustGenderRatio active)
            if (raceSettings.adjustGenderRatio)
            {
                Rect sliderRect = new Rect(col2X, row2Y + 2f, 100f, 20f);
                float newRatio = DrawCustomSlider(sliderRect, raceSettings.maleRatio, 0f, 1f, 0.01f);
                if (newRatio != raceSettings.maleRatio) raceSettings.maleRatio = newRatio;
                
                Rect valLabel = new Rect(sliderRect.xMax + 8f, row2Y, rightWidth - (col2X - col1X) - 108f, 24f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Tiny;
                Widgets.Label(valLabel, "NPCStyleLimiter_MaleRatioLabel".Translate(raceSettings.maleRatio.ToString("P0"), (1f - raceSettings.maleRatio).ToString("P0")));
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // Inherited state handling (if not global and specific config is disabled)
            if (!isGlobal && !raceSettings.useSpecificConfig)
            {
                float inheritedContentY = headerCard.yMax + 12f;
                Rect infoRect = new Rect(inRect.x, inheritedContentY + 30f, inRect.width, 100f);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = InactiveTextColor;
                Widgets.Label(infoRect, "NPCStyleLimiter_InheritingGlobal".Translate());
                if (Widgets.ButtonText(new Rect(inRect.x + (inRect.width - 200f) / 2f, infoRect.yMax, 200f, 32f), "NPCStyleLimiter_CopyFromGlobal".Translate()))
                {
                    CopyGlobalToSpecific(raceSettings);
                }
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // --- TABS ---
            float curY = headerCard.yMax + 12f;
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
                if (Widgets.ButtonText(bulkAllow, "NPCStyleLimiter_AllowAll".Translate())) foreach (var cdef in cachedFilteredDefs) Settings.SetWeight(cdef, activeGender, 1.0f, selectedRaceDefName);
                if (Widgets.ButtonText(bulkBlock, "NPCStyleLimiter_BlockAll".Translate())) foreach (var cdef in cachedFilteredDefs) Settings.SetWeight(cdef, activeGender, 0.0f, selectedRaceDefName);
                
                curY = filterBar.yMax + 5f;
            }

            // --- MAIN LIST ---
            DrawModernList(cachedFilteredDefs, curY, inRect, activeGender);

            // --- BOTTOM CONFIG BUTTONS ---
            Rect bottomBar = new Rect(inRect.x, inRect.yMax - 36f, inRect.width, 36f);
            float bW = (bottomBar.width - 6f) / 2f;
            if (Widgets.ButtonText(new Rect(bottomBar.x, bottomBar.y + 2f, bW, 32f), "NPCStyleLimiter_SaveProfile".Translate())) Find.WindowStack.Add(new Dialog_ManageConfigs(true));
            if (Widgets.ButtonText(new Rect(bottomBar.xMax - bW, bottomBar.y + 2f, bW, 32f), "NPCStyleLimiter_LoadManageProfiles".Translate())) Find.WindowStack.Add(new Dialog_ManageConfigs());
        }

        private void CopyGlobalToSpecific(RaceSettings s)
        {
            var g = Settings.GlobalSettings;
            s.useSpecificConfig = true;
            s.useGenderConfig = g.useGenderConfig;
            s.adjustGenderRatio = g.adjustGenderRatio;
            s.maleRatio = g.maleRatio;
            s.weights = new Dictionary<string, float>(g.weights);
            s.weightsMale = new Dictionary<string, float>(g.weightsMale);
            s.weightsFemale = new Dictionary<string, float>(g.weightsFemale);
            Settings.ResolveRuntimeWeights();
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

                float w = Settings.GetWeight(d, gender, selectedRaceDefName);
                
                // Toggle
                Rect tRect = new Rect(8f, row.y + (rH - 20f) / 2f, 36f, 20f);
                bool active = DrawCustomCheckbox(tRect, w > 0f);
                if (active != (w > 0f)) Settings.SetWeight(d, gender, active ? 1.0f : 0.0f, selectedRaceDefName);

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
                if (Settings.debugMode || Prefs.DevMode) label += " [" + d.defName + "]";
                Widgets.Label(new Rect(90f, row.y, view.width * 0.45f, rH), label);
                
                // Weight Slider
                Rect sRect = new Rect(view.width * 0.55f, row.y + (rH - 24f) / 2f, 120f, 24f);
                float nw = DrawCustomSlider(sRect, w, 0f, 5f, 0.1f);
                if (nw != w) Settings.SetWeight(d, gender, nw, selectedRaceDefName);
                
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

        private bool DrawCustomLabeledCheckbox(Rect rect, string label, bool active)
        {
            Rect tRect = new Rect(rect.x, rect.y + (rect.height - 20f) / 2f, 36f, 20f);
            bool nextState = DrawCustomCheckbox(tRect, active);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(tRect.xMax + 8f, rect.y, rect.width - 44f, rect.height), label);
            Text.Anchor = TextAnchor.UpperLeft;
            return nextState;
        }

        private void DrawHomeContent(Rect inRect)
        {
            // banner Card
            float bannerHeight = 75f;
            Rect bannerCard = new Rect(inRect.x, inRect.y, inRect.width, bannerHeight);
            Widgets.DrawRectFast(bannerCard, CardBgColor);
            Widgets.DrawRectFast(new Rect(bannerCard.x, bannerCard.yMax - 2f, bannerCard.width, 2f), AccentColor);

            // Title inside banner
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;
            GUI.color = AccentColor;
            Widgets.Label(new Rect(bannerCard.x + 16f, bannerCard.y + 12f, bannerCard.width - 32f, 30f), "NPC Style Limiter");
            
            Text.Font = GameFont.Tiny;
            GUI.color = InactiveTextColor;
            Widgets.Label(new Rect(bannerCard.x + 16f, bannerCard.y + 42f, bannerCard.width - 32f, 24f), "NPCStyleLimiter_HomeSubTitle".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Scrollable Content
            Rect outRect = new Rect(inRect.x, bannerCard.yMax + 10f, inRect.width, inRect.height - bannerHeight - 25f);
            Rect viewRect = new Rect(0, 0, outRect.width - 16f, 650f);
            Widgets.BeginScrollView(outRect, ref homeScrollPos, viewRect);

            float curY = 0f;

            // Section 1: Guide
            Rect guideCard = new Rect(0, curY, viewRect.width, 175f);
            Widgets.DrawRectFast(guideCard, PanelBgColor);
            Widgets.DrawRectFast(new Rect(guideCard.x, guideCard.y, 3f, guideCard.height), AccentColor);
            DrawSectionHeader(guideCard.x + 16f, guideCard.y + 10f, "NPCStyleLimiter_HomeGuideTitle".Translate(), AccentColor);
            
            float textY = guideCard.y + 36f;
            DrawBulletPoint(guideCard.x + 20f, textY, "NPCStyleLimiter_HomeGuide1".Translate()); textY += 24f;
            DrawBulletPoint(guideCard.x + 20f, textY, "NPCStyleLimiter_HomeGuide2".Translate()); textY += 24f;
            DrawBulletPoint(guideCard.x + 20f, textY, "NPCStyleLimiter_HomeGuide3".Translate()); textY += 24f;
            DrawBulletPoint(guideCard.x + 20f, textY, "NPCStyleLimiter_HomeGuide4".Translate()); textY += 24f;
            DrawBulletPoint(guideCard.x + 20f, textY, "NPCStyleLimiter_HomeGuide5".Translate());
            
            curY += 187f;

            // Section 2: Limitations (Zero-Weight)
            Rect limitCard = new Rect(0, curY, viewRect.width, 135f);
            Widgets.DrawRectFast(limitCard, PanelBgColor);
            Color orangeRed = new Color(0.88f, 0.35f, 0.15f);
            Widgets.DrawRectFast(new Rect(limitCard.x, limitCard.y, 3f, limitCard.height), orangeRed);
            DrawSectionHeader(limitCard.x + 16f, limitCard.y + 10f, "⚠️ " + "NPCStyleLimiter_HomeLimitTitle".Translate(), orangeRed);
            
            float limitTextY = limitCard.y + 36f;
            Rect limitTextRect = new Rect(limitCard.x + 20f, limitTextY, limitCard.width - 32f, 85f);
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.9f, 0.85f, 0.75f);
            Widgets.Label(limitTextRect, "NPCStyleLimiter_HomeLimitDesc".Translate());
            GUI.color = Color.white;

            curY += 147f;

            // Section 3: Safety valve
            Rect safetyCard = new Rect(0, curY, viewRect.width, 135f);
            Widgets.DrawRectFast(safetyCard, PanelBgColor);
            Color softGreen = new Color(0.2f, 0.7f, 0.35f);
            Widgets.DrawRectFast(new Rect(safetyCard.x, safetyCard.y, 3f, safetyCard.height), softGreen);
            DrawSectionHeader(safetyCard.x + 16f, safetyCard.y + 10f, "🛡️ " + "NPCStyleLimiter_HomeSafetyTitle".Translate(), softGreen);

            float safetyTextY = safetyCard.y + 36f;
            Rect safetyTextRect = new Rect(safetyCard.x + 20f, safetyTextY, safetyCard.width - 32f, 85f);
            GUI.color = new Color(0.8f, 0.9f, 0.8f);
            Widgets.Label(safetyTextRect, "NPCStyleLimiter_HomeSafetyDesc".Translate());
            GUI.color = Color.white;

            curY += 147f;

            // Section 4: Alien Spawning limitations
            Rect alienCard = new Rect(0, curY, viewRect.width, 135f);
            Widgets.DrawRectFast(alienCard, PanelBgColor);
            Color softPurple = new Color(0.7f, 0.45f, 0.85f);
            Widgets.DrawRectFast(new Rect(alienCard.x, alienCard.y, 3f, alienCard.height), softPurple);
            DrawSectionHeader(alienCard.x + 16f, alienCard.y + 10f, "NPCStyleLimiter_HomeAlienTitle".Translate(), softPurple);

            float alienTextY = alienCard.y + 36f;
            Rect alienTextRect = new Rect(alienCard.x + 20f, alienTextY, alienCard.width - 32f, 85f);
            GUI.color = new Color(0.85f, 0.8f, 0.9f);
            Widgets.Label(alienTextRect, "NPCStyleLimiter_HomeAlienDesc".Translate());
            GUI.color = Color.white;

            // Section 5: Footer Copyright
            float footerY = alienCard.yMax + 18f;
            Rect footerRect = new Rect(0, footerY, viewRect.width, 24f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = InactiveTextColor;
            Widgets.Label(footerRect, "NPC Style Limiter v1.6 | Copyright (c) 2026 rinmiolc | Licensed under GPL v3.0");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            Widgets.EndScrollView();
        }

        private void DrawSectionHeader(float x, float y, string title, Color color)
        {
            Text.Font = GameFont.Medium;
            GUI.color = color;
            Widgets.Label(new Rect(x, y, 400f, 28f), title);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawBulletPoint(float x, float y, string text)
        {
            GUI.color = AccentColor;
            Widgets.Label(new Rect(x, y, 16f, 22f), "•");
            GUI.color = Color.white;
            Widgets.Label(new Rect(x + 16f, y, 520f, 22f), text);
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
