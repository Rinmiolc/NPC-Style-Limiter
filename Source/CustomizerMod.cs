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

        public override string SettingsCategory()
        {
            return "NPCStyleLimiter_Category".Translate();
        }

        private Vector2 scrollPosition = Vector2.zero;
        private string hairSearchQuery = "";
        private string beardSearchQuery = "";
        private string apparelSearchQuery = "";
        private int activeTab = 0; // 0: Hair, 1: Beard, 2: Apparel, 3: Body Types
        
        // Mod filtering
        private string selectedModName = "All"; // "All" or mod name

        // State for gender config editing
        private Gender editingGender = Gender.Male;

        // UI caching state
        private readonly List<Def> cachedFilteredDefs = new List<Def>();
        private int lastActiveTab = -1;
        private string lastModFilter = null;
        private string lastHairSearch = null;
        private string lastBeardSearch = null;
        private string lastApparelSearch = null;
        private bool cacheInitialized = false;

        // Pre-cached full database lists for performance
        private List<HairDef> allHairs = null;
        private List<BeardDef> allBeards = null;
        private List<ThingDef> allApparels = null;
        private List<BodyTypeDef> allBodyTypes = null;

        // Custom Modern UI Theme Colors
        private static readonly Color AccentColor = new Color(0.78f, 0.55f, 0.15f); // Warm yellow-orange matching RimWorld UI
        private static readonly Color InactiveTextColor = new Color(0.55f, 0.6f, 0.62f); // Sleek slate grey
        private static readonly Color SliderTrackColor = new Color(0.12f, 0.12f, 0.12f); // Deep dark track
        private static readonly Color PanelBgColor = new Color(1f, 1f, 1f, 0.02f); // High-contrast translucent overlay
        private static readonly Color HoverRowColor = new Color(1f, 1f, 1f, 0.04f); // Highlight color on hover

        private void RebuildFilterCache()
        {
            cachedFilteredDefs.Clear();
            string newSearch = "";
            if (activeTab == 0) newSearch = hairSearchQuery;
            else if (activeTab == 1) newSearch = beardSearchQuery;
            else if (activeTab == 2) newSearch = apparelSearchQuery;

            if (activeTab == 0)
            {
                if (allHairs == null)
                {
                    allHairs = new List<HairDef>();
                    foreach (var def in DefDatabase<HairDef>.AllDefsListForReading)
                    {
                        if (def.defName != "Bald") allHairs.Add(def);
                    }
                    allHairs.Sort((a, b) => string.Compare(a.label ?? "", b.label ?? "", StringComparison.OrdinalIgnoreCase));
                }
                foreach (var def in allHairs)
                {
                    string modName = def.modContentPack?.Name ?? "Core";
                    if (selectedModName != "All" && modName != selectedModName) continue;
                    if (!newSearch.NullOrEmpty())
                    {
                        bool labelMatch = def.label != null && def.label.IndexOf(newSearch, StringComparison.OrdinalIgnoreCase) >= 0;
                        bool defNameMatch = def.defName != null && def.defName.IndexOf(newSearch, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!labelMatch && !defNameMatch) continue;
                    }
                    cachedFilteredDefs.Add(def);
                }
            }
            else if (activeTab == 1)
            {
                if (allBeards == null)
                {
                    allBeards = new List<BeardDef>();
                    foreach (var def in DefDatabase<BeardDef>.AllDefsListForReading)
                    {
                        if (def.defName != "NoBeard") allBeards.Add(def);
                    }
                    allBeards.Sort((a, b) => string.Compare(a.label ?? "", b.label ?? "", StringComparison.OrdinalIgnoreCase));
                }
                foreach (var def in allBeards)
                {
                    string modName = def.modContentPack?.Name ?? "Core";
                    if (selectedModName != "All" && modName != selectedModName) continue;
                    if (!newSearch.NullOrEmpty())
                    {
                        bool labelMatch = def.label != null && def.label.IndexOf(newSearch, StringComparison.OrdinalIgnoreCase) >= 0;
                        bool defNameMatch = def.defName != null && def.defName.IndexOf(newSearch, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!labelMatch && !defNameMatch) continue;
                    }
                    cachedFilteredDefs.Add(def);
                }
            }
            else if (activeTab == 2)
            {
                if (allApparels == null)
                {
                    allApparels = new List<ThingDef>();
                    foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                    {
                        if (def != null && def.IsApparel) allApparels.Add(def);
                    }
                    allApparels.Sort((a, b) => string.Compare(a.label ?? "", b.label ?? "", StringComparison.OrdinalIgnoreCase));
                }
                foreach (var def in allApparels)
                {
                    string modName = def.modContentPack?.Name ?? "Core";
                    if (selectedModName != "All" && modName != selectedModName) continue;
                    if (!newSearch.NullOrEmpty())
                    {
                        bool labelMatch = def.label != null && def.label.IndexOf(newSearch, StringComparison.OrdinalIgnoreCase) >= 0;
                        bool defNameMatch = def.defName != null && def.defName.IndexOf(newSearch, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!labelMatch && !defNameMatch) continue;
                    }
                    cachedFilteredDefs.Add(def);
                }
            }
            else if (activeTab == 3)
            {
                if (allBodyTypes == null)
                {
                    allBodyTypes = new List<BodyTypeDef>();
                    foreach (var def in DefDatabase<BodyTypeDef>.AllDefsListForReading)
                    {
                        if (def.defName != "Baby" && def.defName != "Child") allBodyTypes.Add(def);
                    }
                    allBodyTypes.Sort((a, b) => string.Compare(a.label ?? "", b.label ?? "", StringComparison.OrdinalIgnoreCase));
                }
                foreach (var def in allBodyTypes)
                {
                    cachedFilteredDefs.Add(def);
                }
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.InitializeSets();

            // Current editing target gender
            Gender activeGender = Settings.useGenderConfig ? editingGender : Gender.None;

            // Cache check and rebuild if criteria changed
            bool stateChanged = !cacheInitialized || 
                                activeTab != lastActiveTab || 
                                selectedModName != lastModFilter || 
                                hairSearchQuery != lastHairSearch || 
                                beardSearchQuery != lastBeardSearch || 
                                apparelSearchQuery != lastApparelSearch;

            if (stateChanged)
            {
                RebuildFilterCache();
                lastActiveTab = activeTab;
                lastModFilter = selectedModName;
                lastHairSearch = hairSearchQuery;
                lastBeardSearch = beardSearchQuery;
                lastApparelSearch = apparelSearchQuery;
                cacheInitialized = true;
            }
            Settings.InitializeSets();

            // 1. Top Panel: Gender Config and Custom Ratio (Unified Header Card)
            Rect topConfigRect = new Rect(inRect.x, inRect.y, inRect.width, 36f);
            Widgets.DrawRectFast(topConfigRect, PanelBgColor);

            // Gender Config Toggle
            Rect genderConfigCheckRect = new Rect(topConfigRect.x + 10f, topConfigRect.y + 6f, topConfigRect.width * 0.28f, 24f);
            bool useGender = Settings.useGenderConfig;
            Widgets.CheckboxLabeled(genderConfigCheckRect, "NPCStyleLimiter_UseGenderConfig".Translate(), ref useGender);
            if (useGender != Settings.useGenderConfig)
            {
                Settings.useGenderConfig = useGender;
            }

            // Gender selector buttons
            if (Settings.useGenderConfig)
            {
                Rect maleTabRect = new Rect(topConfigRect.x + topConfigRect.width * 0.29f, topConfigRect.y + 6f, 75f, 24f);
                Rect femaleTabRect = new Rect(topConfigRect.x + topConfigRect.width * 0.29f + 80f, topConfigRect.y + 6f, 75f, 24f);
                
                if (Widgets.ButtonText(maleTabRect, "NPCStyleLimiter_Male".Translate(), editingGender == Gender.Male, true, true))
                {
                    editingGender = Gender.Male;
                }
                if (Widgets.ButtonText(femaleTabRect, "NPCStyleLimiter_Female".Translate(), editingGender == Gender.Female, true, true))
                {
                    editingGender = Gender.Female;
                }
            }

            // Custom Gender Ratio Toggle
            Rect genderRatioCheckRect = new Rect(topConfigRect.x + topConfigRect.width * 0.58f, topConfigRect.y + 6f, topConfigRect.width * 0.22f, 24f);
            bool adjustRatio = Settings.adjustGenderRatio;
            Widgets.CheckboxLabeled(genderRatioCheckRect, "NPCStyleLimiter_AdjustGenderRatio".Translate(), ref adjustRatio);
            if (adjustRatio != Settings.adjustGenderRatio)
            {
                Settings.adjustGenderRatio = adjustRatio;
            }

            // Gender ratio slider
            if (Settings.adjustGenderRatio)
            {
                Rect ratioSliderRect = new Rect(topConfigRect.x + topConfigRect.width * 0.81f, topConfigRect.y + 6f, topConfigRect.width * 0.18f, 24f);
                float newRatio = Widgets.HorizontalSlider(ratioSliderRect, Settings.maleRatio, 0f, 1f, false, 
                    "NPCStyleLimiter_MaleRatioLabel".Translate(Settings.maleRatio.ToString("P0"), (1f - Settings.maleRatio).ToString("P0")), 
                    null, null, 0.05f);
                if (newRatio != Settings.maleRatio)
                {
                    Settings.maleRatio = newRatio;
                }
            }

            // Active gender is already resolved above


            // 2. Tabs UI (Custom styled with bottom active line, modern dashboard look)
            Rect tabRect = new Rect(inRect.x, inRect.y + 50f, inRect.width, 35f);
            float tabWidth = tabRect.width / 4f;

            if (DrawCustomTab(new Rect(tabRect.x, tabRect.y, tabWidth, tabRect.height), "NPCStyleLimiter_HairStyles".Translate(), activeTab == 0))
            {
                activeTab = 0;
                scrollPosition = Vector2.zero;
                selectedModName = "All";
            }
            if (DrawCustomTab(new Rect(tabRect.x + tabWidth, tabRect.y, tabWidth, tabRect.height), "NPCStyleLimiter_BeardStyles".Translate(), activeTab == 1))
            {
                activeTab = 1;
                scrollPosition = Vector2.zero;
                selectedModName = "All";
            }
            if (DrawCustomTab(new Rect(tabRect.x + tabWidth * 2f, tabRect.y, tabWidth, tabRect.height), "NPCStyleLimiter_Apparel".Translate(), activeTab == 2))
            {
                activeTab = 2;
                scrollPosition = Vector2.zero;
                selectedModName = "All";
            }
            if (DrawCustomTab(new Rect(tabRect.x + tabWidth * 3f, tabRect.y, tabWidth, tabRect.height), "NPCStyleLimiter_BodyTypes".Translate(), activeTab == 3))
            {
                activeTab = 3;
                scrollPosition = Vector2.zero;
                selectedModName = "All";
            }

            // 3. Filters and controls row (only for tabs 0, 1, 2)
            float filterRowY = inRect.y + 95f;
            bool showSearchAndModFilters = (activeTab != 3);

            if (showSearchAndModFilters)
            {
                // Background card for filters
                Rect filtersPanelRect = new Rect(inRect.x, filterRowY, inRect.width, 40f);
                Widgets.DrawRectFast(filtersPanelRect, PanelBgColor);

                Rect searchRect = new Rect(filtersPanelRect.x + 10f, filterRowY + 5f, inRect.width * 0.33f, 30f);
                Rect modFilterRect = new Rect(filtersPanelRect.x + inRect.width * 0.35f, filterRowY + 5f, inRect.width * 0.33f, 30f);
                Rect buttonAllRect = new Rect(filtersPanelRect.x + inRect.width * 0.70f, filterRowY + 5f, inRect.width * 0.13f, 30f);
                Rect buttonNoneRect = new Rect(filtersPanelRect.x + inRect.width * 0.84f, filterRowY + 5f, inRect.width * 0.14f, 30f);

                // Draw search query input
                string prevSearch = "";
                if (activeTab == 0) prevSearch = hairSearchQuery;
                else if (activeTab == 1) prevSearch = beardSearchQuery;
                else if (activeTab == 2) prevSearch = apparelSearchQuery;

                string newSearch = Widgets.TextField(searchRect, prevSearch);
                if (activeTab == 0) hairSearchQuery = newSearch;
                else if (activeTab == 1) beardSearchQuery = newSearch;
                else if (activeTab == 2) apparelSearchQuery = newSearch;

                // Draw Mod filter selector
                string displayMod = selectedModName == "All" ? "NPCStyleLimiter_FilterModAll".Translate().ToString() : "NPCStyleLimiter_FilterMod".Translate(selectedModName).ToString();
                if (Widgets.ButtonText(modFilterRect, displayMod))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    options.Add(new FloatMenuOption("NPCStyleLimiter_AllMods".Translate(), () => selectedModName = "All"));
                    
                    HashSet<string> availableMods = new HashSet<string>();
                    if (activeTab == 0)
                    {
                        foreach (var def in DefDatabase<HairDef>.AllDefsListForReading)
                        {
                            availableMods.Add(def.modContentPack?.Name ?? "Core");
                        }
                    }
                    else if (activeTab == 1)
                    {
                        foreach (var def in DefDatabase<BeardDef>.AllDefsListForReading)
                        {
                            availableMods.Add(def.modContentPack?.Name ?? "Core");
                        }
                    }
                    else if (activeTab == 2)
                    {
                        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                        {
                            if (def != null && def.IsApparel)
                            {
                                availableMods.Add(def.modContentPack?.Name ?? "Core");
                            }
                        }
                    }

                    List<string> sortedMods = new List<string>(availableMods);
                    sortedMods.Sort();

                    foreach (var mod in sortedMods)
                    {
                        options.Add(new FloatMenuOption(mod, () => selectedModName = mod));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                }

                // Draw Allow All / Disallow All buttons
                if (Widgets.ButtonText(buttonAllRect, "NPCStyleLimiter_AllowAll".Translate()))
                {
                    foreach (var def in cachedFilteredDefs)
                    {
                        Settings.SetWeight(def, activeGender, 1.0f);
                    }
                }
                if (Widgets.ButtonText(buttonNoneRect, "NPCStyleLimiter_DisallowAll".Translate()))
                {
                    foreach (var def in cachedFilteredDefs)
                    {
                        Settings.SetWeight(def, activeGender, 0.0f);
                    }
                }

                // Draw List
                DrawList(cachedFilteredDefs, filterRowY + 45f, inRect, activeGender);
            }
            else
            {
                // Draw List
                DrawList(cachedFilteredDefs, filterRowY, inRect, activeGender);
            }

            // Bottom Buttons: Save Changes & Restore Defaults
            float buttonGap = 10f;
            float restoreWidth = 160f;
            float saveWidth = inRect.width - restoreWidth - buttonGap;

            Rect saveRect = new Rect(inRect.x, inRect.yMax - 35f, saveWidth, 35f);
            Rect restoreRect = new Rect(inRect.x + saveWidth + buttonGap, inRect.yMax - 35f, restoreWidth, 35f);

            if (Widgets.ButtonText(saveRect, "NPCStyleLimiter_SaveChanges".Translate()))
            {
                Settings.Write();
                Messages.Message("NPCStyleLimiter_SettingsSaved".Translate(), MessageTypeDefOf.TaskCompletion, false);
            }

            if (Widgets.ButtonText(restoreRect, "NPCStyleLimiter_RestoreDefaults".Translate()))
            {
                Settings.ResetToDefaults();
                Settings.Write();
                Messages.Message("NPCStyleLimiter_DefaultsRestored".Translate(), MessageTypeDefOf.CautionInput, false);
            }
        }

        private void DrawList(List<Def> defs, float startY, Rect contentRect, Gender activeGender)
        {
            float listHeight = contentRect.height - (startY - contentRect.y) - 45f;
            Rect scrollOuterRect = new Rect(contentRect.x, startY, contentRect.width, listHeight);

            if (defs == null || defs.Count == 0)
            {
                Widgets.DrawRectFast(scrollOuterRect, PanelBgColor);
                TextAnchor originalAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = InactiveTextColor;
                Widgets.Label(scrollOuterRect, "NPCStyleLimiter_NoResultsFound".Translate());
                GUI.color = Color.white;
                Text.Anchor = originalAnchor;
                return;
            }

            float rowHeight = 38f; // Increased row height for more modern layout breathing space
            float viewHeight = defs.Count * rowHeight;
            Rect scrollInnerRect = new Rect(0f, 0f, scrollOuterRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(scrollOuterRect, ref scrollPosition, scrollInnerRect);

            // Calculate visible index range
            int firstVisibleIndex = Mathf.FloorToInt(scrollPosition.y / rowHeight);
            int lastVisibleIndex = Mathf.CeilToInt((scrollPosition.y + listHeight) / rowHeight);

            // Clamp indexes to list boundaries
            firstVisibleIndex = Mathf.Clamp(firstVisibleIndex, 0, defs.Count - 1);
            lastVisibleIndex = Mathf.Clamp(lastVisibleIndex, 0, defs.Count - 1);

            for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
            {
                if (i >= defs.Count) break;

                Def def = defs[i];
                float currentY = i * rowHeight;
                Rect rowRect = new Rect(0f, currentY, scrollInnerRect.width, rowHeight);
                
                // Hover effect
                bool rowHovered = Mouse.IsOver(rowRect);
                if (rowHovered)
                {
                    Widgets.DrawRectFast(rowRect, HoverRowColor);
                }
                else if (i % 2 == 1)
                {
                    Widgets.DrawLightHighlight(rowRect);
                }

                float currentWeight = Settings.GetWeight(def, activeGender);
                bool isAllowed = currentWeight > 0f;

                // Checkbox UI (Using Toggle Switch style)
                Rect checkboxRect = new Rect(10f, currentY, 36f, rowHeight);
                bool checkState = DrawCustomCheckbox(checkboxRect, isAllowed);
                if (checkState != isAllowed)
                {
                    float nextWeight = checkState ? 1.0f : 0.0f;
                    Settings.SetWeight(def, activeGender, nextWeight);
                }

                // Icon Preview
                Texture2D iconTex = null;
                Color iconColor = Color.white;

                if (def is StyleItemDef styleItem)
                {
                    iconTex = styleItem.Icon;
                }
                else if (def is ThingDef thingDef)
                {
                    iconTex = thingDef.uiIcon;
                    iconColor = thingDef.uiIconColor;
                }

                if (iconTex != null)
                {
                    Rect iconRect = new Rect(56f, currentY + (rowHeight - 24f) / 2f, 24f, 24f);
                    GUI.color = iconColor;
                    Widgets.DrawTextureFitted(iconRect, iconTex, 1f);
                    GUI.color = Color.white;
                }

                // Label for the item
                Rect labelRect = new Rect(90f, currentY, scrollInnerRect.width * 0.35f, rowHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, (def.LabelCap.Resolve() ?? "") + $" ({def.defName})");

                // Slider for weight (uses row height bounds to make sliding easier)
                Rect sliderRect = new Rect(scrollInnerRect.width * 0.50f, currentY, scrollInnerRect.width * 0.22f, rowHeight);
                float newWeight = DrawCustomSlider(sliderRect, currentWeight, 0f, 5f, 0.1f);
                if (newWeight != currentWeight)
                {
                    Settings.SetWeight(def, activeGender, newWeight);
                }

                // Slider value text
                Rect valLabelRect = new Rect(scrollInnerRect.width * 0.74f, currentY, 50f, rowHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                if (newWeight > 0f)
                {
                    GUI.color = AccentColor;
                    Widgets.Label(valLabelRect, newWeight.ToString("0.0") + "x");
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = Color.gray;
                    Widgets.Label(valLabelRect, "0.0x");
                    GUI.color = Color.white;
                }

                // Source Mod
                Rect modLabelRect = new Rect(scrollInnerRect.width * 0.81f, currentY, scrollInnerRect.width * 0.19f, rowHeight);
                string modName = def.modContentPack?.Name ?? "Core";
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Widgets.Label(modLabelRect, modName);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            Widgets.EndScrollView();
        }

        // --- Custom Modern UI Elements ---

        private bool DrawCustomTab(Rect rect, string label, bool selected)
        {
            bool hovered = Mouse.IsOver(rect);
            
            // Draw hover overlay
            if (hovered)
            {
                Widgets.DrawRectFast(rect, new Color(1f, 1f, 1f, 0.03f));
            }

            TextAnchor originalAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            if (selected)
            {
                GUI.color = AccentColor;
            }
            else
            {
                GUI.color = hovered ? Color.white : InactiveTextColor;
            }

            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = originalAnchor;

            // Bottom accent underline
            if (selected)
            {
                Rect lineRect = new Rect(rect.x, rect.yMax - 3f, rect.width, 3f);
                Widgets.DrawRectFast(lineRect, AccentColor);
            }
            else if (hovered)
            {
                Rect lineRect = new Rect(rect.x, rect.yMax - 3f, rect.width, 3f);
                Widgets.DrawRectFast(lineRect, new Color(1f, 1f, 1f, 0.15f));
            }

            return Widgets.ButtonInvisible(rect);
        }

        private float DrawCustomSlider(Rect rect, float value, float min, float max, float roundTo = -1f)
        {
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            // Draw thin slider track bar
            float trackHeight = 6f;
            Rect trackRect = new Rect(rect.x, rect.y + (rect.height - trackHeight) / 2f, rect.width, trackHeight);
            Widgets.DrawRectFast(trackRect, SliderTrackColor);

            // Calculate fill percentage
            float pct = (value - min) / (max - min);
            pct = Mathf.Clamp01(pct);
            float fillWidth = trackRect.width * pct;
            
            // Draw filled track progress bar
            if (fillWidth > 0f)
            {
                Rect fillRect = new Rect(trackRect.x, trackRect.y, fillWidth, trackHeight);
                Widgets.DrawRectFast(fillRect, AccentColor);
            }

            // Draw modern flat thumb knob
            float thumbWidth = 10f;
            float thumbHeight = 14f;
            float thumbX = trackRect.x + fillWidth - thumbWidth / 2f;
            Rect thumbRect = new Rect(thumbX, rect.y + (rect.height - thumbHeight) / 2f, thumbWidth, thumbHeight);

            bool hovered = Mouse.IsOver(rect);
            bool thumbHovered = Mouse.IsOver(thumbRect);
            
            Color thumbColor = (GUIUtility.hotControl == controlID) ? Color.white : 
                               (thumbHovered ? Color.white : (hovered ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.75f, 0.75f, 0.75f)));
            Widgets.DrawRectFast(thumbRect, thumbColor);

            // Event handling using hotControl
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && Mouse.IsOver(rect))
            {
                GUIUtility.hotControl = controlID;
                float clickPct = (currentEvent.mousePosition.x - rect.x) / rect.width;
                value = min + (max - min) * clickPct;
                value = Mathf.Clamp(value, min, max);
                if (roundTo > 0f) value = Mathf.Round(value / roundTo) * roundTo;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag && GUIUtility.hotControl == controlID)
            {
                float dragPct = (currentEvent.mousePosition.x - rect.x) / rect.width;
                value = min + (max - min) * dragPct;
                value = Mathf.Clamp(value, min, max);
                if (roundTo > 0f) value = Mathf.Round(value / roundTo) * roundTo;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp && GUIUtility.hotControl == controlID)
            {
                GUIUtility.hotControl = 0;
                currentEvent.Use();
            }

            return value;
        }

        private bool DrawCustomCheckbox(Rect rect, bool active)
        {
            // Toggle switch layout
            float switchW = 34f;
            float switchH = 18f;
            Rect switchRect = new Rect(rect.x, rect.y + (rect.height - switchH) / 2f, switchW, switchH);
            
            // Toggle track background
            Color bg = active ? AccentColor : new Color(0.18f, 0.18f, 0.18f);
            Widgets.DrawRectFast(switchRect, bg);
            
            // Toggle sliding knob
            float knobSize = 14f;
            float knobY = switchRect.y + (switchH - knobSize) / 2f;
            float knobX = active ? (switchRect.xMax - knobSize - 2f) : (switchRect.x + 2f);
            Rect knobRect = new Rect(knobX, knobY, knobSize, knobSize);
            
            Widgets.DrawRectFast(knobRect, Color.white);
            
            if (Widgets.ButtonInvisible(switchRect))
            {
                return !active;
            }
            return active;
        }


    }

    [StaticConstructorOnStartup]
    public static class ModStartup
    {
        static ModStartup()
        {
            var harmony = new Harmony("rinmiolc.npcstylelimiter");
            harmony.PatchAll();

            // Build the runtime weights cache
            if (CustomizerMod.Settings != null)
            {
                CustomizerMod.Settings.ResolveRuntimeWeights();
            }
        }
    }
}
