// Copyright (c) 2026 rinmiolc
// Licensed under the GNU General Public License v3.0.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace NPCStyleLimiter
{
    public class Dialog_ManageConfigs : Window
    {
        public override Vector2 InitialSize => new Vector2(520f, 520f);

        private Vector2 scrollPosition = Vector2.zero;
        private string saveAsName = "";
        private string renameBuffer = "";
        private string renameTarget = null;
        private bool focusSaveField = false;

        private static readonly Color AccentColor = new Color(0.78f, 0.55f, 0.15f);
        private static readonly Color PanelBgColor = new Color(1f, 1f, 1f, 0.02f);
        private static readonly Color HoverRowColor = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color InactiveTextColor = new Color(0.55f, 0.6f, 0.62f);

        public Dialog_ManageConfigs(bool focusSave = false)
        {
            this.focusSaveField = focusSave;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float curY = inRect.y;

            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, curY, inRect.width, 30f), "NPCStyleLimiter_ProfileManager".Translate());
            Text.Font = GameFont.Small;
            curY += 35f;

            // Current active profile
            string activeName = CustomizerMod.Settings.currentProfileName ?? "Default";
            GUI.color = AccentColor;
            Widgets.Label(new Rect(inRect.x, curY, inRect.width, 22f), "NPCStyleLimiter_CurrentProfile".Translate(activeName));
            GUI.color = Color.white;
            curY += 26f;

            Widgets.DrawRectFast(new Rect(inRect.x, curY, inRect.width, 1f), new Color(1f, 1f, 1f, 0.1f));
            curY += 12f;

            // Save Section
            Widgets.Label(new Rect(inRect.x, curY + 3f, 100f, 24f), "NPCStyleLimiter_SaveAs".Translate());
            Rect nameFieldRect = new Rect(inRect.x + 105f, curY, inRect.width - 200f, 24f);
            
            GUI.SetNextControlName("SaveNameField");
            saveAsName = Widgets.TextField(nameFieldRect, saveAsName);
            if (focusSaveField)
            {
                GUI.FocusControl("SaveNameField");
                focusSaveField = false;
            }

            Rect saveBtnRect = new Rect(inRect.xMax - 85f, curY, 85f, 24f);
            bool canSave = !string.IsNullOrEmpty(saveAsName.Trim()) && saveAsName.Trim() != "Default";
            
            if (!canSave) GUI.color = Color.gray;
            if (Widgets.ButtonText(saveBtnRect, "NPCStyleLimiter_Save".Translate()) && canSave)
            {
                string trimName = saveAsName.Trim();
                if (CustomizerMod.Settings.ListProfiles().Contains(trimName))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("NPCStyleLimiter_ConfirmOverwrite".Translate(trimName), () =>
                    {
                        CustomizerMod.Settings.SaveProfile(trimName);
                        Messages.Message("NPCStyleLimiter_ProfileSaved".Translate(trimName), MessageTypeDefOf.TaskCompletion, false);
                    }));
                }
                else
                {
                    CustomizerMod.Settings.SaveProfile(trimName);
                    Messages.Message("NPCStyleLimiter_ProfileSaved".Translate(trimName), MessageTypeDefOf.TaskCompletion, false);
                }
            }
            GUI.color = Color.white;
            curY += 36f;

            Widgets.DrawRectFast(new Rect(inRect.x, curY, inRect.width, 1f), new Color(1f, 1f, 1f, 0.1f));
            curY += 12f;

            // Profile list (scrollable)
            List<string> profiles = CustomizerMod.Settings.ListProfiles();
            if (!profiles.Contains("Default")) profiles.Insert(0, "Default");

            float listHeight = inRect.yMax - curY - 10f;
            Rect scrollOuterRect = new Rect(inRect.x, curY, inRect.width, listHeight);

            float rowHeight = 36f;
            float viewHeight = profiles.Count * rowHeight;
            Rect scrollInnerRect = new Rect(0f, 0f, scrollOuterRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(scrollOuterRect, ref scrollPosition, scrollInnerRect);

            for (int i = 0; i < profiles.Count; i++)
            {
                string profileName = profiles[i];
                float rowY = i * rowHeight;
                Rect rowRect = new Rect(0f, rowY, scrollInnerRect.width, rowHeight);

                bool isDefault = profileName == "Default";
                bool isActive = profileName == CustomizerMod.Settings.currentProfileName;
                bool isRenaming = renameTarget == profileName;

                // Row background
                if (isActive) Widgets.DrawRectFast(rowRect, new Color(0.78f, 0.55f, 0.15f, 0.12f));
                else if (Mouse.IsOver(rowRect)) Widgets.DrawRectFast(rowRect, HoverRowColor);
                else if (i % 2 == 1) Widgets.DrawLightHighlight(rowRect);

                // Active indicator dot
                if (isActive)
                {
                    GUI.color = AccentColor;
                    Widgets.Label(new Rect(4f, rowY + 8f, 16f, 20f), "\u25CF");
                    GUI.color = Color.white;
                }

                float nameWidth = scrollInnerRect.width * 0.42f;
                if (isRenaming)
                {
                    // ... rename logic (same as before)
                }
                else
                {
                    TextAnchor prevAnchor = Text.Anchor;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    if (isActive) GUI.color = AccentColor;
                    string labelText = isDefault ? (profileName + " (" + "NPCStyleLimiter_ReadOnly".Translate().ToString() + ")") : profileName;
                    Widgets.Label(new Rect(24f, rowY, nameWidth, rowHeight), labelText);
                    GUI.color = Color.white;
                    Text.Anchor = prevAnchor;

                    // Action buttons
                    float btnW = 56f;
                    float btnH = 24f;
                    float btnY = rowY + (rowHeight - btnH) / 2f;
                    float btnX = scrollInnerRect.width - 180f;

                    // Rename (Hidden for Default)
                    if (!isDefault)
                    {
                        if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, btnH), "NPCStyleLimiter_Rename".Translate()))
                        {
                            renameTarget = profileName;
                            renameBuffer = profileName;
                        }
                    }
                    btnX += btnW + 4f;

                    // Load
                    if (isActive) GUI.color = Color.gray;
                    if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, btnH), "NPCStyleLimiter_Load".Translate()) && !isActive)
                    {
                        if (isDefault) CustomizerMod.Settings.ResetToDefaults();
                        else CustomizerMod.Settings.LoadProfile(profileName);
                        Messages.Message("NPCStyleLimiter_ProfileLoaded".Translate(profileName), MessageTypeDefOf.TaskCompletion, false);
                        Close();
                    }
                    GUI.color = Color.white;
                    btnX += btnW + 4f;

                    // Delete (Hidden for Default)
                    if (!isDefault)
                    {
                        if (isActive) GUI.color = Color.gray;
                        if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, btnH), "NPCStyleLimiter_Delete".Translate()) && !isActive)
                        {
                            CustomizerMod.Settings.DeleteProfile(profileName);
                        }
                        GUI.color = Color.white;
                    }
                }
            }
            Widgets.EndScrollView();

            Widgets.EndScrollView();
        }
    }
}
